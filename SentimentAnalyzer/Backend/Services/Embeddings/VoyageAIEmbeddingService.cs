using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SentimentAnalyzer.Agents.Configuration;

namespace SentimentAnalyzer.API.Services.Embeddings;

/// <summary>
/// Embedding service using Voyage AI's voyage-finance-2 model (1024-dim, finance-optimized).
/// Free tier: 50M tokens. No credit card required.
/// API: POST https://api.voyageai.com/v1/embeddings
///
/// Insurance use case: generating finance-domain embeddings for insurance policy documents,
/// claim descriptions, and regulatory correspondence for RAG semantic retrieval.
///
/// IMPORTANT: PII MUST be redacted before calling this service (external API).
/// Use the ResilientEmbeddingProvider which handles PII redaction automatically.
/// </summary>
public class VoyageAIEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly VoyageSettings _settings;
    private readonly ILogger<VoyageAIEmbeddingService> _logger;

    /// <summary>
    /// Maximum number of texts per batch request (Voyage AI limit: 1000, but we cap at 128
    /// to stay well within the 120K token limit for voyage-finance-2).
    /// </summary>
    private const int MaxBatchSize = 128;

    /// <summary>
    /// Voyage AI voyage-finance-2 produces 1024-dimensional embeddings.
    /// </summary>
    private const int VoyageDimension = 1024;

    public VoyageAIEmbeddingService(
        HttpClient httpClient,
        IOptions<AgentSystemSettings> settings,
        ILogger<VoyageAIEmbeddingService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _settings = settings?.Value?.Voyage ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public int EmbeddingDimension => VoyageDimension;

    /// <inheritdoc />
    public string ProviderName => "VoyageAI";

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
                ErrorMessage = "Voyage AI API key not configured. Use: dotnet user-secrets set \"AgentSystem:Voyage:ApiKey\" \"your-key\"",
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
                _logger.LogWarning("Voyage AI returned {StatusCode}: {Error}", response.StatusCode, errorBody);

                return new EmbeddingResult
                {
                    IsSuccess = false,
                    Provider = ProviderName,
                    ErrorMessage = $"Voyage AI API error: {response.StatusCode}",
                    ElapsedMilliseconds = sw.ElapsedMilliseconds
                };
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var parsed = ParseSingleEmbedding(json);
            parsed.ElapsedMilliseconds = sw.ElapsedMilliseconds;

            _logger.LogInformation(
                "Voyage AI embedding generated in {ElapsedMs}ms. Dimension: {Dim}, Tokens: {Tokens}",
                parsed.ElapsedMilliseconds, parsed.Dimension, parsed.TokensUsed);

            return parsed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Voyage AI embedding generation failed");
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
                ErrorMessage = "Voyage AI API key not configured.",
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
                _logger.LogWarning("Voyage AI batch returned {StatusCode}: {Error}", response.StatusCode, errorBody);

                return new BatchEmbeddingResult
                {
                    IsSuccess = false,
                    Provider = ProviderName,
                    ErrorMessage = $"Voyage AI API error: {response.StatusCode}",
                    ElapsedMilliseconds = sw.ElapsedMilliseconds
                };
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var parsed = ParseBatchEmbeddings(json);
            parsed.ElapsedMilliseconds = sw.ElapsedMilliseconds;

            _logger.LogInformation(
                "Voyage AI batch embedding completed in {ElapsedMs}ms. Count: {Count}, Dimension: {Dim}, Tokens: {Tokens}",
                parsed.ElapsedMilliseconds, parsed.Count, parsed.Dimension, parsed.TotalTokensUsed);

            return parsed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Voyage AI batch embedding generation failed");
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
    /// Builds the JSON request body for the Voyage AI embeddings endpoint.
    /// Request format:
    /// {
    ///   "input": ["text1", "text2"],
    ///   "model": "voyage-finance-2",
    ///   "input_type": "document" | "query" | null,
    ///   "truncation": true
    /// }
    /// </summary>
    private string BuildRequestBody(string[] texts, string? inputType)
    {
        var requestObj = new Dictionary<string, object>
        {
            ["input"] = texts.Length == 1 ? (object)texts[0] : texts,
            ["model"] = _settings.Model,
            ["truncation"] = true
        };

        if (!string.IsNullOrWhiteSpace(inputType))
        {
            requestObj["input_type"] = inputType;
        }

        return JsonSerializer.Serialize(requestObj);
    }

    /// <summary>
    /// Parses the Voyage AI single-embedding response.
    /// Response format:
    /// {
    ///   "object": "list",
    ///   "data": [{ "object": "embedding", "embedding": [0.1, 0.2, ...], "index": 0 }],
    ///   "model": "voyage-finance-2",
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
                ErrorMessage = "Voyage AI returned empty data array."
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
    /// Parses the Voyage AI batch embedding response.
    /// Data array contains embeddings ordered by index.
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
