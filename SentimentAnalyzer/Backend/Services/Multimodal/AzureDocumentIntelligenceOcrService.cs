using Azure;
using Azure.AI.DocumentIntelligence;
using Microsoft.Extensions.Options;
using SentimentAnalyzer.Agents.Configuration;
using SentimentAnalyzer.Agents.Orchestration;

namespace SentimentAnalyzer.API.Services.Multimodal;

/// <summary>
/// Tier 2 document OCR service using Azure AI Document Intelligence (prebuilt-read model).
/// Free F0 tier: 500 pages/month, max 2 pages/request, 4MB max file size, 1 req/sec.
/// Insurance use case: highest-accuracy OCR for scanned policy documents, claim forms, and adjuster reports.
/// PII redaction is applied to extracted text before returning to callers.
/// </summary>
public class AzureDocumentIntelligenceOcrService : IDocumentOcrService
{
    /// <summary>Maximum file size for Azure Document Intelligence F0 tier (4MB).</summary>
    public const int MaxFileSizeBytes = 4 * 1024 * 1024;

    /// <summary>Azure F0 tier limits to 2 pages per request. Batch pages in groups of 2.</summary>
    private const int PagesPerBatch = 2;

    private readonly AzureDocumentIntelligenceSettings _settings;
    private readonly ILogger<AzureDocumentIntelligenceOcrService> _logger;
    private readonly IPIIRedactor? _piiRedactor;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureDocumentIntelligenceOcrService"/> class.
    /// </summary>
    /// <param name="settings">Agent system settings containing Azure Document Intelligence configuration.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="piiRedactor">Optional PII redactor for sanitizing extracted text.</param>
    public AzureDocumentIntelligenceOcrService(
        IOptions<AgentSystemSettings> settings,
        ILogger<AzureDocumentIntelligenceOcrService> logger,
        IPIIRedactor? piiRedactor = null)
    {
        _settings = settings?.Value?.AzureDocumentIntelligence ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _piiRedactor = piiRedactor;
    }

    /// <inheritdoc />
    public async Task<OcrResult> ExtractTextAsync(
        byte[] documentData,
        string mimeType = "application/pdf",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            return new OcrResult
            {
                IsSuccess = false,
                Provider = "AzureDocIntel",
                ErrorMessage = "Azure Document Intelligence API key not configured."
            };
        }

        if (string.IsNullOrWhiteSpace(_settings.Endpoint))
        {
            return new OcrResult
            {
                IsSuccess = false,
                Provider = "AzureDocIntel",
                ErrorMessage = "Azure Document Intelligence endpoint not configured."
            };
        }

        if (documentData.Length > MaxFileSizeBytes)
        {
            _logger.LogWarning(
                "Document size {Size} bytes exceeds Azure F0 4MB limit ({MaxSize} bytes). Falling back to next provider.",
                documentData.Length, MaxFileSizeBytes);

            return new OcrResult
            {
                IsSuccess = false,
                Provider = "AzureDocIntel",
                ErrorMessage = "Document exceeds Azure F0 4MB file size limit"
            };
        }

        try
        {
            var model = string.IsNullOrWhiteSpace(_settings.Model) ? "prebuilt-read" : _settings.Model;

            var client = new DocumentIntelligenceClient(
                new Uri(_settings.Endpoint),
                new AzureKeyCredential(_settings.ApiKey));

            // Azure F0 tier limits to 2 pages per request. Batch pages in groups of 2
            // and stitch the results together for full document extraction.
            var totalPageCount = DetectPageCount(documentData);
            var needsBatching = totalPageCount > PagesPerBatch;

            if (needsBatching)
            {
                _logger.LogInformation(
                    "Azure DocIntel F0: document has {TotalPages} pages, batching in groups of {BatchSize}",
                    totalPageCount, PagesPerBatch);
            }
            else
            {
                _logger.LogInformation(
                    "Starting Azure Document Intelligence analysis with model '{Model}' for {Size} byte document",
                    model, documentData.Length);
            }

            var allTextParts = new List<string>();
            var allConfidences = new List<double>();
            var totalPages = 0;

            // Process pages in batches of 2 (F0 tier limit)
            var batchCount = needsBatching ? (int)Math.Ceiling((double)totalPageCount / PagesPerBatch) : 1;

            for (var batch = 0; batch < batchCount; batch++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var startPage = batch * PagesPerBatch + 1; // 1-based page numbers
                var endPage = Math.Min(startPage + PagesPerBatch - 1, totalPageCount);

                // Use AnalyzeDocumentOptions with Pages property for batch processing
                var options = new AnalyzeDocumentOptions(model, BinaryData.FromBytes(documentData));
                if (needsBatching)
                {
                    options.Pages = $"{startPage}-{endPage}";
                }

                var operation = await client.AnalyzeDocumentAsync(
                    WaitUntil.Completed,
                    options,
                    cancellationToken);

                var result = operation.Value;
                var batchText = result.Content ?? string.Empty;
                var batchPageCount = result.Pages?.Count ?? 0;

                totalPages += batchPageCount;
                allTextParts.Add(batchText);

                // Collect word-level confidence from this batch
                if (result.Pages is { Count: > 0 })
                {
                    var batchWords = result.Pages
                        .Where(p => p.Words is not null)
                        .SelectMany(p => p.Words)
                        .ToList();

                    if (batchWords.Count > 0)
                    {
                        allConfidences.AddRange(batchWords.Select(w => (double)w.Confidence));
                    }
                }

                if (needsBatching)
                {
                    _logger.LogInformation(
                        "Azure DocIntel batch {Batch}/{Total}: pages {Start}-{End} extracted ({Chars} chars)",
                        batch + 1, batchCount, startPage, endPage, batchText.Length);
                }
            }

            var fullText = string.Join("\n\n", allTextParts);
            var confidence = allConfidences.Count > 0 ? allConfidences.Average() : 0.9;

            // Redact PII from extracted text before returning to callers
            var sanitizedText = _piiRedactor?.Redact(fullText) ?? fullText;

            _logger.LogInformation(
                "Azure Document Intelligence extraction completed. Pages: {Pages}, Confidence: {Confidence:F3}, Text length: {Length} chars",
                totalPages, confidence, sanitizedText.Length);

            return new OcrResult
            {
                IsSuccess = true,
                ExtractedText = sanitizedText,
                PageCount = totalPages,
                Confidence = confidence,
                Provider = "AzureDocIntel"
            };
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex,
                "Azure Document Intelligence API request failed with status {StatusCode}: {Message}",
                ex.Status, ex.Message);

            return new OcrResult
            {
                IsSuccess = false,
                Provider = "AzureDocIntel",
                ErrorMessage = $"Azure Document Intelligence API error (HTTP {ex.Status}): {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure Document Intelligence extraction failed unexpectedly");

            return new OcrResult
            {
                IsSuccess = false,
                Provider = "AzureDocIntel",
                ErrorMessage = $"Azure Document Intelligence error: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Detects the number of pages in a PDF document using PdfPig (lightweight, local).
    /// Returns 0 for non-PDF documents or on failure — caller falls back to single-request mode.
    /// </summary>
    private int DetectPageCount(byte[] documentData)
    {
        try
        {
            using var stream = new MemoryStream(documentData);
            using var doc = UglyToad.PdfPig.PdfDocument.Open(stream);
            return doc.NumberOfPages;
        }
        catch
        {
            return 0;
        }
    }
}
