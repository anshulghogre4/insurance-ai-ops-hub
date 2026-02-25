using SentimentAnalyzer.Agents.Orchestration;

namespace SentimentAnalyzer.API.Services.Embeddings;

/// <summary>
/// Resilient embedding provider with automatic fallback: Voyage AI -> Ollama.
/// Follows the Chain of Responsibility pattern (consistent with IResilientKernelProvider for LLMs).
///
/// Responsibilities:
/// 1. PII redaction before sending text to ANY provider (mandatory per insurance domain rules).
/// 2. Automatic fallback when Voyage AI fails (rate limit, network error, API key missing).
/// 3. Dimension mismatch logging when fallback produces different-dimension embeddings.
/// 4. Exponential backoff cooldown for Voyage AI on repeated failures.
///
/// Insurance use case: ensures RAG document indexing always succeeds, even when the
/// Voyage AI free tier (50M tokens) is exhausted, by falling back to local Ollama.
/// </summary>
public class ResilientEmbeddingProvider : IEmbeddingService
{
    private readonly IEmbeddingService _voyageService;
    private readonly IEmbeddingService _ollamaService;
    private readonly IPIIRedactor _piiRedactor;
    private readonly ILogger<ResilientEmbeddingProvider> _logger;

    private readonly object _lock = new();
    private DateTime? _voyageCooldownExpiresUtc;
    private int _voyageConsecutiveFailures;

    private static readonly TimeSpan _baseCooldown = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _maxCooldown = TimeSpan.FromSeconds(300);

    /// <summary>
    /// Initializes the resilient embedding provider with Voyage AI (primary) and Ollama (fallback).
    /// </summary>
    /// <param name="voyageService">Voyage AI embedding service (keyed as "VoyageAI").</param>
    /// <param name="ollamaService">Ollama embedding service (keyed as "Ollama").</param>
    /// <param name="piiRedactor">PII redaction service (mandatory before external API calls).</param>
    /// <param name="logger">Logger instance.</param>
    public ResilientEmbeddingProvider(
        [Microsoft.Extensions.DependencyInjection.FromKeyedServices("VoyageAI")] IEmbeddingService voyageService,
        [Microsoft.Extensions.DependencyInjection.FromKeyedServices("Ollama")] IEmbeddingService ollamaService,
        IPIIRedactor piiRedactor,
        ILogger<ResilientEmbeddingProvider> logger)
    {
        _voyageService = voyageService ?? throw new ArgumentNullException(nameof(voyageService));
        _ollamaService = ollamaService ?? throw new ArgumentNullException(nameof(ollamaService));
        _piiRedactor = piiRedactor ?? throw new ArgumentNullException(nameof(piiRedactor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    /// <remarks>
    /// Returns the dimension of the currently active provider.
    /// When Voyage AI is active: 1024. When Ollama fallback: 1024 (mxbai-embed-large).
    /// </remarks>
    public int EmbeddingDimension => ActiveProvider.EmbeddingDimension;

    /// <inheritdoc />
    public string ProviderName => $"Resilient({ActiveProvider.ProviderName})";

    /// <summary>
    /// Returns the currently active (non-cooldown) embedding service.
    /// </summary>
    private IEmbeddingService ActiveProvider
    {
        get
        {
            lock (_lock)
            {
                if (_voyageCooldownExpiresUtc.HasValue && DateTime.UtcNow >= _voyageCooldownExpiresUtc.Value)
                {
                    _voyageCooldownExpiresUtc = null;
                    _logger.LogInformation("Voyage AI cooldown expired, marking as available");
                }

                return _voyageCooldownExpiresUtc.HasValue ? _ollamaService : _voyageService;
            }
        }
    }

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

        // Try primary provider (Voyage AI)
        if (!IsVoyageInCooldown())
        {
            var result = await _voyageService.GenerateEmbeddingAsync(redactedText, inputType, cancellationToken);

            if (result.IsSuccess)
            {
                ResetVoyageFailures();
                return result;
            }

            // Voyage AI failed — apply cooldown and fall through to Ollama
            ReportVoyageFailure(result.ErrorMessage);
        }

        // Fallback to Ollama
        _logger.LogWarning("Falling back to Ollama for embedding generation");
        var fallbackResult = await _ollamaService.GenerateEmbeddingAsync(redactedText, inputType, cancellationToken);

        if (fallbackResult.IsSuccess)
        {
            LogDimensionMismatchWarning(fallbackResult.Dimension);
        }

        return fallbackResult;
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

        // Try primary provider (Voyage AI)
        if (!IsVoyageInCooldown())
        {
            var result = await _voyageService.GenerateBatchEmbeddingsAsync(redactedTexts, inputType, cancellationToken);

            if (result.IsSuccess)
            {
                ResetVoyageFailures();
                return result;
            }

            // Voyage AI failed — apply cooldown and fall through to Ollama
            ReportVoyageFailure(result.ErrorMessage);
        }

        // Fallback to Ollama
        _logger.LogWarning("Falling back to Ollama for batch embedding generation ({Count} texts)", texts.Length);
        var fallbackResult = await _ollamaService.GenerateBatchEmbeddingsAsync(redactedTexts, inputType, cancellationToken);

        if (fallbackResult.IsSuccess && fallbackResult.Count > 0)
        {
            LogDimensionMismatchWarning(fallbackResult.Dimension);
        }

        return fallbackResult;
    }

    /// <summary>
    /// Checks whether Voyage AI is currently in cooldown.
    /// </summary>
    private bool IsVoyageInCooldown()
    {
        lock (_lock)
        {
            if (!_voyageCooldownExpiresUtc.HasValue)
                return false;

            if (DateTime.UtcNow >= _voyageCooldownExpiresUtc.Value)
            {
                _voyageCooldownExpiresUtc = null;
                _logger.LogInformation("Voyage AI cooldown expired, marking as available");
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Reports a Voyage AI failure and applies exponential backoff cooldown.
    /// Cooldown schedule: 30s, 60s, 120s, 240s, capped at 300s.
    /// </summary>
    private void ReportVoyageFailure(string? errorMessage)
    {
        lock (_lock)
        {
            _voyageConsecutiveFailures++;

            var backoffMultiplier = Math.Min(
                Math.Pow(2, _voyageConsecutiveFailures - 1),
                _maxCooldown.TotalSeconds / _baseCooldown.TotalSeconds);

            var cooldownSeconds = Math.Min(
                _baseCooldown.TotalSeconds * backoffMultiplier,
                _maxCooldown.TotalSeconds);

            _voyageCooldownExpiresUtc = DateTime.UtcNow.AddSeconds(cooldownSeconds);

            _logger.LogWarning(
                "Voyage AI embedding failed (attempt #{Failures}). Cooldown: {Cooldown}s. Error: {Error}",
                _voyageConsecutiveFailures, cooldownSeconds, errorMessage ?? "Unknown");
        }
    }

    /// <summary>
    /// Resets the Voyage AI failure counter after a successful request.
    /// </summary>
    private void ResetVoyageFailures()
    {
        lock (_lock)
        {
            if (_voyageConsecutiveFailures > 0)
            {
                _logger.LogInformation("Voyage AI recovered after {Failures} consecutive failures",
                    _voyageConsecutiveFailures);
            }
            _voyageConsecutiveFailures = 0;
            _voyageCooldownExpiresUtc = null;
        }
    }

    /// <summary>
    /// Logs a warning if the fallback provider returns embeddings with a different
    /// dimension than the primary provider. This is critical for RAG because existing
    /// indexed embeddings may have a different dimension.
    /// </summary>
    private void LogDimensionMismatchWarning(int actualDimension)
    {
        var expectedDimension = _voyageService.EmbeddingDimension;
        if (actualDimension != expectedDimension)
        {
            _logger.LogWarning(
                "DIMENSION MISMATCH: Voyage AI produces {Expected}-dim embeddings, " +
                "but Ollama fallback returned {Actual}-dim. Cosine similarity will truncate to min dimension. " +
                "Rebuild the vector index when switching back to Voyage AI.",
                expectedDimension, actualDimension);
        }
    }
}
