namespace SentimentAnalyzer.API.Services.Multimodal;

/// <summary>
/// Extracts text from document images and scanned PDFs.
/// Insurance use case: digitizing scanned policy documents and claim forms.
/// </summary>
public interface IDocumentOcrService
{
    /// <summary>
    /// Extracts text from a document image or scanned PDF.
    /// </summary>
    /// <param name="documentData">Raw document bytes (PDF, PNG, JPG).</param>
    /// <param name="mimeType">MIME type (e.g., "application/pdf", "image/png").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>OCR result with extracted text.</returns>
    Task<OcrResult> ExtractTextAsync(
        byte[] documentData,
        string mimeType = "application/pdf",
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a document OCR extraction.
/// </summary>
public class OcrResult
{
    /// <summary>Whether the extraction succeeded.</summary>
    public bool IsSuccess { get; set; }

    /// <summary>Full extracted text content.</summary>
    public string ExtractedText { get; set; } = string.Empty;

    /// <summary>Number of pages processed.</summary>
    public int PageCount { get; set; }

    /// <summary>OCR confidence score (0.0 to 1.0).</summary>
    public double Confidence { get; set; }

    /// <summary>Provider that performed the extraction.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Error message if extraction failed.</summary>
    public string? ErrorMessage { get; set; }
}
