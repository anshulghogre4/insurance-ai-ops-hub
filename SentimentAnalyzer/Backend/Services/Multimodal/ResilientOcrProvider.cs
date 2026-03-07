namespace SentimentAnalyzer.API.Services.Multimodal;

/// <summary>
/// Resilient OCR provider with automatic 6-tier fallback chain ordered by DATA SAFETY:
/// PdfPig (local) → Tesseract (local) → Azure Document Intelligence → Mistral OCR → OCR Space → Gemini Vision.
/// Follows the Chain of Responsibility pattern (consistent with ResilientEmbeddingProvider and IResilientKernelProvider).
///
/// Data privacy ranking (safest first):
/// Tier 1:  PdfPig — 100% local, zero data transfer, open-source, digital/native PDFs only
/// Tier 1b: Tesseract — 100% local, zero data transfer, open-source, handles scanned docs PdfPig can't
/// Tier 2:  Azure Document Intelligence — Microsoft does NOT train on customer data (any tier), 24h auto-delete
/// Tier 2b: Mistral OCR — WARNING: Free tier may train on data. Best-in-class accuracy, 1000 pages/doc
/// Tier 3:  OCR Space — Immediate document deletion, no stated training, GDPR compliant
/// Tier 4:  Gemini Vision — WARNING: Google free tier trains on data, human reviewers may read input/output
///
/// Responsibilities:
/// 1. Always try PdfPig first (local, instant, zero API calls — no cooldown needed).
/// 2. Try Tesseract for scanned docs (local, no data transfer — no cooldown needed).
/// 3. Automatic fallback through cloud OCR providers when local providers fail.
/// 4. Per-provider exponential backoff cooldown for Azure, Mistral, and OCR Space on repeated failures.
/// 5. Gemini Vision as last resort (always attempted, no cooldown) — least safe for PII.
/// 6. PII redaction is NOT done here — each individual provider handles it.
///
/// Insurance use case: ensures document text extraction always succeeds for claims processing,
/// while prioritizing providers that do NOT train on policyholder data.
/// </summary>
public class ResilientOcrProvider : IDocumentOcrService
{
    private readonly IDocumentOcrService _pdfPigService;
    private readonly IDocumentOcrService _tesseractService;
    private readonly IDocumentOcrService _azureService;
    private readonly IDocumentOcrService _mistralOcrService;
    private readonly IDocumentOcrService _ocrSpaceService;
    private readonly IDocumentOcrService _geminiService;
    private readonly ILogger<ResilientOcrProvider> _logger;

    private readonly object _lock = new();

    private DateTime? _azureCooldownExpiresUtc;
    private int _azureConsecutiveFailures;
    private DateTime? _mistralOcrCooldownExpiresUtc;
    private int _mistralOcrConsecutiveFailures;
    private DateTime? _ocrSpaceCooldownExpiresUtc;
    private int _ocrSpaceConsecutiveFailures;

    private static readonly TimeSpan _baseCooldown = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _maxCooldown = TimeSpan.FromSeconds(300);

    /// <summary>
    /// Initializes the resilient OCR provider with 6-tier fallback chain.
    /// </summary>
    public ResilientOcrProvider(
        [Microsoft.Extensions.DependencyInjection.FromKeyedServices("PdfPig")] IDocumentOcrService pdfPigService,
        [Microsoft.Extensions.DependencyInjection.FromKeyedServices("Tesseract")] IDocumentOcrService tesseractService,
        [Microsoft.Extensions.DependencyInjection.FromKeyedServices("AzureDocIntel")] IDocumentOcrService azureService,
        [Microsoft.Extensions.DependencyInjection.FromKeyedServices("MistralOcr")] IDocumentOcrService mistralOcrService,
        [Microsoft.Extensions.DependencyInjection.FromKeyedServices("OcrSpace")] IDocumentOcrService ocrSpaceService,
        [Microsoft.Extensions.DependencyInjection.FromKeyedServices("GeminiOcr")] IDocumentOcrService geminiService,
        ILogger<ResilientOcrProvider> logger)
    {
        _pdfPigService = pdfPigService ?? throw new ArgumentNullException(nameof(pdfPigService));
        _tesseractService = tesseractService ?? throw new ArgumentNullException(nameof(tesseractService));
        _azureService = azureService ?? throw new ArgumentNullException(nameof(azureService));
        _mistralOcrService = mistralOcrService ?? throw new ArgumentNullException(nameof(mistralOcrService));
        _ocrSpaceService = ocrSpaceService ?? throw new ArgumentNullException(nameof(ocrSpaceService));
        _geminiService = geminiService ?? throw new ArgumentNullException(nameof(geminiService));
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

        // PdfPig failed but may have detected the actual page count (useful for detecting
        // partial extraction from Azure DocIntel F0's 2-page limit).
        var expectedPageCount = pdfPigResult.PageCount;

        _logger.LogInformation(
            "Native PDF text extraction insufficient ({Error}), detected {ExpectedPages} pages, trying OCR providers",
            pdfPigResult.ErrorMessage ?? "unknown", expectedPageCount);

        // Tier 1b: Tesseract (always attempted, no cooldown — 100% local, handles scanned docs)
        var tesseractResult = await _tesseractService.ExtractTextAsync(documentData, mimeType, cancellationToken);
        if (tesseractResult.IsSuccess)
        {
            _logger.LogInformation("OCR completed via Tesseract (Tier 1b) — local scanned document OCR");
            return tesseractResult;
        }

        _logger.LogInformation(
            "Tesseract OCR insufficient ({Error}), trying cloud providers",
            tesseractResult.ErrorMessage ?? "unknown");

        // Tier 2: Azure Document Intelligence (if not in cooldown)
        if (!IsInCooldown(ref _azureCooldownExpiresUtc, "AzureDocIntel"))
        {
            var azureResult = await _azureService.ExtractTextAsync(documentData, mimeType, cancellationToken);
            if (azureResult.IsSuccess)
            {
                // Detect partial page extraction: Azure F0 caps at 2 pages per document.
                // With batching, this should now extract all pages, but verify.
                if (expectedPageCount > 0 && azureResult.PageCount < expectedPageCount)
                {
                    _logger.LogWarning(
                        "Azure DocIntel only processed {Actual} of {Expected} pages. " +
                        "Falling through to next OCR provider for full extraction.",
                        azureResult.PageCount, expectedPageCount);
                    ReportFailure(ref _azureConsecutiveFailures, ref _azureCooldownExpiresUtc, "AzureDocIntel",
                        $"Partial extraction: {azureResult.PageCount}/{expectedPageCount} pages");
                }
                else
                {
                    ResetFailures(ref _azureConsecutiveFailures, ref _azureCooldownExpiresUtc, "AzureDocIntel");
                    _logger.LogInformation("OCR completed via Azure Document Intelligence (Tier 2)");
                    return azureResult;
                }
            }
            else
            {
                ReportFailure(ref _azureConsecutiveFailures, ref _azureCooldownExpiresUtc, "AzureDocIntel", azureResult.ErrorMessage);
            }
        }

        // Tier 2b: Mistral OCR (if not in cooldown) — high accuracy, but free tier trains on data
        if (!IsInCooldown(ref _mistralOcrCooldownExpiresUtc, "MistralOCR"))
        {
            var mistralResult = await _mistralOcrService.ExtractTextAsync(documentData, mimeType, cancellationToken);
            if (mistralResult.IsSuccess)
            {
                ResetFailures(ref _mistralOcrConsecutiveFailures, ref _mistralOcrCooldownExpiresUtc, "MistralOCR");
                _logger.LogInformation("OCR completed via Mistral OCR (Tier 2b)");
                return mistralResult;
            }

            ReportFailure(ref _mistralOcrConsecutiveFailures, ref _mistralOcrCooldownExpiresUtc, "MistralOCR", mistralResult.ErrorMessage);
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
            _logger.LogError("All 6 OCR tiers failed. PdfPig: {PdfPigError}, Tesseract: {TesseractError}, Gemini: {GeminiError}",
                pdfPigResult.ErrorMessage, tesseractResult.ErrorMessage, geminiResult.ErrorMessage);
        }

        return geminiResult;
    }

    /// <summary>
    /// Checks whether a provider is currently in cooldown. Thread-safe.
    /// </summary>
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
