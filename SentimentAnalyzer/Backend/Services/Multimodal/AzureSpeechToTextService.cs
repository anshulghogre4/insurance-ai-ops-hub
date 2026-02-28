using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SentimentAnalyzer.Agents.Configuration;
using SentimentAnalyzer.Agents.Orchestration;

namespace SentimentAnalyzer.API.Services.Multimodal;

/// <summary>
/// Speech-to-text service using Azure AI Speech REST API (NOT the SDK — avoids .NET 10 TFM issues).
/// Free F0 tier: 5 hours STT/month. Hard cap — 429 after limit.
/// Insurance use case: transcribing policyholder call recordings, adjuster voice notes, and claims hotline audio.
/// PII redaction is applied to transcribed text before returning to callers.
///
/// REST endpoint: POST https://{region}.stt.speech.microsoft.com/speech/recognition/conversation/cognitiveservices/v1
/// </summary>
public class AzureSpeechToTextService : ISpeechToTextService
{
    /// <summary>Maximum audio file size for Azure Speech F0 tier (25MB).</summary>
    public const int MaxFileSizeBytes = 25 * 1024 * 1024;

    private readonly HttpClient _httpClient;
    private readonly AzureSpeechSettings _settings;
    private readonly IPIIRedactor? _piiRedactor;
    private readonly ILogger<AzureSpeechToTextService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureSpeechToTextService"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client for making REST API calls.</param>
    /// <param name="settings">Agent system settings containing Azure Speech configuration.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="piiRedactor">Optional PII redactor for sanitizing transcribed text.</param>
    public AzureSpeechToTextService(
        HttpClient httpClient,
        IOptions<AgentSystemSettings> settings,
        ILogger<AzureSpeechToTextService> logger,
        IPIIRedactor? piiRedactor = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _settings = settings?.Value?.AzureSpeech ?? new AzureSpeechSettings();
        _piiRedactor = piiRedactor;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<TranscriptionResult> TranscribeAsync(
        byte[] audioData,
        string mimeType = "audio/wav",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            return new TranscriptionResult
            {
                IsSuccess = false,
                Provider = "AzureSpeech",
                ErrorMessage = "Azure Speech API key not configured."
            };
        }

        if (string.IsNullOrWhiteSpace(_settings.Region))
        {
            return new TranscriptionResult
            {
                IsSuccess = false,
                Provider = "AzureSpeech",
                ErrorMessage = "Azure Speech region not configured."
            };
        }

        if (audioData is null || audioData.Length == 0)
        {
            return new TranscriptionResult
            {
                IsSuccess = false,
                Provider = "AzureSpeech",
                ErrorMessage = "Audio data is empty."
            };
        }

        if (audioData.Length > MaxFileSizeBytes)
        {
            _logger.LogWarning(
                "Audio file size {Size} bytes exceeds Azure Speech 25MB limit ({MaxSize} bytes). Falling back to next provider.",
                audioData.Length, MaxFileSizeBytes);

            return new TranscriptionResult
            {
                IsSuccess = false,
                Provider = "AzureSpeech",
                ErrorMessage = "Audio file exceeds Azure Speech 25MB file size limit."
            };
        }

        try
        {
            var contentType = MapMimeType(mimeType);
            var requestUrl = $"https://{_settings.Region}.stt.speech.microsoft.com/speech/recognition/conversation/cognitiveservices/v1?language=en-US";

            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            request.Headers.Add("Ocp-Apim-Subscription-Key", _settings.ApiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = new ByteArrayContent(audioData);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

            _logger.LogInformation(
                "Starting Azure Speech transcription for {Size} byte audio ({MimeType}) in region {Region}",
                audioData.Length, mimeType, _settings.Region);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Azure Speech API returned {StatusCode}: {Error}",
                    response.StatusCode, errorBody);

                return new TranscriptionResult
                {
                    IsSuccess = false,
                    Provider = "AzureSpeech",
                    ErrorMessage = $"Azure Speech API error: {response.StatusCode}"
                };
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var speechResponse = JsonSerializer.Deserialize<SpeechRecognitionResponse>(json, _jsonOptions);

            if (speechResponse is null)
            {
                _logger.LogWarning("Azure Speech returned null/unparseable response");
                return new TranscriptionResult
                {
                    IsSuccess = false,
                    Provider = "AzureSpeech",
                    ErrorMessage = "Azure Speech returned an unparseable response."
                };
            }

            if (!string.Equals(speechResponse.RecognitionStatus, "Success", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Azure Speech recognition status: {Status}",
                    speechResponse.RecognitionStatus);

                return new TranscriptionResult
                {
                    IsSuccess = false,
                    Provider = "AzureSpeech",
                    ErrorMessage = $"Azure Speech recognition failed with status: {speechResponse.RecognitionStatus}"
                };
            }

            var text = speechResponse.DisplayText ?? string.Empty;

            // Duration is in 100-nanosecond ticks — convert to seconds
            var durationSeconds = speechResponse.Duration / 10_000_000.0;

            _logger.LogInformation(
                "Azure Speech transcription completed. Length: {Length} chars, Duration: {Duration:F2}s",
                text.Length, durationSeconds);

            // Redact PII from transcribed text before returning to callers
            var sanitizedText = _piiRedactor?.Redact(text) ?? text;

            return new TranscriptionResult
            {
                IsSuccess = true,
                Text = sanitizedText,
                Confidence = 0.9, // Azure Speech REST v1 does not return per-utterance confidence; default high
                DurationSeconds = durationSeconds,
                Provider = "AzureSpeech"
            };
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken == cancellationToken)
        {
            _logger.LogWarning("Azure Speech transcription was cancelled");
            return new TranscriptionResult
            {
                IsSuccess = false,
                Provider = "AzureSpeech",
                ErrorMessage = "Transcription was cancelled."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure Speech transcription failed unexpectedly");
            return new TranscriptionResult
            {
                IsSuccess = false,
                Provider = "AzureSpeech",
                ErrorMessage = $"Azure Speech error: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Maps common audio MIME types to Azure Speech-compatible Content-Type headers.
    /// </summary>
    /// <param name="mimeType">Input MIME type (e.g., "audio/wav", "audio/mpeg", "audio/webm").</param>
    /// <returns>Content-Type string compatible with Azure Speech REST API.</returns>
    private static string MapMimeType(string mimeType) => mimeType?.ToLowerInvariant() switch
    {
        "audio/wav" or "audio/x-wav" => "audio/wav",
        "audio/mpeg" or "audio/mp3" => "audio/mpeg",
        "audio/webm" => "audio/webm",
        "audio/ogg" => "audio/ogg",
        _ => "audio/wav" // Default to WAV for unknown types
    };

    /// <summary>JSON serializer options with case-insensitive property matching.</summary>
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Deserialization model for Azure Speech REST API recognition response.
    /// </summary>
    private sealed class SpeechRecognitionResponse
    {
        /// <summary>Recognition status: "Success", "NoMatch", "InitialSilenceTimeout", etc.</summary>
        public string RecognitionStatus { get; set; } = string.Empty;

        /// <summary>Formatted transcription text with punctuation and capitalization.</summary>
        public string DisplayText { get; set; } = string.Empty;

        /// <summary>Offset of the recognized speech in 100-nanosecond ticks.</summary>
        public long Offset { get; set; }

        /// <summary>Duration of the recognized speech in 100-nanosecond ticks.</summary>
        public long Duration { get; set; }
    }
}
