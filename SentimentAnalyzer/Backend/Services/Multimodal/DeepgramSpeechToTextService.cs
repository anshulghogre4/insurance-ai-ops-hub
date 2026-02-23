using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SentimentAnalyzer.Agents.Configuration;
using SentimentAnalyzer.Agents.Orchestration;

namespace SentimentAnalyzer.API.Services.Multimodal;

/// <summary>
/// Speech-to-text service using Deepgram Nova-2 API.
/// Free tier: $200 credit (~3,300 hours of transcription).
/// Insurance use case: transcribing field adjuster voice notes and call recordings.
/// PII redaction is applied to transcribed text before returning to callers.
/// </summary>
public class DeepgramSpeechToTextService : ISpeechToTextService
{
    private readonly HttpClient _httpClient;
    private readonly DeepgramSettings _settings;
    private readonly ILogger<DeepgramSpeechToTextService> _logger;
    private readonly IPIIRedactor? _piiRedactor;

    public DeepgramSpeechToTextService(
        HttpClient httpClient,
        IOptions<AgentSystemSettings> settings,
        ILogger<DeepgramSpeechToTextService> logger,
        IPIIRedactor? piiRedactor = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _settings = settings?.Value?.Deepgram ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _piiRedactor = piiRedactor;
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
                Provider = "Deepgram",
                ErrorMessage = "Deepgram API key not configured."
            };
        }

        try
        {
            var requestUrl = $"{_settings.Endpoint}/listen?model={_settings.Model}&smart_format=true&punctuate=true";

            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Token", _settings.ApiKey);
            request.Content = new ByteArrayContent(audioData);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(mimeType);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Deepgram API returned {StatusCode}: {Error}", response.StatusCode, errorBody);
                return new TranscriptionResult
                {
                    IsSuccess = false,
                    Provider = "Deepgram",
                    ErrorMessage = $"Deepgram API error: {response.StatusCode}"
                };
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var transcript = root
                .GetProperty("results")
                .GetProperty("channels")[0]
                .GetProperty("alternatives")[0];

            var text = transcript.GetProperty("transcript").GetString() ?? string.Empty;
            var confidence = transcript.GetProperty("confidence").GetDouble();

            var duration = root.GetProperty("metadata").TryGetProperty("duration", out var dur)
                ? dur.GetDouble()
                : 0.0;

            _logger.LogInformation("Deepgram transcription completed. Length: {Length} chars, Confidence: {Confidence:F2}",
                text.Length, confidence);

            // Redact PII from transcribed text before returning to callers
            var sanitizedText = _piiRedactor?.Redact(text) ?? text;

            return new TranscriptionResult
            {
                IsSuccess = true,
                Text = sanitizedText,
                Confidence = confidence,
                DurationSeconds = duration,
                Provider = "Deepgram"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Deepgram transcription failed");
            return new TranscriptionResult
            {
                IsSuccess = false,
                Provider = "Deepgram",
                ErrorMessage = $"Transcription error: {ex.Message}"
            };
        }
    }
}
