using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SentimentAnalyzer.Agents.Configuration;

namespace SentimentAnalyzer.API.Services.Embeddings;

/// <summary>
/// Embedding service using Jina AI's jina-embeddings-v3 model (1024-dim, multilingual).
/// Free tier: 1M tokens. No credit card required. OpenAI-compatible endpoint.
/// API: POST https://api.jina.ai/v1/embeddings
///
/// Insurance use case: multilingual embedding fallback for insurance policy documents,
/// claim descriptions, and regulatory correspondence for RAG semantic retrieval.
///
/// IMPORTANT: PII MUST be redacted before calling this service (external API).
/// Use the ResilientEmbeddingProvider which handles PII redaction automatically.
/// </summary>
public class JinaEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly JinaSettings _settings;
    private readonly ILogger<JinaEmbeddingService> _logger;

    /// <summary>
    /// Maximum number of texts per batch request.
    /// Jina supports up to 2048 inputs, capped at 100 for safety and token limits.
    /// </summary>
    private const int MaxBatchSize = 100;

    /// <summary>
    /// Jina jina-embeddings-v3 produces 1024-dimensional embeddings (configurable, default 1024).
    /// </summary>
    private const int JinaDimension = 1024;

    /// <summary>
    /// Initializes the Jina AI embedding service.
    /// </summary>
    /// <param name="httpClient">HTTP client for API requests.</param>
    /// <param name="settings">Agent system settings containing Jina configuration.</param>
    /// <param name="logger">Logger instance.</param>
    public JinaEmbeddingService(
        HttpClient httpClient,
        IOptions<AgentSystemSettings> settings,
        ILogger<JinaEmbeddingService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _settings = settings?.Value?.Jina ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public int EmbeddingDimension => JinaDimension;

    /// <inheritdoc />
    public string ProviderName => "Jina";

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
                ErrorMessage = "Jina AI API key not configured. Use: dotnet user-secrets set \"AgentSystem:Jina:ApiKey\" \"your-key\"",
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
            var requestUrl = $"{_settings.Endpoint}/embeddings";

            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
            request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Jina AI returned {StatusCode}: {Error}", response.StatusCode, errorBody);

                return new EmbeddingResult
                {
                    IsSuccess = false,
                    Provider = ProviderName,
                    ErrorMessage = $"Jina AI API error: {response.StatusCode}",
                    ElapsedMilliseconds = sw.ElapsedMilliseconds
                };
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var parsed = ParseSingleEmbedding(json);
            parsed.ElapsedMilliseconds = sw.ElapsedMilliseconds;

            _logger.LogInformation(
                "Jina AI embedding generated in {ElapsedMs}ms. Dimension: {Dim}, Tokens: {Tokens}",
                parsed.ElapsedMilliseconds, parsed.Dimension, parsed.TokensUsed);

            return parsed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Jina AI embedding generation failed");
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
                ErrorMessage = "Jina AI API key not configured.",
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
            var requestUrl = $"{_settings.Endpoint}/embeddings";

            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
            request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Jina AI batch returned {StatusCode}: {Error}", response.StatusCode, errorBody);

                return new BatchEmbeddingResult
                {
                    IsSuccess = false,
                    Provider = ProviderName,
                    ErrorMessage = $"Jina AI API error: {response.StatusCode}",
                    ElapsedMilliseconds = sw.ElapsedMilliseconds
                };
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var parsed = ParseBatchEmbeddings(json);
            parsed.ElapsedMilliseconds = sw.ElapsedMilliseconds;

            _logger.LogInformation(
                "Jina AI batch embedding completed in {ElapsedMs}ms. Count: {Count}, Dimension: {Dim}, Tokens: {Tokens}",
                parsed.ElapsedMilliseconds, parsed.Count, parsed.Dimension, parsed.TotalTokensUsed);

            return parsed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Jina AI batch embedding generation failed");
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
    /// Builds the JSON request body for the Jina AI embeddings endpoint.
    /// Request format:
    /// {
    ///   "model": "jina-embeddings-v3",
    ///   "input": ["text1", "text2"],
    ///   "task": "retrieval.document" | "retrieval.query",
    ///   "dimensions": 1024
    /// }
    /// </summary>
    private string BuildRequestBody(string[] texts, string? inputType)
    {
        var task = MapInputTypeToTask(inputType);

        var requestObj = new Dictionary<string, object>
        {
            ["model"] = _settings.Model,
            ["input"] = texts,
            ["task"] = task,
            ["dimensions"] = JinaDimension
        };

        return JsonSerializer.Serialize(requestObj);
    }

    /// <summary>
    /// Maps the generic inputType ("query"/"document") to Jina-specific task values.
    /// Jina uses "retrieval.query" for search queries and "retrieval.document" for indexed content.
    /// </summary>
    private static string MapInputTypeToTask(string? inputType)
    {
        return inputType?.ToLowerInvariant() switch
        {
            "query" => "retrieval.query",
            "document" => "retrieval.document",
            _ => "retrieval.document" // Default to document for general embedding
        };
    }

    /// <summary>
    /// Parses the Jina AI single-embedding response (OpenAI-compatible format).
    /// Response format:
    /// {
    ///   "data": [{ "embedding": [0.1, 0.2, ...], "index": 0 }],
    ///   "usage": { "total_tokens": 42 }
    /// }
    /// </summary>
    private EmbeddingResult ParseSingleEmbedding(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var dataArray = root.GetProperty("data");
        if (dataArray.GetArrayLength() == 0)
        {
            return new EmbeddingResult
            {
                IsSuccess = false,
                Provider = ProviderName,
                ErrorMessage = "Jina AI returned empty data array."
            };
        }

        var embeddingElement = dataArray[0].GetProperty("embedding");
        var embedding = ParseFloatArray(embeddingElement);

        var tokensUsed = 0;
        if (root.TryGetProperty("usage", out var usage) &&
            usage.TryGetProperty("total_tokens", out var tokens))
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
    /// Parses the Jina AI batch embedding response (OpenAI-compatible format).
    /// </summary>
    private BatchEmbeddingResult ParseBatchEmbeddings(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var dataArray = root.GetProperty("data");
        var embeddings = new float[dataArray.GetArrayLength()][];

        foreach (var item in dataArray.EnumerateArray())
        {
            var index = item.GetProperty("index").GetInt32();
            var embeddingElement = item.GetProperty("embedding");
            embeddings[index] = ParseFloatArray(embeddingElement);
        }

        var tokensUsed = 0;
        if (root.TryGetProperty("usage", out var usage) &&
            usage.TryGetProperty("total_tokens", out var tokens))
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
