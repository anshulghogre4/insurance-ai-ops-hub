namespace SentimentAnalyzer.API.Services.Multimodal;

/// <summary>
/// Resilient entity extraction provider with automatic 2-tier fallback chain:
/// HuggingFace (primary, 300 req/hr) → Azure Language (fallback, 5K records/month).
/// Follows the Chain of Responsibility pattern (consistent with ResilientOcrProvider and IResilientKernelProvider).
///
/// Data privacy ranking (safest first):
/// Tier 1: HuggingFace — Rate-limited free tier, processes text for NER only
/// Tier 2: Azure Language — Microsoft does NOT train on customer data (any tier), 24h auto-delete
///
/// Responsibilities:
/// 1. Always try HuggingFace first (primary provider, no cooldown).
/// 2. Automatic fallback to Azure Language when HuggingFace fails and not in cooldown.
/// 3. Per-provider exponential backoff cooldown for Azure Language on repeated failures.
/// 4. Thread-safe cooldown state management.
/// 5. PII redaction is NOT done here — NER requires unredacted text to detect entities.
///
/// Insurance use case: ensures entity extraction always succeeds for claims processing,
/// supporting policyholder name, organization, location, date, and insurance-specific
/// identifier extraction across provider outages.
/// </summary>
public class ResilientEntityExtractionProvider : IEntityExtractionService
{
    private readonly IEntityExtractionService _huggingFaceService;
    private readonly IEntityExtractionService _azureLanguageService;
    private readonly ILogger<ResilientEntityExtractionProvider> _logger;

    private readonly object _lock = new();
    private DateTime? _azureCooldownExpiresUtc;
    private int _azureConsecutiveFailures;

    private static readonly TimeSpan _baseCooldown = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _maxCooldown = TimeSpan.FromSeconds(300);

    /// <summary>
    /// Initializes the resilient entity extraction provider with 2-tier fallback chain.
    /// </summary>
    /// <param name="huggingFaceService">HuggingFace NER service (Tier 1, primary).</param>
    /// <param name="azureLanguageService">Azure Language NER service (Tier 2, fallback).</param>
    /// <param name="logger">Logger instance for diagnostics.</param>
    public ResilientEntityExtractionProvider(
        [Microsoft.Extensions.DependencyInjection.FromKeyedServices("HuggingFace")] IEntityExtractionService huggingFaceService,
        [Microsoft.Extensions.DependencyInjection.FromKeyedServices("AzureLanguage")] IEntityExtractionService azureLanguageService,
        ILogger<ResilientEntityExtractionProvider> logger)
    {
        _huggingFaceService = huggingFaceService ?? throw new ArgumentNullException(nameof(huggingFaceService));
        _azureLanguageService = azureLanguageService ?? throw new ArgumentNullException(nameof(azureLanguageService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<EntityExtractionResult> ExtractEntitiesAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        // Tier 1: HuggingFace (always attempted, primary provider — no cooldown)
        var huggingFaceResult = await _huggingFaceService.ExtractEntitiesAsync(text, cancellationToken);
        if (huggingFaceResult.IsSuccess)
        {
            _logger.LogInformation("Entity extraction completed via HuggingFace (Tier 1, primary)");
            return huggingFaceResult;
        }

        _logger.LogInformation(
            "HuggingFace NER failed ({Error}), attempting Azure Language fallback",
            huggingFaceResult.ErrorMessage ?? "unknown");

        // Tier 2: Azure Language (if not in cooldown)
        if (!IsInCooldown(ref _azureCooldownExpiresUtc, "AzureLanguage"))
        {
            var azureResult = await _azureLanguageService.ExtractEntitiesAsync(text, cancellationToken);
            if (azureResult.IsSuccess)
            {
                ResetFailures(ref _azureConsecutiveFailures, ref _azureCooldownExpiresUtc, "AzureLanguage");
                _logger.LogInformation("Entity extraction completed via Azure Language (Tier 2, fallback)");
                return azureResult;
            }

            ReportFailure(ref _azureConsecutiveFailures, ref _azureCooldownExpiresUtc, "AzureLanguage", azureResult.ErrorMessage);
        }

        // Both providers failed — return the HuggingFace error (primary provider's error is most relevant)
        _logger.LogError(
            "All entity extraction providers failed. HuggingFace: {HuggingFaceError}",
            huggingFaceResult.ErrorMessage);

        return huggingFaceResult;
    }

    /// <summary>
    /// Checks whether a provider is currently in cooldown. Thread-safe.
    /// </summary>
    /// <param name="cooldownExpires">Reference to the provider's cooldown expiration timestamp.</param>
    /// <param name="providerName">Provider name for logging.</param>
    /// <returns>True if the provider is in cooldown and should be skipped.</returns>
    private bool IsInCooldown(ref DateTime? cooldownExpires, string providerName)
    {
        lock (_lock)
        {
            if (!cooldownExpires.HasValue)
                return false;

            if (DateTime.UtcNow >= cooldownExpires.Value)
            {
                cooldownExpires = null;
                _logger.LogInformation("{Provider} cooldown expired, marking as available", providerName);
                return false;
            }

            _logger.LogInformation("{Provider} is in cooldown until {Expiry:u}, skipping",
                providerName, cooldownExpires.Value);
            return true;
        }
    }

    /// <summary>
    /// Reports a provider failure and applies exponential backoff cooldown.
    /// Cooldown schedule: 30s, 60s, 120s, 240s, capped at 300s.
    /// </summary>
    /// <param name="consecutiveFailures">Reference to the provider's consecutive failure counter.</param>
    /// <param name="cooldownExpires">Reference to the provider's cooldown expiration timestamp.</param>
    /// <param name="providerName">Provider name for logging.</param>
    /// <param name="errorMessage">Error message from the failed attempt.</param>
    private void ReportFailure(ref int consecutiveFailures, ref DateTime? cooldownExpires, string providerName, string? errorMessage)
    {
        lock (_lock)
        {
            consecutiveFailures++;

            var backoffMultiplier = Math.Min(
                Math.Pow(2, consecutiveFailures - 1),
                _maxCooldown.TotalSeconds / _baseCooldown.TotalSeconds);

            var cooldownSeconds = Math.Min(
                _baseCooldown.TotalSeconds * backoffMultiplier,
                _maxCooldown.TotalSeconds);

            cooldownExpires = DateTime.UtcNow.AddSeconds(cooldownSeconds);

            _logger.LogWarning(
                "{Provider} NER failed (attempt #{Failures}). Cooldown: {Cooldown}s. Error: {Error}",
                providerName, consecutiveFailures, cooldownSeconds, errorMessage ?? "Unknown");
        }
    }

    /// <summary>
    /// Resets a provider's failure counter after a successful request.
    /// </summary>
    /// <param name="consecutiveFailures">Reference to the provider's consecutive failure counter.</param>
    /// <param name="cooldownExpires">Reference to the provider's cooldown expiration timestamp.</param>
    /// <param name="providerName">Provider name for logging.</param>
    private void ResetFailures(ref int consecutiveFailures, ref DateTime? cooldownExpires, string providerName)
    {
        lock (_lock)
        {
            if (consecutiveFailures > 0)
            {
                _logger.LogInformation("{Provider} recovered after {Failures} consecutive failures",
                    providerName, consecutiveFailures);
            }
            consecutiveFailures = 0;
            cooldownExpires = null;
        }
    }
}
