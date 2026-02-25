using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SentimentAnalyzer.Agents.Configuration;

namespace SentimentAnalyzer.API.Services.Multimodal;

/// <summary>
/// Financial sentiment pre-screening using HuggingFace FinBERT (ProsusAI/finbert).
/// Free tier: rate-limited (300 requests/hour, shared with NER).
/// Insurance use case: rapid sentiment triage before full multi-agent orchestration.
/// PII MUST be redacted before calling this service (unlike NER which needs raw text).
/// </summary>
public class FinBertSentimentService : IFinancialSentimentPreScreener
{
    private readonly HttpClient _httpClient;
    private readonly HuggingFaceSettings _settings;
    private readonly ILogger<FinBertSentimentService> _logger;
    private readonly double _confidenceThreshold;
    private static readonly string _baseUrl = "https://router.huggingface.co/hf-inference/models";

    public FinBertSentimentService(
        HttpClient httpClient,
        IOptions<AgentSystemSettings> settings,
        ILogger<FinBertSentimentService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _settings = settings?.Value?.HuggingFace ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _confidenceThreshold = _settings.PreScreenConfidenceThreshold;
    }

    /// <inheritdoc />
    public async Task<FinancialSentimentResult> PreScreenAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            return new FinancialSentimentResult
            {
                IsSuccess = false,
                Provider = "HuggingFace/FinBERT",
                ErrorMessage = "HuggingFace API key not configured.",
                ElapsedMilliseconds = sw.ElapsedMilliseconds
            };
        }

        try
        {
            var requestUrl = $"{_baseUrl}/{_settings.SentimentModel}";
            var payload = JsonSerializer.Serialize(new { inputs = text });

            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, cancellationToken);

            // Handle model cold start (503 with estimated_time)
            if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
            {
                var errorJson = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("FinBERT model loading (cold start). Response: {Response}", errorJson);
                return new FinancialSentimentResult
                {
                    IsSuccess = false,
                    Provider = "HuggingFace/FinBERT",
                    ErrorMessage = "FinBERT model is loading (cold start). Please retry in 20-30 seconds.",
                    ElapsedMilliseconds = sw.ElapsedMilliseconds
                };
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("FinBERT API returned {StatusCode}: {Error}", response.StatusCode, errorBody);
                return new FinancialSentimentResult
                {
                    IsSuccess = false,
                    Provider = "HuggingFace/FinBERT",
                    ErrorMessage = $"FinBERT API error: {response.StatusCode}",
                    ElapsedMilliseconds = sw.ElapsedMilliseconds
                };
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = ParseFinBertResponse(json);
            result.ElapsedMilliseconds = sw.ElapsedMilliseconds;

            _logger.LogInformation(
                "FinBERT pre-screening completed in {ElapsedMs}ms. Sentiment: {Sentiment}, " +
                "Score: {Score:F3}, HighConfidence: {IsHigh}, Threshold: {Threshold}",
                result.ElapsedMilliseconds, result.Sentiment, result.TopScore,
                result.IsHighConfidence, _confidenceThreshold);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FinBERT pre-screening failed");
            return new FinancialSentimentResult
            {
                IsSuccess = false,
                Provider = "HuggingFace/FinBERT",
                ErrorMessage = $"Pre-screening error: {ex.Message}",
                ElapsedMilliseconds = sw.ElapsedMilliseconds
            };
        }
    }

    /// <summary>
    /// Parses the FinBERT API response. FinBERT returns a nested array:
    /// [[{"label": "positive", "score": 0.92}, {"label": "negative", "score": 0.05}, ...]]
    /// </summary>
    private FinancialSentimentResult ParseFinBertResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // FinBERT wraps results in a double array: [[{...}, {...}, {...}]]
        var labelArray = root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0
            ? root[0]
            : root;

        var scores = new Dictionary<string, double>();
        var topLabel = "neutral";
        var topScore = 0.0;

        foreach (var item in labelArray.EnumerateArray())
        {
            var label = item.GetProperty("label").GetString() ?? "unknown";
            var score = item.GetProperty("score").GetDouble();
            scores[label] = score;

            if (score > topScore)
            {
                topScore = score;
                topLabel = label;
            }
        }

        return new FinancialSentimentResult
        {
            IsSuccess = true,
            Sentiment = topLabel,
            TopScore = topScore,
            Scores = scores,
            IsHighConfidence = topScore >= _confidenceThreshold,
            Provider = "HuggingFace/FinBERT"
        };
    }
}
