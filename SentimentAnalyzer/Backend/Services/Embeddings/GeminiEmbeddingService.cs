using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SentimentAnalyzer.Agents.Configuration;

namespace SentimentAnalyzer.API.Services.Embeddings;

/// <summary>
/// Embedding service using Google Gemini's text-embedding-004 model (768-dim native).
/// Free tier: 1,500 requests/day. Can reuse existing Gemini API key.
/// API: POST https://generativelanguage.googleapis.com/v1beta/models/{model}:embedContent?key={apiKey}
///
/// Insurance use case: high-quality embedding fallback for RAG document indexing
/// when Voyage AI, Jina, and Cohere free tiers are exhausted.
///
/// IMPORTANT: PII MUST be redacted before calling this service (external API).
/// Use the ResilientEmbeddingProvider which handles PII redaction automatically.
/// </summary>
public class GeminiEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly GeminiEmbeddingSettings _settings;
    private readonly ILogger<GeminiEmbeddingService> _logger;

    /// <summary>
    /// Maximum number of texts per batch request.
    /// Gemini batchEmbedContents supports up to 100 requests per call.
    /// </summary>
    private const int MaxBatchSize = 100;

    /// <summary>
    /// Gemini text-embedding-004 produces 768-dimensional embeddings natively.
    /// The outputDimensionality parameter only truncates (cannot exceed native dim).
    /// ResilientEmbeddingProvider will log a dimension mismatch warning when Gemini
    /// is used as fallback for the 1024-dim chain (Voyage/Jina/Cohere).
    /// </summary>
    private const int GeminiDimension = 768;

    /// <summary>
    /// Gemini embedContent API base URL.
    /// </summary>
    private const string GeminiBaseUrl = "https://generativelanguage.googleapis.com/v1beta";

    /// <summary>
    /// Initializes the Gemini embedding service.
    /// </summary>
    /// <param name="httpClient">HTTP client for API requests.</param>
    /// <param name="settings">Agent system settings containing Gemini embedding configuration.</param>
    /// <param name="logger">Logger instance.</param>
    public GeminiEmbeddingService(
        HttpClient httpClient,
        IOptions<AgentSystemSettings> settings,
        ILogger<GeminiEmbeddingService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _settings = settings?.Value?.GeminiEmbedding ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public int EmbeddingDimension => GeminiDimension;

    /// <inheritdoc />
    public string ProviderName => "GeminiEmbed";

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
                ErrorMessage = "Gemini Embedding API key not configured. Use: dotnet user-secrets set \"AgentSystem:GeminiEmbedding:ApiKey\" \"your-key\"",
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
            var requestUrl = $"{GeminiBaseUrl}/models/{_settings.Model}:embedContent?key={_settings.ApiKey}";

            // Map generic input type to Gemini task_type
            var taskType = inputType switch
            {
                "query" => "RETRIEVAL_QUERY",
                "document" => "RETRIEVAL_DOCUMENT",
                _ => "RETRIEVAL_DOCUMENT"
            };

            var requestObj = new Dictionary<string, object>
            {
                ["model"] = $"models/{_settings.Model}",
                ["content"] = new Dictionary<string, object>
                {
                    ["parts"] = new[] { new Dictionary<string, string> { ["text"] = text } }
                },
                ["taskType"] = taskType,
                ["outputDimensionality"] = GeminiDimension
            };

            var requestBody = JsonSerializer.Serialize(requestObj);

            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Gemini Embedding returned {StatusCode}: {Error}", response.StatusCode, errorBody);

                return new EmbeddingResult
                {
                    IsSuccess = false,
                    Provider = ProviderName,
                    ErrorMessage = $"Gemini Embedding API error: {response.StatusCode}",
                    ElapsedMilliseconds = sw.ElapsedMilliseconds
                };
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var parsed = ParseEmbedContentResponse(json);
            parsed.ElapsedMilliseconds = sw.ElapsedMilliseconds;

            _logger.LogInformation(
                "Gemini embedding generated in {ElapsedMs}ms. Dimension: {Dim}",
                parsed.ElapsedMilliseconds, parsed.Dimension);

            return parsed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini embedding generation failed");
            return new EmbeddingResult
            {
                IsSuccess = false,
                Provider = ProviderName,
                ErrorMessage = $"Embedding error: {SanitizeErrorMessage(ex.Message)}",
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
                ErrorMessage = "Gemini Embedding API key not configured.",
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
            // Gemini uses batchEmbedContents endpoint for batch requests
            var requestUrl = $"{GeminiBaseUrl}/models/{_settings.Model}:batchEmbedContents?key={_settings.ApiKey}";

            var taskType = inputType switch
            {
                "query" => "RETRIEVAL_QUERY",
                "document" => "RETRIEVAL_DOCUMENT",
                _ => "RETRIEVAL_DOCUMENT"
            };

            var requests = new List<Dictionary<string, object>>();
            foreach (var text in texts)
            {
                requests.Add(new Dictionary<string, object>
                {
                    ["model"] = $"models/{_settings.Model}",
                    ["content"] = new Dictionary<string, object>
                    {
                        ["parts"] = new[] { new Dictionary<string, string> { ["text"] = text } }
                    },
                    ["taskType"] = taskType,
                    ["outputDimensionality"] = GeminiDimension
                });
            }

            var requestObj = new Dictionary<string, object>
            {
                ["requests"] = requests
            };

            var requestBody = JsonSerializer.Serialize(requestObj);

            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Gemini batch embedding returned {StatusCode}: {Error}", response.StatusCode, errorBody);

                return new BatchEmbeddingResult
                {
                    IsSuccess = false,
                    Provider = ProviderName,
                    ErrorMessage = $"Gemini Embedding API error: {response.StatusCode}",
                    ElapsedMilliseconds = sw.ElapsedMilliseconds
                };
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var parsed = ParseBatchEmbedContentResponse(json);
            parsed.ElapsedMilliseconds = sw.ElapsedMilliseconds;

            _logger.LogInformation(
                "Gemini batch embedding completed in {ElapsedMs}ms. Count: {Count}, Dimension: {Dim}",
                parsed.ElapsedMilliseconds, parsed.Count, parsed.Dimension);

            return parsed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini batch embedding generation failed");
            return new BatchEmbeddingResult
            {
                IsSuccess = false,
                Provider = ProviderName,
                ErrorMessage = $"Batch embedding error: {SanitizeErrorMessage(ex.Message)}",
                ElapsedMilliseconds = sw.ElapsedMilliseconds
            };
        }
    }

    /// <summary>
    /// Parses the Gemini embedContent single-embedding response.
    /// Response format:
    /// {
    ///   "embedding": { "values": [0.1, 0.2, ...] }
    /// }
    /// </summary>
    private EmbeddingResult ParseEmbedContentResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("embedding", out var embeddingObj) ||
            !embeddingObj.TryGetProperty("values", out var valuesArray))
        {
            return new EmbeddingResult
            {
                IsSuccess = false,
                Provider = ProviderName,
                ErrorMessage = "Gemini returned unexpected response format."
            };
        }

        var embedding = ParseFloatArray(valuesArray);

        return new EmbeddingResult
        {
            IsSuccess = true,
            Embedding = embedding,
            Provider = ProviderName,
            TokensUsed = 0 // Gemini embedding API does not report token usage
        };
    }

    /// <summary>
    /// Parses the Gemini batchEmbedContents response.
    /// Response format:
    /// {
    ///   "embeddings": [{ "values": [0.1, 0.2, ...] }, ...]
    /// }
    /// </summary>
    private BatchEmbeddingResult ParseBatchEmbedContentResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("embeddings", out var embeddingsArray))
        {
            return new BatchEmbeddingResult
            {
                IsSuccess = false,
                Provider = ProviderName,
                ErrorMessage = "Gemini returned unexpected batch response format."
            };
        }

        var embeddings = new float[embeddingsArray.GetArrayLength()][];
        var index = 0;
        foreach (var item in embeddingsArray.EnumerateArray())
        {
            var valuesArray = item.GetProperty("values");
            embeddings[index++] = ParseFloatArray(valuesArray);
        }

        return new BatchEmbeddingResult
        {
            IsSuccess = true,
            Embeddings = embeddings,
            Provider = ProviderName,
            TotalTokensUsed = 0 // Gemini embedding API does not report token usage
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

    /// <summary>
    /// Strips the API key from exception messages to prevent credential leakage in logs.
    /// Gemini uses query-string auth (?key=...) which can appear in HttpRequestException messages.
    /// </summary>
    private string SanitizeErrorMessage(string message)
    {
        if (string.IsNullOrEmpty(_settings.ApiKey) || _settings.ApiKey.Length < 8)
            return message;
        return message.Replace(_settings.ApiKey, "[REDACTED]");
    }
}
