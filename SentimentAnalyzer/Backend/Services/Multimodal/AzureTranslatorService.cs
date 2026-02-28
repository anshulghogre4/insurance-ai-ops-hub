using System.Text.Json;
using Microsoft.Extensions.Options;
using SentimentAnalyzer.Agents.Configuration;
using SentimentAnalyzer.Agents.Orchestration;

namespace SentimentAnalyzer.API.Services.Multimodal;

/// <summary>
/// Azure AI Translator service for multilingual claims processing.
/// Free F0 tier: 2M characters/month. Hard cap — 429 after limit.
/// Insurance use case: translating non-English policyholder communications,
/// claims descriptions, and policy documents to English before AI analysis.
/// PII redaction is applied to text before sending to Azure.
/// </summary>
public class AzureTranslatorService : ITranslationService
{
    /// <summary>Azure Translator REST API version.</summary>
    private const string ApiVersion = "3.0";

    /// <summary>Provider identifier for result attribution.</summary>
    private const string ProviderName = "AzureTranslator";

    /// <summary>JSON options for case-insensitive property deserialization.</summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Language code to human-readable name mapping for common insurance languages.</summary>
    private static readonly Dictionary<string, string> LanguageNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = "English",
        ["es"] = "Spanish",
        ["fr"] = "French",
        ["de"] = "German",
        ["pt"] = "Portuguese",
        ["it"] = "Italian",
        ["zh-Hans"] = "Chinese (Simplified)",
        ["zh-Hant"] = "Chinese (Traditional)",
        ["ja"] = "Japanese",
        ["ko"] = "Korean",
        ["ar"] = "Arabic",
        ["hi"] = "Hindi",
        ["vi"] = "Vietnamese",
        ["tl"] = "Filipino",
        ["ru"] = "Russian",
        ["pl"] = "Polish",
        ["nl"] = "Dutch"
    };

    private readonly HttpClient _httpClient;
    private readonly AzureTranslatorSettings _settings;
    private readonly ILogger<AzureTranslatorService> _logger;
    private readonly IPIIRedactor? _piiRedactor;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureTranslatorService"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client for Azure Translator REST API calls.</param>
    /// <param name="settings">Agent system settings containing Azure Translator configuration.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="piiRedactor">Optional PII redactor for sanitizing text before sending to Azure.</param>
    public AzureTranslatorService(
        HttpClient httpClient,
        IOptions<AgentSystemSettings> settings,
        ILogger<AzureTranslatorService> logger,
        IPIIRedactor? piiRedactor = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _settings = settings?.Value?.AzureTranslator ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _piiRedactor = piiRedactor;
    }

    /// <inheritdoc />
    public async Task<TranslationResult> TranslateAsync(
        string text,
        string targetLanguage = "en",
        string? sourceLanguage = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new TranslationResult
            {
                IsSuccess = false,
                Provider = ProviderName,
                ErrorMessage = "Translation text cannot be empty."
            };
        }

        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            return new TranslationResult
            {
                IsSuccess = false,
                Provider = ProviderName,
                ErrorMessage = "Azure Translator API key not configured."
            };
        }

        try
        {
            // Redact PII before sending to Azure
            var sanitizedText = _piiRedactor?.Redact(text) ?? text;

            var endpoint = string.IsNullOrWhiteSpace(_settings.Endpoint)
                ? "https://api.cognitive.microsofttranslator.com"
                : _settings.Endpoint.TrimEnd('/');

            var url = $"{endpoint}/translate?api-version={ApiVersion}&to={targetLanguage}";
            if (!string.IsNullOrWhiteSpace(sourceLanguage))
            {
                url += $"&from={sourceLanguage}";
            }

            var requestBody = new[] { new TranslateRequestItem { Text = sanitizedText } };
            var jsonContent = JsonSerializer.Serialize(requestBody, JsonOptions);

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
            request.Headers.Add("Ocp-Apim-Subscription-Key", _settings.ApiKey);

            if (!string.IsNullOrWhiteSpace(_settings.Region))
            {
                request.Headers.Add("Ocp-Apim-Subscription-Region", _settings.Region);
            }

            _logger.LogInformation(
                "Starting Azure Translator translation to '{TargetLanguage}' for {Length} character text",
                targetLanguage, sanitizedText.Length);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Azure Translator API returned {StatusCode}: {Error}",
                    response.StatusCode, errorBody);

                return new TranslationResult
                {
                    IsSuccess = false,
                    Provider = ProviderName,
                    ErrorMessage = $"Azure Translator API error: {response.StatusCode}"
                };
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var responseItems = JsonSerializer.Deserialize<List<TranslateResponseItem>>(responseJson, JsonOptions);

            if (responseItems is not { Count: > 0 } || responseItems[0].Translations.Count == 0)
            {
                _logger.LogWarning("Azure Translator returned empty translation response");
                return new TranslationResult
                {
                    IsSuccess = false,
                    Provider = ProviderName,
                    ErrorMessage = "Azure Translator returned empty response."
                };
            }

            var firstResult = responseItems[0];
            var translation = firstResult.Translations[0];

            var detectedLanguage = firstResult.DetectedLanguage?.Language ?? sourceLanguage ?? "unknown";
            var confidence = firstResult.DetectedLanguage?.Score ?? 1.0;

            _logger.LogInformation(
                "Azure Translator translation completed. Source: {Source} (confidence: {Confidence:F3}), Target: {Target}, Output length: {Length} chars",
                detectedLanguage, confidence, targetLanguage, translation.Text.Length);

            return new TranslationResult
            {
                IsSuccess = true,
                TranslatedText = translation.Text,
                DetectedSourceLanguage = detectedLanguage,
                Confidence = confidence,
                Provider = ProviderName
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure Translator translation failed unexpectedly");
            return new TranslationResult
            {
                IsSuccess = false,
                Provider = ProviderName,
                ErrorMessage = $"Azure Translator error: {ex.Message}"
            };
        }
    }

    /// <inheritdoc />
    public async Task<LanguageDetectionResult> DetectLanguageAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new LanguageDetectionResult
            {
                IsSuccess = false,
                Provider = ProviderName,
                ErrorMessage = "Detection text cannot be empty."
            };
        }

        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            return new LanguageDetectionResult
            {
                IsSuccess = false,
                Provider = ProviderName,
                ErrorMessage = "Azure Translator API key not configured."
            };
        }

        try
        {
            // Redact PII before sending to Azure
            var sanitizedText = _piiRedactor?.Redact(text) ?? text;

            var endpoint = string.IsNullOrWhiteSpace(_settings.Endpoint)
                ? "https://api.cognitive.microsofttranslator.com"
                : _settings.Endpoint.TrimEnd('/');

            var url = $"{endpoint}/detect?api-version={ApiVersion}";

            var requestBody = new[] { new TranslateRequestItem { Text = sanitizedText } };
            var jsonContent = JsonSerializer.Serialize(requestBody, JsonOptions);

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
            request.Headers.Add("Ocp-Apim-Subscription-Key", _settings.ApiKey);

            if (!string.IsNullOrWhiteSpace(_settings.Region))
            {
                request.Headers.Add("Ocp-Apim-Subscription-Region", _settings.Region);
            }

            _logger.LogInformation(
                "Starting Azure Translator language detection for {Length} character text",
                sanitizedText.Length);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Azure Translator detect API returned {StatusCode}: {Error}",
                    response.StatusCode, errorBody);

                return new LanguageDetectionResult
                {
                    IsSuccess = false,
                    Provider = ProviderName,
                    ErrorMessage = $"Azure Translator detect API error: {response.StatusCode}"
                };
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var responseItems = JsonSerializer.Deserialize<List<DetectResponseItem>>(responseJson, JsonOptions);

            if (responseItems is not { Count: > 0 })
            {
                _logger.LogWarning("Azure Translator returned empty detection response");
                return new LanguageDetectionResult
                {
                    IsSuccess = false,
                    Provider = ProviderName,
                    ErrorMessage = "Azure Translator returned empty detection response."
                };
            }

            var detected = responseItems[0];
            var languageName = LanguageNames.TryGetValue(detected.Language, out var name)
                ? name
                : detected.Language;

            _logger.LogInformation(
                "Azure Translator language detection completed. Detected: {Language} ({Name}), Confidence: {Confidence:F3}",
                detected.Language, languageName, detected.Score);

            return new LanguageDetectionResult
            {
                IsSuccess = true,
                DetectedLanguage = detected.Language,
                LanguageName = languageName,
                Confidence = detected.Score,
                Provider = ProviderName
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure Translator language detection failed unexpectedly");
            return new LanguageDetectionResult
            {
                IsSuccess = false,
                Provider = ProviderName,
                ErrorMessage = $"Azure Translator detect error: {ex.Message}"
            };
        }
    }

    /// <summary>JSON request item for Azure Translator API.</summary>
    private sealed class TranslateRequestItem
    {
        public string Text { get; set; } = string.Empty;
    }

    /// <summary>JSON response item from Azure Translator translate endpoint.</summary>
    private sealed class TranslateResponseItem
    {
        public DetectedLanguageInfo? DetectedLanguage { get; set; }
        public List<TranslationInfo> Translations { get; set; } = [];
    }

    /// <summary>Detected language metadata from Azure Translator.</summary>
    private sealed class DetectedLanguageInfo
    {
        public string Language { get; set; } = string.Empty;
        public double Score { get; set; }
    }

    /// <summary>Translation text and target language from Azure Translator.</summary>
    private sealed class TranslationInfo
    {
        public string Text { get; set; } = string.Empty;
        public string To { get; set; } = string.Empty;
    }

    /// <summary>JSON response item from Azure Translator detect endpoint.</summary>
    private sealed class DetectResponseItem
    {
        public string Language { get; set; } = string.Empty;
        public double Score { get; set; }
        public bool IsTranslationSupported { get; set; }
    }
}
