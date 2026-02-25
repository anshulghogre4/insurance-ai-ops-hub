using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SentimentAnalyzer.API.Data;
using SentimentAnalyzer.API.Data.Entities;
using SentimentAnalyzer.API.Models;
using SentimentAnalyzer.Agents.Orchestration;
using SentimentAnalyzer.API.Services.Multimodal;

namespace SentimentAnalyzer.API.Services.Claims;

/// <summary>
/// Routes multimodal evidence to the appropriate service based on MIME type.
/// Processes images via vision (with Azure → Cloudflare fallback), audio via STT,
/// documents via OCR, then runs NER on all text output.
/// </summary>
public class MultimodalEvidenceProcessor : IMultimodalEvidenceProcessor
{
    private readonly IImageAnalysisService _imageService;
    private readonly IImageAnalysisService? _fallbackImageService;
    private readonly ISpeechToTextService _speechService;
    private readonly IDocumentOcrService _ocrService;
    private readonly IEntityExtractionService _nerService;
    private readonly IClaimsRepository _claimsRepo;
    private readonly IPIIRedactor? _piiRedactor;
    private readonly ILogger<MultimodalEvidenceProcessor> _logger;

    public MultimodalEvidenceProcessor(
        IImageAnalysisService imageService,
        ISpeechToTextService speechService,
        IDocumentOcrService ocrService,
        IEntityExtractionService nerService,
        IClaimsRepository claimsRepo,
        ILogger<MultimodalEvidenceProcessor> logger,
        [FromKeyedServices("CloudflareVision")] IImageAnalysisService? fallbackImageService = null,
        IPIIRedactor? piiRedactor = null)
    {
        _imageService = imageService ?? throw new ArgumentNullException(nameof(imageService));
        _fallbackImageService = fallbackImageService;
        _speechService = speechService ?? throw new ArgumentNullException(nameof(speechService));
        _ocrService = ocrService ?? throw new ArgumentNullException(nameof(ocrService));
        _nerService = nerService ?? throw new ArgumentNullException(nameof(nerService));
        _claimsRepo = claimsRepo ?? throw new ArgumentNullException(nameof(claimsRepo));
        _piiRedactor = piiRedactor;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<ClaimEvidenceResponse> ProcessAsync(int claimId, byte[] fileData, string mimeType, string fileName)
    {
        _logger.LogInformation("Processing evidence for claim {ClaimId}: {MimeType}, {Size} bytes",
            claimId, mimeType, fileData.Length);

        var evidenceType = ClassifyEvidenceType(mimeType);
        string processedText;
        string provider;
        var damageIndicators = new List<string>();

        switch (evidenceType)
        {
            case "image":
                (processedText, provider, damageIndicators) = await AnalyzeImageWithFallbackAsync(claimId, fileData, mimeType);
                break;

            case "audio":
                var audioResult = await _speechService.TranscribeAsync(fileData, mimeType);
                processedText = audioResult.Text ?? "";
                provider = audioResult.Provider ?? "Deepgram";
                if (!audioResult.IsSuccess)
                {
                    _logger.LogWarning("Audio transcription failed for claim {ClaimId}: {Error}", claimId, audioResult.ErrorMessage);
                }
                break;

            case "document":
                var ocrResult = await _ocrService.ExtractTextAsync(fileData, mimeType);
                processedText = ocrResult.ExtractedText ?? "";
                provider = ocrResult.Provider ?? "OcrSpace";
                if (!ocrResult.IsSuccess)
                {
                    _logger.LogWarning("OCR extraction failed for claim {ClaimId}: {Error}", claimId, ocrResult.ErrorMessage);
                }
                break;

            default:
                _logger.LogWarning("Unsupported MIME type {MimeType} for claim {ClaimId}", mimeType, claimId);
                processedText = "";
                provider = "None";
                break;
        }

        // Run NER on processed text if we have any
        string entitiesJson = "[]";
        if (!string.IsNullOrWhiteSpace(processedText))
        {
            try
            {
                var nerResult = await _nerService.ExtractEntitiesAsync(processedText);
                if (nerResult.IsSuccess && nerResult.Entities?.Count > 0)
                {
                    // Redact PII-category entity values before serialization (BA compliance requirement)
                    // PERSON = HuggingFaceNerService maps BERT "PER" → "PERSON" before entities reach here
                    var piiTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        { "PERSON", "SSN", "EMAIL", "PHONE", "POLICY_NUMBER", "CLAIM_NUMBER" };

                    entitiesJson = JsonSerializer.Serialize(nerResult.Entities.Select(e => new
                    {
                        e.Type,
                        Value = piiTypes.Contains(e.Type) ? $"[{e.Type}-REDACTED]" : e.Value,
                        e.Confidence
                    }));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "NER extraction failed for claim {ClaimId}, continuing without entities", claimId);
            }
        }

        // PII-redact processed text before DB persistence (P0 security fix)
        var safeText = _piiRedactor?.Redact(processedText) ?? processedText;

        // Persist the evidence record
        var record = new ClaimEvidenceRecord
        {
            ClaimId = claimId,
            EvidenceType = evidenceType,
            MimeType = mimeType,
            Provider = provider,
            ProcessedText = safeText,
            DamageIndicatorsJson = damageIndicators.Count > 0
                ? JsonSerializer.Serialize(damageIndicators)
                : "[]",
            EntitiesJson = entitiesJson
        };

        var saved = await _claimsRepo.SaveEvidenceAsync(record);

        _logger.LogInformation("Evidence processed for claim {ClaimId}: type={Type}, provider={Provider}, text length={Length}",
            claimId, evidenceType, provider, processedText.Length);

        return new ClaimEvidenceResponse
        {
            EvidenceType = saved.EvidenceType,
            Provider = saved.Provider,
            ProcessedText = safeText,
            DamageIndicators = damageIndicators,
            CreatedAt = saved.CreatedAt
        };
    }

    /// <summary>
    /// Analyzes an image with AzureVision, falling back to CloudflareVision on failure.
    /// </summary>
    private async Task<(string Text, string Provider, List<string> DamageIndicators)> AnalyzeImageWithFallbackAsync(
        int claimId, byte[] fileData, string mimeType)
    {
        // Try primary: AzureVision
        var imageResult = await _imageService.AnalyzeImageAsync(fileData, mimeType);
        if (imageResult.IsSuccess)
        {
            return (
                imageResult.Description ?? "",
                imageResult.Provider ?? "AzureVision",
                imageResult.DamageIndicators?.ToList() ?? []);
        }

        _logger.LogWarning("Primary vision service failed for claim {ClaimId}: {Error}. Trying fallback.",
            claimId, imageResult.ErrorMessage);

        // Fallback: CloudflareVision
        if (_fallbackImageService != null)
        {
            try
            {
                var fallbackResult = await _fallbackImageService.AnalyzeImageAsync(fileData, mimeType);
                if (fallbackResult.IsSuccess)
                {
                    _logger.LogInformation("Cloudflare fallback succeeded for claim {ClaimId}", claimId);
                    return (
                        fallbackResult.Description ?? "",
                        fallbackResult.Provider ?? "CloudflareVision",
                        fallbackResult.DamageIndicators?.ToList() ?? []);
                }
                _logger.LogWarning("Cloudflare fallback also failed for claim {ClaimId}: {Error}", claimId, fallbackResult.ErrorMessage);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cloudflare fallback threw exception for claim {ClaimId}", claimId);
            }
        }

        // Both failed — return what we have from primary
        return (imageResult.Description ?? "", imageResult.Provider ?? "AzureVision", imageResult.DamageIndicators?.ToList() ?? []);
    }

    /// <summary>
    /// Classifies MIME type into evidence category: image, audio, or document.
    /// </summary>
    private static string ClassifyEvidenceType(string mimeType)
    {
        if (mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return "image";
        if (mimeType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
            return "audio";
        if (mimeType.StartsWith("application/pdf", StringComparison.OrdinalIgnoreCase) ||
            mimeType.Contains("document", StringComparison.OrdinalIgnoreCase))
            return "document";
        return "unknown";
    }
}
