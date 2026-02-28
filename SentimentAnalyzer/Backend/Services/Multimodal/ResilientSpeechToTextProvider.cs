namespace SentimentAnalyzer.API.Services.Multimodal;

/// <summary>
/// Resilient speech-to-text provider with automatic 2-tier fallback chain:
/// Deepgram ($200 credit, primary) → Azure Speech (5 hrs/month, fallback).
/// Follows the Chain of Responsibility pattern (consistent with ResilientOcrProvider and ResilientEmbeddingProvider).
///
/// Data privacy ranking (safest first):
/// Tier 1: Deepgram — mip_opt_out=true prevents model training on audio data, immediate deletion
/// Tier 2: Azure Speech — Microsoft does NOT train on customer data (any tier), 30-day auto-delete
///
/// Responsibilities:
/// 1. Always try Deepgram first (primary provider with $200 credit).
/// 2. Automatic fallback to Azure Speech when Deepgram fails or is unavailable.
/// 3. Per-provider exponential backoff cooldown for Azure Speech on repeated failures.
/// 4. PII redaction is NOT done here — each individual provider handles it.
///
/// Insurance use case: ensures voice transcription always succeeds for field adjuster notes,
/// policyholder call recordings, and claims hotline audio.
/// </summary>
public class ResilientSpeechToTextProvider : ISpeechToTextService
{
    private readonly ISpeechToTextService _deepgramService;
    private readonly ISpeechToTextService _azureSpeechService;
    private readonly ILogger<ResilientSpeechToTextProvider> _logger;

    private readonly object _lock = new();
    private DateTime? _azureCooldownExpiresUtc;
    private int _azureConsecutiveFailures;

    private static readonly TimeSpan _baseCooldown = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _maxCooldown = TimeSpan.FromSeconds(300);

    /// <summary>
    /// Initializes the resilient speech-to-text provider with 2-tier fallback chain.
    /// </summary>
    /// <param name="deepgramService">Deepgram speech-to-text service (Tier 1, primary).</param>
    /// <param name="azureSpeechService">Azure Speech service (Tier 2, fallback).</param>
    /// <param name="logger">Logger instance.</param>
    public ResilientSpeechToTextProvider(
        [Microsoft.Extensions.DependencyInjection.FromKeyedServices("Deepgram")] ISpeechToTextService deepgramService,
        [Microsoft.Extensions.DependencyInjection.FromKeyedServices("AzureSpeech")] ISpeechToTextService azureSpeechService,
        ILogger<ResilientSpeechToTextProvider> logger)
    {
        _deepgramService = deepgramService ?? throw new ArgumentNullException(nameof(deepgramService));
        _azureSpeechService = azureSpeechService ?? throw new ArgumentNullException(nameof(azureSpeechService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<TranscriptionResult> TranscribeAsync(
        byte[] audioData,
        string mimeType = "audio/wav",
        CancellationToken cancellationToken = default)
    {
        // Tier 1: Deepgram (always attempted — primary provider with $200 credit)
        var deepgramResult = await _deepgramService.TranscribeAsync(audioData, mimeType, cancellationToken);
        if (deepgramResult.IsSuccess)
        {
            _logger.LogInformation("Transcription completed via Deepgram (Tier 1) — primary STT provider");
            return deepgramResult;
        }

        _logger.LogInformation(
            "Deepgram transcription failed ({Error}), trying Azure Speech fallback",
            deepgramResult.ErrorMessage ?? "unknown");

        // Tier 2: Azure Speech (if not in cooldown)
        if (!IsInCooldown(ref _azureCooldownExpiresUtc, "AzureSpeech"))
        {
            var azureResult = await _azureSpeechService.TranscribeAsync(audioData, mimeType, cancellationToken);
            if (azureResult.IsSuccess)
            {
                ResetFailures(ref _azureConsecutiveFailures, ref _azureCooldownExpiresUtc, "AzureSpeech");
                _logger.LogInformation("Transcription completed via Azure Speech (Tier 2)");
                return azureResult;
            }

            ReportFailure(ref _azureConsecutiveFailures, ref _azureCooldownExpiresUtc, "AzureSpeech", azureResult.ErrorMessage);

            _logger.LogError(
                "All speech-to-text providers failed. Deepgram: {DeepgramError}, Azure: {AzureError}",
                deepgramResult.ErrorMessage, azureResult.ErrorMessage);

            return azureResult;
        }

        // Azure is in cooldown — return Deepgram's error as the last known failure
        _logger.LogError(
            "All speech-to-text providers unavailable. Deepgram failed, Azure Speech in cooldown. Error: {Error}",
            deepgramResult.ErrorMessage);

        return deepgramResult;
    }

    /// <summary>
    /// Checks whether Azure Speech is currently in cooldown. Thread-safe.
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
                "{Provider} STT failed (attempt #{Failures}). Cooldown: {Cooldown}s. Error: {Error}",
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
