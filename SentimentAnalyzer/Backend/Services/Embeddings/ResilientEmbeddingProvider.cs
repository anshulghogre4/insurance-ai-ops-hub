using SentimentAnalyzer.Agents.Orchestration;

namespace SentimentAnalyzer.API.Services.Embeddings;

/// <summary>
/// Resilient embedding provider with automatic 6-provider fallback chain:
/// Voyage AI -> Jina v3 -> Cohere Embed v3 -> Gemini -> HuggingFace BGE-large -> Ollama.
/// Follows the Chain of Responsibility pattern (consistent with IResilientKernelProvider for LLMs).
///
/// Responsibilities:
/// 1. PII redaction before sending text to ANY provider (mandatory per insurance domain rules).
/// 2. Automatic fallback when a provider fails (rate limit, network error, API key missing).
/// 3. Per-provider exponential backoff cooldown (30s->60s->120s->240s->300s cap).
/// 4. Dimension mismatch logging when fallback produces different-dimension embeddings.
///
/// Insurance use case: ensures RAG document indexing always succeeds by cascading through
/// 6 embedding providers, from finance-optimized (Voyage AI) to local fallback (Ollama).
/// Most providers produce 1024-dim embeddings; Gemini produces 768-dim (dimension mismatch logged).
/// </summary>
public class ResilientEmbeddingProvider : IEmbeddingService
{
    private readonly (string Name, IEmbeddingService Service)[] _providers;
    private readonly IPIIRedactor _piiRedactor;
    private readonly ILogger<ResilientEmbeddingProvider> _logger;

    private readonly object _lock = new();
    private readonly Dictionary<string, DateTime?> _cooldownExpiry = new();
    private readonly Dictionary<string, int> _consecutiveFailures = new();

    private static readonly TimeSpan _baseCooldown = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _maxCooldown = TimeSpan.FromSeconds(300);

    /// <summary>
    /// Tracks the name of the last provider that successfully generated embeddings.
    /// Access must be under <see cref="_lock"/> for thread safety.
    /// </summary>
    private volatile string _lastSuccessfulProvider = "VoyageAI";

    /// <summary>
    /// Initializes the resilient embedding provider with 6 providers in fallback order:
    /// Voyage AI -> Jina -> Cohere -> Gemini -> HuggingFace BGE -> Ollama.
    /// </summary>
    /// <param name="voyageService">Voyage AI embedding service (keyed as "VoyageAI").</param>
    /// <param name="jinaService">Jina AI embedding service (keyed as "Jina").</param>
    /// <param name="cohereService">Cohere embedding service (keyed as "Cohere").</param>
    /// <param name="geminiService">Gemini embedding service (keyed as "GeminiEmbed").</param>
    /// <param name="huggingFaceService">HuggingFace BGE embedding service (keyed as "HuggingFaceEmbed").</param>
    /// <param name="ollamaService">Ollama embedding service (keyed as "Ollama").</param>
    /// <param name="piiRedactor">PII redaction service (mandatory before external API calls).</param>
    /// <param name="logger">Logger instance.</param>
    public ResilientEmbeddingProvider(
        [Microsoft.Extensions.DependencyInjection.FromKeyedServices("VoyageAI")] IEmbeddingService voyageService,
        [Microsoft.Extensions.DependencyInjection.FromKeyedServices("Jina")] IEmbeddingService jinaService,
        [Microsoft.Extensions.DependencyInjection.FromKeyedServices("Cohere")] IEmbeddingService cohereService,
        [Microsoft.Extensions.DependencyInjection.FromKeyedServices("GeminiEmbed")] IEmbeddingService geminiService,
        [Microsoft.Extensions.DependencyInjection.FromKeyedServices("HuggingFaceEmbed")] IEmbeddingService huggingFaceService,
        [Microsoft.Extensions.DependencyInjection.FromKeyedServices("Ollama")] IEmbeddingService ollamaService,
        IPIIRedactor piiRedactor,
        ILogger<ResilientEmbeddingProvider> logger)
    {
        _providers =
        [
            ("VoyageAI", voyageService ?? throw new ArgumentNullException(nameof(voyageService))),
            ("Jina", jinaService ?? throw new ArgumentNullException(nameof(jinaService))),
            ("Cohere", cohereService ?? throw new ArgumentNullException(nameof(cohereService))),
            ("GeminiEmbed", geminiService ?? throw new ArgumentNullException(nameof(geminiService))),
            ("HuggingFaceEmbed", huggingFaceService ?? throw new ArgumentNullException(nameof(huggingFaceService))),
            ("Ollama", ollamaService ?? throw new ArgumentNullException(nameof(ollamaService)))
        ];

        _piiRedactor = piiRedactor ?? throw new ArgumentNullException(nameof(piiRedactor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Initialize per-provider tracking
        foreach (var (name, _) in _providers)
        {
            _cooldownExpiry[name] = null;
            _consecutiveFailures[name] = 0;
        }

        _logger.LogInformation(
            "Resilient embedding provider initialized with {Count}-provider chain: {Chain}",
            _providers.Length,
            string.Join(" -> ", _providers.Select(p => p.Name)));
    }

    /// <inheritdoc />
    /// <remarks>
    /// Returns the dimension of the currently active (first non-cooldown) provider.
    /// All providers in the chain target 1024-dim for index compatibility.
    /// </remarks>
    public int EmbeddingDimension => GetFirstAvailableProvider()?.Service.EmbeddingDimension ?? 1024;

    /// <inheritdoc />
    public string ProviderName => $"Resilient({_lastSuccessfulProvider})";

    /// <inheritdoc />
    public async Task<EmbeddingResult> GenerateEmbeddingAsync(
        string text,
        string? inputType = null,
        CancellationToken cancellationToken = default)
    {
        // PII redaction is mandatory before ANY embedding provider (even Ollama for consistency)
        var redactedText = _piiRedactor.Redact(text);

        if (redactedText != text)
        {
            _logger.LogInformation("PII redacted from embedding input before provider call");
        }

        // Iterate providers in fallback order, skip those in cooldown
        foreach (var (name, service) in _providers)
        {
            if (IsProviderInCooldown(name))
            {
                _logger.LogDebug("Skipping {Provider} (in cooldown)", name);
                continue;
            }

            var result = await service.GenerateEmbeddingAsync(redactedText, inputType, cancellationToken);

            if (result.IsSuccess)
            {
                ResetProviderFailures(name);
                _lastSuccessfulProvider = name;

                // Log dimension mismatch warning if fallback provider has different dimensions
                if (name != _providers[0].Name)
                {
                    LogDimensionMismatchWarning(name, result.Dimension);
                }

                return result;
            }

            // Provider failed — apply cooldown and try next
            ReportProviderFailure(name, result.ErrorMessage);
        }

        // All providers exhausted
        _logger.LogError("All {Count} embedding providers failed. No embeddings generated.", _providers.Length);
        return new EmbeddingResult
        {
            IsSuccess = false,
            Provider = "Resilient(AllFailed)",
            ErrorMessage = $"All {_providers.Length} embedding providers failed. Check provider API keys and connectivity."
        };
    }

    /// <inheritdoc />
    public async Task<BatchEmbeddingResult> GenerateBatchEmbeddingsAsync(
        string[] texts,
        string? inputType = null,
        CancellationToken cancellationToken = default)
    {
        // PII redaction is mandatory for all texts in the batch
        var redactedTexts = new string[texts.Length];
        var piiDetected = false;
        for (var i = 0; i < texts.Length; i++)
        {
            redactedTexts[i] = _piiRedactor.Redact(texts[i]);
            if (redactedTexts[i] != texts[i])
                piiDetected = true;
        }

        if (piiDetected)
        {
            _logger.LogInformation("PII redacted from {Count} batch embedding inputs before provider call", texts.Length);
        }

        // Iterate providers in fallback order, skip those in cooldown
        foreach (var (name, service) in _providers)
        {
            if (IsProviderInCooldown(name))
            {
                _logger.LogDebug("Skipping {Provider} for batch (in cooldown)", name);
                continue;
            }

            var result = await service.GenerateBatchEmbeddingsAsync(redactedTexts, inputType, cancellationToken);

            if (result.IsSuccess)
            {
                ResetProviderFailures(name);
                _lastSuccessfulProvider = name;

                // Log dimension mismatch warning if fallback provider has different dimensions
                if (name != _providers[0].Name && result.Count > 0)
                {
                    LogDimensionMismatchWarning(name, result.Dimension);
                }

                return result;
            }

            // Provider failed — apply cooldown and try next
            ReportProviderFailure(name, result.ErrorMessage);
        }

        // All providers exhausted
        _logger.LogError("All {Count} embedding providers failed for batch ({BatchSize} texts).",
            _providers.Length, texts.Length);
        return new BatchEmbeddingResult
        {
            IsSuccess = false,
            Provider = "Resilient(AllFailed)",
            ErrorMessage = $"All {_providers.Length} embedding providers failed. Check provider API keys and connectivity."
        };
    }

    /// <summary>
    /// Gets the first provider not currently in cooldown, or null if all are in cooldown.
    /// </summary>
    private (string Name, IEmbeddingService Service)? GetFirstAvailableProvider()
    {
        foreach (var provider in _providers)
        {
            if (!IsProviderInCooldown(provider.Name))
                return provider;
        }
        return _providers.Length > 0 ? _providers[^1] : null;
    }

    /// <summary>
    /// Checks whether a provider is currently in cooldown.
    /// Automatically clears expired cooldowns.
    /// </summary>
    private bool IsProviderInCooldown(string providerName)
    {
        lock (_lock)
        {
            if (!_cooldownExpiry.TryGetValue(providerName, out var expiry) || !expiry.HasValue)
                return false;

            if (DateTime.UtcNow >= expiry.Value)
            {
                _cooldownExpiry[providerName] = null;
                _logger.LogInformation("{Provider} cooldown expired, marking as available", providerName);
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Reports a provider failure and applies exponential backoff cooldown.
    /// Cooldown schedule: 30s, 60s, 120s, 240s, capped at 300s.
    /// </summary>
    private void ReportProviderFailure(string providerName, string? errorMessage)
    {
        lock (_lock)
        {
            _consecutiveFailures[providerName] = _consecutiveFailures.GetValueOrDefault(providerName) + 1;
            var failures = _consecutiveFailures[providerName];

            var backoffMultiplier = Math.Min(
                Math.Pow(2, failures - 1),
                _maxCooldown.TotalSeconds / _baseCooldown.TotalSeconds);

            var cooldownSeconds = Math.Min(
                _baseCooldown.TotalSeconds * backoffMultiplier,
                _maxCooldown.TotalSeconds);

            _cooldownExpiry[providerName] = DateTime.UtcNow.AddSeconds(cooldownSeconds);

            _logger.LogWarning(
                "{Provider} embedding failed (attempt #{Failures}). Cooldown: {Cooldown}s. Error: {Error}",
                providerName, failures, cooldownSeconds, errorMessage ?? "Unknown");
        }
    }

    /// <summary>
    /// Resets a provider's failure counter after a successful request.
    /// </summary>
    private void ResetProviderFailures(string providerName)
    {
        lock (_lock)
        {
            var previousFailures = _consecutiveFailures.GetValueOrDefault(providerName);
            if (previousFailures > 0)
            {
                _logger.LogInformation("{Provider} recovered after {Failures} consecutive failures",
                    providerName, previousFailures);
            }
            _consecutiveFailures[providerName] = 0;
            _cooldownExpiry[providerName] = null;
        }
    }

    /// <summary>
    /// Logs a warning if a fallback provider returns embeddings with a different
    /// dimension than the primary provider. This is critical for RAG because existing
    /// indexed embeddings may have a different dimension.
    /// </summary>
    private void LogDimensionMismatchWarning(string providerName, int actualDimension)
    {
        var expectedDimension = _providers[0].Service.EmbeddingDimension;
        if (actualDimension != expectedDimension)
        {
            _logger.LogWarning(
                "DIMENSION MISMATCH: Primary provider ({Primary}) produces {Expected}-dim embeddings, " +
                "but {Fallback} returned {Actual}-dim. Cosine similarity will truncate to min dimension. " +
                "Rebuild the vector index when switching back to the primary provider.",
                _providers[0].Name, expectedDimension, providerName, actualDimension);
        }
        else
        {
            _logger.LogInformation(
                "Fallback to {Provider} succeeded with matching {Dim}-dim embeddings",
                providerName, actualDimension);
        }
    }
}
