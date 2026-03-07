using SentimentAnalyzer.Agents.Orchestration;
using TesseractOCR;
using TesseractOCR.Enums;

namespace SentimentAnalyzer.API.Services.Multimodal;

/// <summary>
/// Tier 1b OCR: local Tesseract OCR for scanned documents that PdfPig cannot handle.
/// 100% local — no API calls, no data transfer, unlimited pages.
/// Requires tessdata directory with trained language data (eng.traineddata).
/// Insurance use case: scanned claim forms, adjuster reports, and handwritten notes.
/// PII redaction is applied to extracted text before returning to callers.
/// </summary>
public class TesseractOcrService : IDocumentOcrService
{
    private readonly ILogger<TesseractOcrService> _logger;
    private readonly IPIIRedactor? _piiRedactor;
    private readonly string _tessdataPath;

    /// <summary>
    /// Minimum character threshold for extracted text. Documents with fewer characters
    /// likely had OCR issues and should fall through to cloud providers.
    /// </summary>
    private const int MinimumTextLength = 50;

    /// <summary>
    /// Supported image MIME types for direct Tesseract processing.
    /// </summary>
    private static readonly HashSet<string> SupportedImageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png", "image/jpeg", "image/jpg", "image/tiff", "image/bmp", "image/gif"
    };

    public TesseractOcrService(
        ILogger<TesseractOcrService> logger,
        IPIIRedactor? piiRedactor = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _piiRedactor = piiRedactor;

        // Look for tessdata in multiple locations
        var basePath = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(basePath, "tessdata"),
            Path.Combine(Directory.GetCurrentDirectory(), "tessdata"),
            Path.Combine(basePath, "..", "tessdata")
        };

        _tessdataPath = candidates.FirstOrDefault(Directory.Exists) ?? Path.Combine(basePath, "tessdata");
    }

    /// <inheritdoc />
    public Task<OcrResult> ExtractTextAsync(
        byte[] documentData,
        string mimeType = "application/pdf",
        CancellationToken cancellationToken = default)
    {
        // Check if tessdata directory exists with required language data
        if (!Directory.Exists(_tessdataPath))
        {
            _logger.LogWarning(
                "Tesseract tessdata directory not found at {Path}. Download eng.traineddata to enable local OCR.",
                _tessdataPath);
            return Task.FromResult(new OcrResult
            {
                IsSuccess = false,
                Provider = "Tesseract",
                ErrorMessage = "Tesseract tessdata not configured"
            });
        }

        // Tesseract works best with image files; PDFs need page-to-image conversion
        if (mimeType == "application/pdf")
        {
            return ProcessPdfAsync(documentData, cancellationToken);
        }

        if (SupportedImageTypes.Contains(mimeType))
        {
            return ProcessImageAsync(documentData, cancellationToken);
        }

        _logger.LogWarning("Tesseract received unsupported MIME type: {MimeType}", mimeType);
        return Task.FromResult(new OcrResult
        {
            IsSuccess = false,
            Provider = "Tesseract",
            ErrorMessage = $"Unsupported document type: {mimeType}"
        });
    }

    /// <summary>
    /// Processes an image file directly through Tesseract OCR.
    /// </summary>
    private Task<OcrResult> ProcessImageAsync(byte[] imageData, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var engine = new Engine(_tessdataPath, Language.English, EngineMode.Default);
            using var img = TesseractOCR.Pix.Image.LoadFromMemory(imageData);
            using var page = engine.Process(img);

            var text = page.Text ?? string.Empty;
            var confidence = page.MeanConfidence;

            if (text.Length < MinimumTextLength)
            {
                _logger.LogInformation(
                    "Tesseract extracted only {Length} chars (confidence: {Confidence:P0}) — insufficient text",
                    text.Length, confidence);
                return Task.FromResult(new OcrResult
                {
                    IsSuccess = false,
                    Provider = "Tesseract",
                    ErrorMessage = "Insufficient text extracted from image"
                });
            }

            var sanitizedText = _piiRedactor?.Redact(text) ?? text;

            _logger.LogInformation(
                "Tesseract OCR completed. Text length: {Length} chars, Confidence: {Confidence:P0}",
                sanitizedText.Length, confidence);

            return Task.FromResult(new OcrResult
            {
                IsSuccess = true,
                ExtractedText = sanitizedText,
                PageCount = 1,
                Confidence = confidence,
                Provider = "Tesseract"
            });
        }
        catch (OperationCanceledException)
        {
            return Task.FromResult(new OcrResult
            {
                IsSuccess = false,
                Provider = "Tesseract",
                ErrorMessage = "OCR was cancelled"
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tesseract image OCR failed");
            return Task.FromResult(new OcrResult
            {
                IsSuccess = false,
                Provider = "Tesseract",
                ErrorMessage = $"Tesseract OCR error: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Attempts to process a scanned PDF through Tesseract.
    /// Uses PdfPig to iterate pages and extract embedded images for OCR.
    /// Falls through to next provider if PDF structure prevents image extraction.
    /// </summary>
    private Task<OcrResult> ProcessPdfAsync(byte[] pdfData, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var stream = new MemoryStream(pdfData);
            using var pdfDoc = UglyToad.PdfPig.PdfDocument.Open(stream);
            using var engine = new Engine(_tessdataPath, Language.English, EngineMode.Default);

            var pageTexts = new List<string>();
            var totalConfidence = 0.0;
            var processedPages = 0;

            foreach (var pdfPage in pdfDoc.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Extract embedded images from the PDF page
                var images = pdfPage.GetImages().ToList();
                if (images.Count == 0) continue;

                foreach (var image in images)
                {
                    try
                    {
                        var imageBytes = image.RawBytes.ToArray();
                        if (imageBytes.Length < 100) continue; // Skip tiny images (icons, borders)

                        using var img = TesseractOCR.Pix.Image.LoadFromMemory(imageBytes);
                        using var page = engine.Process(img);

                        var pageText = page.Text ?? string.Empty;
                        if (pageText.Trim().Length > 10)
                        {
                            pageTexts.Add(pageText);
                            totalConfidence += page.MeanConfidence;
                            processedPages++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Tesseract failed to process embedded image on page {Page}", pdfPage.Number);
                    }
                }
            }

            if (processedPages == 0 || pageTexts.Count == 0)
            {
                _logger.LogInformation(
                    "Tesseract could not extract images from PDF ({Pages} pages scanned). Falling through to cloud OCR.",
                    pdfDoc.NumberOfPages);
                return Task.FromResult(new OcrResult
                {
                    IsSuccess = false,
                    Provider = "Tesseract",
                    ErrorMessage = "No processable images found in scanned PDF"
                });
            }

            var fullText = string.Join("\n\n", pageTexts);
            var avgConfidence = totalConfidence / processedPages;

            if (fullText.Length < MinimumTextLength)
            {
                return Task.FromResult(new OcrResult
                {
                    IsSuccess = false,
                    Provider = "Tesseract",
                    ErrorMessage = "Insufficient text extracted from PDF images"
                });
            }

            var sanitizedText = _piiRedactor?.Redact(fullText) ?? fullText;

            _logger.LogInformation(
                "Tesseract PDF OCR completed. Pages: {Pages}, Text length: {Length} chars, Confidence: {Confidence:P0}",
                processedPages, sanitizedText.Length, avgConfidence);

            return Task.FromResult(new OcrResult
            {
                IsSuccess = true,
                ExtractedText = sanitizedText,
                PageCount = processedPages,
                Confidence = avgConfidence,
                Provider = "Tesseract"
            });
        }
        catch (OperationCanceledException)
        {
            return Task.FromResult(new OcrResult
            {
                IsSuccess = false,
                Provider = "Tesseract",
                ErrorMessage = "OCR was cancelled"
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tesseract PDF OCR failed");
            return Task.FromResult(new OcrResult
            {
                IsSuccess = false,
                Provider = "Tesseract",
                ErrorMessage = $"Tesseract PDF OCR error: {ex.Message}"
            });
        }
    }
}
