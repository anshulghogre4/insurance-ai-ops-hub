using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SentimentAnalyzer.Agents.Configuration;

namespace SentimentAnalyzer.API.Services.Embeddings;

/// <summary>
/// Embedding service using HuggingFace Inference API with BAAI/bge-large-en-v1.5 model (1024-dim).
/// Free tier: rate-limited (300 requests/hour). Shares API key with NER service.
/// API: POST https://api-inference.huggingface.co/pipeline/feature-extraction/{model}
///
/// Insurance use case: open-source embedding fallback before Ollama local,
/// for RAG document indexing when cloud embedding providers are exhausted.
///
/// IMPORTANT: PII MUST be redacted before calling this service (external API).
/// Use the ResilientEmbeddingProvider which handles PII redaction automatically.
/// </summary>
public class HuggingFaceEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly HuggingFaceEmbeddingSettings _settings;
    private readonly ILogger<HuggingFaceEmbeddingService> _logger;

    /// <summary>
    /// Maximum number of texts per batch request.
    /// HuggingFace free tier is rate-limited (300 req/hr), so we keep batches small.
    /// </summary>
    private const int MaxBatchSize = 20;

    /// <summary>
    /// HuggingFace Inference API base URL for feature extraction pipeline.
    /// </summary>
    private const string HuggingFaceBaseUrl = "https://api-inference.huggingface.co/pipeline/feature-extraction";

    /// <summary>
    /// BAAI/bge-large-en-v1.5 produces 1024-dimensional embeddings natively.
    /// </summary>
    private const int HuggingFaceDimension = 1024;

    /// <summary>
    /// Initializes the HuggingFace embedding service.
    /// </summary>
    /// <param name="httpClient">HTTP client for API requests.</param>
    /// <param name="settings">Agent system settings containing HuggingFace embedding configuration.</param>
    /// <param name="logger">Logger instance.</param>
    public HuggingFaceEmbeddingService(
        HttpClient httpClient,
        IOptions<AgentSystemSettings> settings,
        ILogger<HuggingFaceEmbeddingService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _settings = settings?.Value?.HuggingFaceEmbedding ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public int EmbeddingDimension => HuggingFaceDimension;

    /// <inheritdoc />
    public string ProviderName => "HuggingFaceEmbed";

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
                ErrorMessage = "HuggingFace Embedding API key not configured. Use: dotnet user-secrets set \"AgentSystem:HuggingFaceEmbedding:ApiKey\" \"your-key\"",
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
            var requestUrl = $"{HuggingFaceBaseUrl}/{_settings.Model}";

            var requestObj = new Dictionary<string, object>
            {
                ["inputs"] = text,
                ["options"] = new Dictionary<string, bool>
                {
                    ["wait_for_model"] = true
                }
            };

            var requestBody = JsonSerializer.Serialize(requestObj);

            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
            request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("HuggingFace Embedding returned {StatusCode}: {Error}", response.StatusCode, errorBody);

                return new EmbeddingResult
                {
                    IsSuccess = false,
                    Provider = ProviderName,
                    ErrorMessage = $"HuggingFace Embedding API error: {response.StatusCode}",
                    ElapsedMilliseconds = sw.ElapsedMilliseconds
                };
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var parsed = ParseFeatureExtractionResponse(json);
            parsed.ElapsedMilliseconds = sw.ElapsedMilliseconds;

            _logger.LogInformation(
                "HuggingFace embedding generated in {ElapsedMs}ms. Dimension: {Dim}",
                parsed.ElapsedMilliseconds, parsed.Dimension);

            return parsed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HuggingFace embedding generation failed");
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
                ErrorMessage = "HuggingFace Embedding API key not configured.",
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
            // HuggingFace feature-extraction supports array input for batch
            var requestUrl = $"{HuggingFaceBaseUrl}/{_settings.Model}";

            var requestObj = new Dictionary<string, object>
            {
                ["inputs"] = texts,
                ["options"] = new Dictionary<string, bool>
                {
                    ["wait_for_model"] = true
                }
            };

            var requestBody = JsonSerializer.Serialize(requestObj);

            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
            request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("HuggingFace batch embedding returned {StatusCode}: {Error}", response.StatusCode, errorBody);

                return new BatchEmbeddingResult
                {
                    IsSuccess = false,
                    Provider = ProviderName,
                    ErrorMessage = $"HuggingFace Embedding API error: {response.StatusCode}",
                    ElapsedMilliseconds = sw.ElapsedMilliseconds
                };
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var parsed = ParseBatchFeatureExtractionResponse(json);
            parsed.ElapsedMilliseconds = sw.ElapsedMilliseconds;

            _logger.LogInformation(
                "HuggingFace batch embedding completed in {ElapsedMs}ms. Count: {Count}, Dimension: {Dim}",
                parsed.ElapsedMilliseconds, parsed.Count, parsed.Dimension);

            return parsed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HuggingFace batch embedding generation failed");
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
    /// Parses the HuggingFace feature-extraction response for a single text.
    /// BGE models return a 2D array: [[token1_emb], [token2_emb], ...] (per-token embeddings).
    /// We use mean pooling to get a single sentence embedding, consistent with BGE usage.
    /// Alternatively, for sentence-transformers models, the response may be a flat 1D array.
    /// </summary>
    private EmbeddingResult ParseFeatureExtractionResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
        {
            var firstElement = root[0];

            if (firstElement.ValueKind == JsonValueKind.Array)
            {
                // 2D array: per-token embeddings — mean pool
                var embedding = MeanPoolEmbeddings(root);
                return new EmbeddingResult
                {
                    IsSuccess = true,
                    Embedding = embedding,
                    Provider = ProviderName,
                    TokensUsed = 0
                };
            }
            else if (firstElement.ValueKind == JsonValueKind.Number)
            {
                // 1D array: sentence embedding directly
                var embedding = ParseFloatArray(root);
                return new EmbeddingResult
                {
                    IsSuccess = true,
                    Embedding = embedding,
                    Provider = ProviderName,
                    TokensUsed = 0
                };
            }
        }

        return new EmbeddingResult
        {
            IsSuccess = false,
            Provider = ProviderName,
            ErrorMessage = "HuggingFace returned unexpected embedding format."
        };
    }

    /// <summary>
    /// Parses the HuggingFace feature-extraction batch response.
    /// Returns a 3D array: [text_idx][token_idx][dim] for per-token models,
    /// or a 2D array: [text_idx][dim] for sentence-level models.
    /// </summary>
    private BatchEmbeddingResult ParseBatchFeatureExtractionResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
        {
            return new BatchEmbeddingResult
            {
                IsSuccess = false,
                Provider = ProviderName,
                ErrorMessage = "HuggingFace returned empty batch response."
            };
        }

        var embeddings = new float[root.GetArrayLength()][];
        var index = 0;

        foreach (var textResult in root.EnumerateArray())
        {
            if (textResult.ValueKind == JsonValueKind.Array && textResult.GetArrayLength() > 0)
            {
                var firstInner = textResult[0];
                if (firstInner.ValueKind == JsonValueKind.Array)
                {
                    // Per-token: mean pool
                    embeddings[index] = MeanPoolEmbeddings(textResult);
                }
                else
                {
                    // Sentence-level embedding
                    embeddings[index] = ParseFloatArray(textResult);
                }
            }
            else
            {
                embeddings[index] = [];
            }
            index++;
        }

        return new BatchEmbeddingResult
        {
            IsSuccess = true,
            Embeddings = embeddings,
            Provider = ProviderName,
            TotalTokensUsed = 0
        };
    }

    /// <summary>
    /// Mean-pools per-token embeddings into a single sentence embedding.
    /// Input: 2D JSON array [num_tokens][embedding_dim].
    /// Output: 1D float array [embedding_dim] (average of all token embeddings).
    /// </summary>
    private static float[] MeanPoolEmbeddings(JsonElement tokensArray)
    {
        var tokenCount = tokensArray.GetArrayLength();
        if (tokenCount == 0) return [];

        var dim = tokensArray[0].GetArrayLength();
        var summed = new double[dim];

        foreach (var tokenEmb in tokensArray.EnumerateArray())
        {
            var i = 0;
            foreach (var val in tokenEmb.EnumerateArray())
            {
                if (i < dim)
                {
                    summed[i] += val.GetDouble();
                }
                i++;
            }
        }

        var result = new float[dim];
        for (var i = 0; i < dim; i++)
        {
            result[i] = (float)(summed[i] / tokenCount);
        }

        return result;
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
