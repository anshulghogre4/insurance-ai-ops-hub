using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SentimentAnalyzer.Agents.Configuration;

namespace SentimentAnalyzer.API.Services.Embeddings;

/// <summary>
/// Embedding service using Ollama's local inference with mxbai-embed-large model (1024-dim).
/// Always available as local fallback — no API key needed, no rate limits, PII-safe.
/// API: POST http://localhost:11434/api/embed
///
/// Insurance use case: local fallback when Voyage AI free tier is exhausted,
/// or for PII-sensitive documents that should never leave the local network.
///
/// Uses the newer /api/embed endpoint which supports batch input and returns
/// L2-normalized (unit-length) embedding vectors.
/// </summary>
public class OllamaEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly OllamaSettings _settings;
    private readonly ILogger<OllamaEmbeddingService> _logger;

    /// <summary>
    /// Embedding model to use. mxbai-embed-large produces 1024-dim embeddings,
    /// matching Voyage AI voyage-finance-2 dimensionality for index compatibility.
    /// </summary>
    private const string EmbeddingModel = "mxbai-embed-large";

    /// <summary>
    /// mxbai-embed-large produces 1024-dimensional embeddings.
    /// </summary>
    private const int OllamaDimension = 1024;

    /// <summary>
    /// Maximum batch size for sequential processing through Ollama.
    /// Ollama /api/embed supports batch input natively.
    /// </summary>
    private const int MaxBatchSize = 128;

    public OllamaEmbeddingService(
        HttpClient httpClient,
        IOptions<AgentSystemSettings> settings,
        ILogger<OllamaEmbeddingService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _settings = settings?.Value?.Ollama ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public int EmbeddingDimension => OllamaDimension;

    /// <inheritdoc />
    public string ProviderName => "Ollama";

    /// <inheritdoc />
    public async Task<EmbeddingResult> GenerateEmbeddingAsync(
        string text,
        string? inputType = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

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
            // Ollama /api/embed endpoint with single input
            var requestUrl = $"{_settings.Endpoint}/api/embed";
            var requestBody = JsonSerializer.Serialize(new
            {
                model = EmbeddingModel,
                input = text,
                truncate = true
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Ollama embed returned {StatusCode}: {Error}", response.StatusCode, errorBody);

                // Check if model needs to be pulled
                if (errorBody.Contains("not found", StringComparison.OrdinalIgnoreCase))
                {
                    return new EmbeddingResult
                    {
                        IsSuccess = false,
                        Provider = ProviderName,
                        ErrorMessage = $"Ollama model '{EmbeddingModel}' not found. Run: ollama pull {EmbeddingModel}",
                        ElapsedMilliseconds = sw.ElapsedMilliseconds
                    };
                }

                return new EmbeddingResult
                {
                    IsSuccess = false,
                    Provider = ProviderName,
                    ErrorMessage = $"Ollama API error: {response.StatusCode}",
                    ElapsedMilliseconds = sw.ElapsedMilliseconds
                };
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var parsed = ParseEmbedResponse(json);
            parsed.ElapsedMilliseconds = sw.ElapsedMilliseconds;

            _logger.LogInformation(
                "Ollama embedding generated in {ElapsedMs}ms. Dimension: {Dim}, Model: {Model}",
                parsed.ElapsedMilliseconds, parsed.Dimension, EmbeddingModel);

            return parsed;
        }
        catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException)
        {
            _logger.LogWarning("Ollama server not reachable at {Endpoint}. Is Ollama running?", _settings.Endpoint);
            return new EmbeddingResult
            {
                IsSuccess = false,
                Provider = ProviderName,
                ErrorMessage = $"Ollama server not reachable at {_settings.Endpoint}. Ensure Ollama is running.",
                ElapsedMilliseconds = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ollama embedding generation failed");
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
            // Ollama /api/embed supports batch input natively via array
            var requestUrl = $"{_settings.Endpoint}/api/embed";
            var requestBody = JsonSerializer.Serialize(new
            {
                model = EmbeddingModel,
                input = texts,
                truncate = true
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Ollama batch embed returned {StatusCode}: {Error}", response.StatusCode, errorBody);

                return new BatchEmbeddingResult
                {
                    IsSuccess = false,
                    Provider = ProviderName,
                    ErrorMessage = $"Ollama API error: {response.StatusCode}",
                    ElapsedMilliseconds = sw.ElapsedMilliseconds
                };
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var parsed = ParseBatchEmbedResponse(json);
            parsed.ElapsedMilliseconds = sw.ElapsedMilliseconds;

            _logger.LogInformation(
                "Ollama batch embedding completed in {ElapsedMs}ms. Count: {Count}, Dimension: {Dim}",
                parsed.ElapsedMilliseconds, parsed.Count, parsed.Dimension);

            return parsed;
        }
        catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException)
        {
            _logger.LogWarning("Ollama server not reachable at {Endpoint}. Is Ollama running?", _settings.Endpoint);
            return new BatchEmbeddingResult
            {
                IsSuccess = false,
                Provider = ProviderName,
                ErrorMessage = $"Ollama server not reachable at {_settings.Endpoint}. Ensure Ollama is running.",
                ElapsedMilliseconds = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ollama batch embedding generation failed");
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
    /// Parses the Ollama /api/embed single-embedding response.
    /// Response format:
    /// {
    ///   "model": "mxbai-embed-large",
    ///   "embeddings": [[0.1, 0.2, ...]]
    /// }
    /// </summary>
    private EmbeddingResult ParseEmbedResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var embeddingsArray = root.GetProperty("embeddings");
        if (embeddingsArray.GetArrayLength() == 0)
        {
            return new EmbeddingResult
            {
                IsSuccess = false,
                Provider = ProviderName,
                ErrorMessage = "Ollama returned empty embeddings array."
            };
        }

        var embedding = ParseFloatArray(embeddingsArray[0]);

        return new EmbeddingResult
        {
            IsSuccess = true,
            Embedding = embedding,
            Provider = ProviderName,
            TokensUsed = 0 // Ollama does not report token usage for embeddings
        };
    }

    /// <summary>
    /// Parses the Ollama /api/embed batch response.
    /// Response format:
    /// {
    ///   "model": "mxbai-embed-large",
    ///   "embeddings": [[0.1, 0.2, ...], [0.3, 0.4, ...]]
    /// }
    /// </summary>
    private BatchEmbeddingResult ParseBatchEmbedResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var embeddingsArray = root.GetProperty("embeddings");
        var embeddings = new float[embeddingsArray.GetArrayLength()][];

        var index = 0;
        foreach (var item in embeddingsArray.EnumerateArray())
        {
            embeddings[index++] = ParseFloatArray(item);
        }

        return new BatchEmbeddingResult
        {
            IsSuccess = true,
            Embeddings = embeddings,
            Provider = ProviderName,
            TotalTokensUsed = 0 // Ollama does not report token usage for embeddings
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
