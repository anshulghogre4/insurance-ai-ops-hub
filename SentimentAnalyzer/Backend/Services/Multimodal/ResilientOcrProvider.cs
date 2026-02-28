namespace SentimentAnalyzer.API.Services.Multimodal;

/// <summary>
/// Resilient OCR provider with automatic 4-tier fallback chain ordered by DATA SAFETY:
/// PdfPig (local) → Azure Document Intelligence → OCR Space → Gemini Vision.
/// Follows the Chain of Responsibility pattern (consistent with ResilientEmbeddingProvider and IResilientKernelProvider).
///
/// Data privacy ranking (safest first):
/// Tier 1: PdfPig — 100% local, zero data transfer, open-source
/// Tier 2: Azure Document Intelligence — Microsoft does NOT train on customer data (any tier), 24h auto-delete
/// Tier 3: OCR Space — Immediate document deletion, no stated training, GDPR compliant
/// Tier 4: Gemini Vision — WARNING: Google free tier trains on data, human reviewers may read input/output
///
/// Responsibilities:
/// 1. Always try PdfPig first (local, instant, zero API calls — no cooldown needed).
/// 2. Automatic fallback through cloud OCR providers when native PDF text is insufficient.
/// 3. Per-provider exponential backoff cooldown for Azure and OCR Space on repeated failures.
/// 4. Gemini Vision as last resort (always attempted, no cooldown) — least safe for PII.
/// 5. PII redaction is NOT done here — each individual provider handles it.
///
/// Insurance use case: ensures document text extraction always succeeds for claims processing,
/// while prioritizing providers that do NOT train on policyholder data.
/// </summary>
public class ResilientOcrProvider : IDocumentOcrService
{
    private readonly IDocumentOcrService _pdfPigService;
    private readonly IDocumentOcrService _azureService;
    private readonly IDocumentOcrService _geminiService;
    private readonly IDocumentOcrService _ocrSpaceService;
    private readonly ILogger<ResilientOcrProvider> _logger;

    private readonly object _lock = new();

    private DateTime? _azureCooldownExpiresUtc;
    private int _azureConsecutiveFailures;
    private DateTime? _ocrSpaceCooldownExpiresUtc;
    private int _ocrSpaceConsecutiveFailures;

    private static readonly TimeSpan _baseCooldown = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _maxCooldown = TimeSpan.FromSeconds(300);

    /// <summary>
    /// Initializes the resilient OCR provider with 4-tier fallback chain.
    /// </summary>
    /// <param name="pdfPigService">PdfPig native text extractor (Tier 1, local).</param>
    /// <param name="azureService">Azure Document Intelligence OCR (Tier 2, cloud).</param>
    /// <param name="ocrSpaceService">OCR Space OCR (Tier 3, cloud, GDPR compliant, no training).</param>
    /// <param name="geminiService">Gemini Vision OCR (Tier 4, cloud, last resort — free tier trains on data).</param>
    /// <param name="logger">Logger instance.</param>
    public ResilientOcrProvider(
        [Microsoft.Extensions.DependencyInjection.FromKeyedServices("PdfPig")] IDocumentOcrService pdfPigService,
        [Microsoft.Extensions.DependencyInjection.FromKeyedServices("AzureDocIntel")] IDocumentOcrService azureService,
        [Microsoft.Extensions.DependencyInjection.FromKeyedServices("GeminiOcr")] IDocumentOcrService geminiService,
        [Microsoft.Extensions.DependencyInjection.FromKeyedServices("OcrSpace")] IDocumentOcrService ocrSpaceService,
        ILogger<ResilientOcrProvider> logger)
    {
        _pdfPigService = pdfPigService ?? throw new ArgumentNullException(nameof(pdfPigService));
        _azureService = azureService ?? throw new ArgumentNullException(nameof(azureService));
        _geminiService = geminiService ?? throw new ArgumentNullException(nameof(geminiService));
        _ocrSpaceService = ocrSpaceService ?? throw new ArgumentNullException(nameof(ocrSpaceService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<OcrResult> ExtractTextAsync(
        byte[] documentData,
        string mimeType = "application/pdf",
        CancellationToken cancellationToken = default)
    {
        // Tier 1: PdfPig (always attempted, no cooldown — local, instant, zero API calls)
        var pdfPigResult = await _pdfPigService.ExtractTextAsync(documentData, mimeType, cancellationToken);
        if (pdfPigResult.IsSuccess)
        {
            _logger.LogInformation("OCR completed via PdfPig (Tier 1) — native PDF text extraction");
            return pdfPigResult;
        }

        _logger.LogInformation(
            "Native PDF text extraction insufficient ({Error}), trying OCR providers",
            pdfPigResult.ErrorMessage ?? "unknown");

        // Tier 2: Azure Document Intelligence (if not in cooldown)
        if (!IsInCooldown(ref _azureCooldownExpiresUtc, "AzureDocIntel"))
        {
            var azureResult = await _azureService.ExtractTextAsync(documentData, mimeType, cancellationToken);
            if (azureResult.IsSuccess)
            {
                ResetFailures(ref _azureConsecutiveFailures, ref _azureCooldownExpiresUtc, "AzureDocIntel");
                _logger.LogInformation("OCR completed via Azure Document Intelligence (Tier 2)");
                return azureResult;
            }

            ReportFailure(ref _azureConsecutiveFailures, ref _azureCooldownExpiresUtc, "AzureDocIntel", azureResult.ErrorMessage);
        }

        // Tier 3: OCR Space (if not in cooldown) — safer than Gemini (no data training, GDPR compliant)
        if (!IsInCooldown(ref _ocrSpaceCooldownExpiresUtc, "OcrSpace"))
        {
            var ocrSpaceResult = await _ocrSpaceService.ExtractTextAsync(documentData, mimeType, cancellationToken);
            if (ocrSpaceResult.IsSuccess)
            {
                ResetFailures(ref _ocrSpaceConsecutiveFailures, ref _ocrSpaceCooldownExpiresUtc, "OcrSpace");
                _logger.LogInformation("OCR completed via OCR Space (Tier 3)");
                return ocrSpaceResult;
            }

            ReportFailure(ref _ocrSpaceConsecutiveFailures, ref _ocrSpaceCooldownExpiresUtc, "OcrSpace", ocrSpaceResult.ErrorMessage);
        }

        // Tier 4: Gemini Vision (always attempted, last resort — no cooldown)
        // WARNING: Google free tier may train on submitted data. PII is redacted by GeminiOcrService before sending.
        _logger.LogWarning("Falling back to Gemini Vision (Tier 4, last resort) for document OCR. " +
            "Note: Google free tier may use data for model improvement.");
        var geminiResult = await _geminiService.ExtractTextAsync(documentData, mimeType, cancellationToken);

        if (geminiResult.IsSuccess)
        {
            _logger.LogInformation("OCR completed via Gemini Vision (Tier 4)");
        }
        else
        {
            _logger.LogError("All 4 OCR tiers failed. PdfPig: {PdfPigError}, Gemini: {GeminiError}",
                pdfPigResult.ErrorMessage, geminiResult.ErrorMessage);
        }

        return geminiResult;
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
                "{Provider} OCR failed (attempt #{Failures}). Cooldown: {Cooldown}s. Error: {Error}",
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
