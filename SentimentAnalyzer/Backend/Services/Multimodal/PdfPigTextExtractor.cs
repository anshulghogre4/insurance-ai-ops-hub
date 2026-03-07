using UglyToad.PdfPig;
using SentimentAnalyzer.Agents.Orchestration;

namespace SentimentAnalyzer.API.Services.Multimodal;

/// <summary>
/// Tier 1 OCR: extracts native/digital PDF text using PdfPig in under 50ms with zero API calls.
/// Insurance use case: fast text extraction from digitally-generated policy documents, endorsements,
/// and claim forms before falling back to cloud OCR for scanned documents.
/// PII redaction is applied to extracted text before returning to callers.
/// </summary>
public class PdfPigTextExtractor : IDocumentOcrService
{
    private readonly ILogger<PdfPigTextExtractor> _logger;
    private readonly IPIIRedactor? _piiRedactor;

    /// <summary>
    /// Minimum character threshold for extracted text. Documents with fewer characters
    /// are likely scanned images and should fall through to Azure OCR (Tier 2).
    /// </summary>
    private const int MinimumTextLength = 100;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfPigTextExtractor"/> class.
    /// </summary>
    /// <param name="logger">Logger for diagnostics and telemetry.</param>
    /// <param name="piiRedactor">Optional PII redactor applied to extracted text before returning.</param>
    public PdfPigTextExtractor(
        ILogger<PdfPigTextExtractor> logger,
        IPIIRedactor? piiRedactor = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _piiRedactor = piiRedactor;
    }

    /// <inheritdoc />
    public Task<OcrResult> ExtractTextAsync(
        byte[] documentData,
        string mimeType = "application/pdf",
        CancellationToken cancellationToken = default)
    {
        if (mimeType != "application/pdf")
        {
            _logger.LogWarning("PdfPig received unsupported MIME type: {MimeType}", mimeType);
            return Task.FromResult(new OcrResult
            {
                IsSuccess = false,
                Provider = "PdfPig",
                ErrorMessage = "PdfPig only supports PDF documents"
            });
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var stream = new MemoryStream(documentData);
            using var document = PdfDocument.Open(stream);

            var pageTexts = new List<string>();
            var pageCount = 0;

            foreach (var page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();
                pageCount++;
                var pageText = page.Text ?? string.Empty;

                // Fallback: if page.Text is empty/too short, try page.Letters collection.
                // Some PDFs use CID or Type3 fonts where page.Text returns nothing,
                // but individual Letter objects are still accessible.
                if (pageText.Length < 10 && page.Letters.Count > 0)
                {
                    _logger.LogInformation(
                        "PdfPig page {Page}: Text property empty ({TextLen} chars) but {LetterCount} Letters found — using Letters fallback (CID/Type3 font)",
                        pageCount, pageText.Length, page.Letters.Count);
                    pageText = string.Concat(page.Letters.Select(l => l.Value));
                }

                pageTexts.Add(pageText);
            }

            var fullText = string.Join("\n\n", pageTexts);

            if (fullText.Length < MinimumTextLength)
            {
                _logger.LogInformation(
                    "PdfPig extracted only {Length} chars from {Pages} page(s) — likely a scanned document or " +
                    "encrypted/signed PDF with non-extractable text. Deferring to cloud OCR. " +
                    "First 200 chars: [{Preview}]",
                    fullText.Length, pageCount,
                    fullText.Length > 0 ? fullText[..Math.Min(200, fullText.Length)] : "(empty)");

                return Task.FromResult(new OcrResult
                {
                    IsSuccess = false,
                    PageCount = pageCount,
                    Provider = "PdfPig",
                    ErrorMessage = "Insufficient text extracted (likely a scanned document)"
                });
            }

            // Redact PII from extracted text before returning to callers
            var sanitizedText = _piiRedactor?.Redact(fullText) ?? fullText;

            _logger.LogInformation(
                "PdfPig native text extraction completed. Pages: {Pages}, Text length: {Length} chars",
                pageCount, sanitizedText.Length);

            return Task.FromResult(new OcrResult
            {
                IsSuccess = true,
                ExtractedText = sanitizedText,
                PageCount = pageCount,
                Confidence = 1.0,
                Provider = "PdfPig"
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("PdfPig text extraction was cancelled");
            return Task.FromResult(new OcrResult
            {
                IsSuccess = false,
                Provider = "PdfPig",
                ErrorMessage = "Text extraction was cancelled"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PdfPig text extraction failed");
            return Task.FromResult(new OcrResult
            {
                IsSuccess = false,
                Provider = "PdfPig",
                ErrorMessage = $"PdfPig extraction error: {ex.Message}"
            });
        }
    }
}
