using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SentimentAnalyzer.Agents.Configuration;

namespace SentimentAnalyzer.API.Services.Embeddings;

/// <summary>
/// Embedding service using Cohere's embed-multilingual-v3.0 model (1024-dim).
/// Free trial tier: 100 req/min, 1,000 req/month. No credit card required.
/// API: POST https://api.cohere.com/v2/embed
///
/// Insurance use case: multilingual claims embedding for RAG semantic retrieval.
/// Cohere v3 embeddings support input_type for search optimization (search_document vs search_query).
///
/// IMPORTANT: PII MUST be redacted before calling this service (external API).
/// Use the ResilientEmbeddingProvider which handles PII redaction automatically.
/// </summary>
public class CohereEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly CohereEmbeddingSettings _settings;
    private readonly ILogger<CohereEmbeddingService> _logger;

    /// <summary>
    /// Maximum number of texts per batch request (Cohere limit: 96 for embed v3).
    /// </summary>
    private const int MaxBatchSize = 96;

    /// <summary>
    /// Cohere embed-multilingual-v3.0 produces 1024-dimensional embeddings.
    /// </summary>
    private const int CohereDimension = 1024;

    /// <summary>
    /// Initializes the Cohere embedding service.
    /// </summary>
    /// <param name="httpClient">HTTP client for API requests.</param>
    /// <param name="settings">Agent system settings containing Cohere configuration.</param>
    /// <param name="logger">Logger instance.</param>
    public CohereEmbeddingService(
        HttpClient httpClient,
        IOptions<AgentSystemSettings> settings,
        ILogger<CohereEmbeddingService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _settings = settings?.Value?.CohereEmbedding ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public int EmbeddingDimension => CohereDimension;

    /// <inheritdoc />
    public string ProviderName => "Cohere";

    /// <inheritdoc />
    public async Task<EmbeddingResult> GenerateEmbeddingAsync(
        string text,
        string? inputType = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            return new EmbeddingResult
            {
                IsSuccess = false,
                Provider = ProviderName,
                ErrorMessage = "Cohere API key not configured. Use: dotnet user-secrets set \"AgentSystem:CohereEmbedding:ApiKey\" \"your-key\"",
                ElapsedMilliseconds = sw.ElapsedMilliseconds
            };
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return new EmbeddingResult
            {
                IsSuccess = false,
                Provider = ProviderName,
                ErrorMessage = "Text cannot be empty.",
                ElapsedMilliseconds = sw.ElapsedMilliseconds
            };
        }

        try
        {
            var requestBody = BuildRequestBody(new[] { text }, inputType);
            var requestUrl = $"{_settings.Endpoint}/embed";

            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
            request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Cohere returned {StatusCode}: {Error}", response.StatusCode, errorBody);

                return new EmbeddingResult
                {
                    IsSuccess = false,
                    Provider = ProviderName,
                    ErrorMessage = $"Cohere API error: {response.StatusCode}",
                    ElapsedMilliseconds = sw.ElapsedMilliseconds
                };
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var parsed = ParseSingleEmbedding(json);
            parsed.ElapsedMilliseconds = sw.ElapsedMilliseconds;

            _logger.LogInformation(
                "Cohere embedding generated in {ElapsedMs}ms. Dimension: {Dim}, Tokens: {Tokens}",
                parsed.ElapsedMilliseconds, parsed.Dimension, parsed.TokensUsed);

            return parsed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cohere embedding generation failed");
            return new EmbeddingResult
            {
                IsSuccess = false,
                Provider = ProviderName,
                ErrorMessage = $"Embedding error: {ex.Message}",
                ElapsedMilliseconds = sw.ElapsedMilliseconds
            };
        }
    }

    /// <inheritdoc />
    public async Task<BatchEmbeddingResult> GenerateBatchEmbeddingsAsync(
        string[] texts,
        string? inputType = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            return new BatchEmbeddingResult
            {
                IsSuccess = false,
                Provider = ProviderName,
                ErrorMessage = "Cohere API key not configured.",
                ElapsedMilliseconds = sw.ElapsedMilliseconds
            };
        }

        if (texts == null || texts.Length == 0)
        {
            return new BatchEmbeddingResult
            {
                IsSuccess = false,
                Provider = ProviderName,
                ErrorMessage = "Texts array cannot be null or empty.",
                ElapsedMilliseconds = sw.ElapsedMilliseconds
            };
        }

        if (texts.Length > MaxBatchSize)
        {
            return new BatchEmbeddingResult
            {
                IsSuccess = false,
                Provider = ProviderName,
                ErrorMessage = $"Batch size {texts.Length} exceeds maximum of {MaxBatchSize}.",
                ElapsedMilliseconds = sw.ElapsedMilliseconds
            };
        }

        try
        {
            var requestBody = BuildRequestBody(texts, inputType);
            var requestUrl = $"{_settings.Endpoint}/embed";

            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
            request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Cohere batch returned {StatusCode}: {Error}", response.StatusCode, errorBody);

                return new BatchEmbeddingResult
                {
                    IsSuccess = false,
                    Provider = ProviderName,
                    ErrorMessage = $"Cohere API error: {response.StatusCode}",
                    ElapsedMilliseconds = sw.ElapsedMilliseconds
                };
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var parsed = ParseBatchEmbeddings(json);
            parsed.ElapsedMilliseconds = sw.ElapsedMilliseconds;

            _logger.LogInformation(
                "Cohere batch embedding completed in {ElapsedMs}ms. Count: {Count}, Dimension: {Dim}, Tokens: {Tokens}",
                parsed.ElapsedMilliseconds, parsed.Count, parsed.Dimension, parsed.TotalTokensUsed);

            return parsed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cohere batch embedding generation failed");
            return new BatchEmbeddingResult
            {
                IsSuccess = false,
                Provider = ProviderName,
                ErrorMessage = $"Batch embedding error: {ex.Message}",
                ElapsedMilliseconds = sw.ElapsedMilliseconds
            };
        }
    }

    /// <summary>
    /// Builds the JSON request body for the Cohere v2 embed endpoint.
    /// Cohere v3 requires input_type for all embed calls.
    /// Request format:
    /// {
    ///   "texts": ["text1", "text2"],
    ///   "model": "embed-multilingual-v3.0",
    ///   "input_type": "search_document" | "search_query",
    ///   "embedding_types": ["float"]
    /// }
    /// </summary>
    private string BuildRequestBody(string[] texts, string? inputType)
    {
        // Map generic input types to Cohere-specific types
        var cohereInputType = inputType switch
        {
            "query" => "search_query",
            "document" => "search_document",
            _ => "search_document" // Default: Cohere v3 requires input_type
        };

        var requestObj = new Dictionary<string, object>
        {
            ["texts"] = texts,
            ["model"] = _settings.Model,
            ["input_type"] = cohereInputType,
            ["embedding_types"] = new[] { "float" }
        };

        return JsonSerializer.Serialize(requestObj);
    }

    /// <summary>
    /// Parses the Cohere v2 single-embedding response.
    /// Response format:
    /// {
    ///   "id": "...",
    ///   "embeddings": { "float": [[0.1, 0.2, ...]] },
    ///   "texts": ["..."],
    ///   "meta": { "billed_units": { "input_tokens": 42 } }
    /// }
    /// </summary>
    private EmbeddingResult ParseSingleEmbedding(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var embeddingsObj = root.GetProperty("embeddings");
        var floatArray = embeddingsObj.GetProperty("float");
        if (floatArray.GetArrayLength() == 0)
        {
            return new EmbeddingResult
            {
                IsSuccess = false,
                Provider = ProviderName,
                ErrorMessage = "Cohere returned empty embeddings array."
            };
        }

        var embedding = ParseFloatArray(floatArray[0]);

        var tokensUsed = 0;
        if (root.TryGetProperty("meta", out var meta) &&
            meta.TryGetProperty("billed_units", out var billedUnits) &&
            billedUnits.TryGetProperty("input_tokens", out var tokens))
        {
            tokensUsed = tokens.GetInt32();
        }

        return new EmbeddingResult
        {
            IsSuccess = true,
            Embedding = embedding,
            Provider = ProviderName,
            TokensUsed = tokensUsed
        };
    }

    /// <summary>
    /// Parses the Cohere v2 batch embedding response.
    /// </summary>
    private BatchEmbeddingResult ParseBatchEmbeddings(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var embeddingsObj = root.GetProperty("embeddings");
        var floatArray = embeddingsObj.GetProperty("float");
        var embeddings = new float[floatArray.GetArrayLength()][];

        var index = 0;
        foreach (var item in floatArray.EnumerateArray())
        {
            embeddings[index++] = ParseFloatArray(item);
        }

        var tokensUsed = 0;
        if (root.TryGetProperty("meta", out var meta) &&
            meta.TryGetProperty("billed_units", out var billedUnits) &&
            billedUnits.TryGetProperty("input_tokens", out var tokens))
        {
            tokensUsed = tokens.GetInt32();
        }

        return new BatchEmbeddingResult
        {
            IsSuccess = true,
            Embeddings = embeddings,
            Provider = ProviderName,
            TotalTokensUsed = tokensUsed
        };
    }

    /// <summary>
    /// Parses a JSON array of numbers into a float array.
    /// </summary>
    private static float[] ParseFloatArray(JsonElement arrayElement)
    {
        var count = arrayElement.GetArrayLength();
        var result = new float[count];
        var i = 0;
        foreach (var element in arrayElement.EnumerateArray())
        {
            result[i++] = element.GetSingle();
        }
        return result;
    }
}
