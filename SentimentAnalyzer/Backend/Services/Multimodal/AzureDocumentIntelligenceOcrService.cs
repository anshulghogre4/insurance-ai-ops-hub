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

            _logger.LogInformation(
                "Starting Azure Document Intelligence analysis with model '{Model}' for {Size} byte document",
                model, documentData.Length);

            var operation = await client.AnalyzeDocumentAsync(
                WaitUntil.Completed,
                model,
                BinaryData.FromBytes(documentData),
                cancellationToken: cancellationToken);

            var result = operation.Value;

            var fullText = result.Content ?? string.Empty;
            var pageCount = result.Pages?.Count ?? 0;

            // Calculate average word confidence across all pages
            var confidence = 0.9; // Default when no words are detected
            if (result.Pages is { Count: > 0 })
            {
                var allWords = result.Pages
                    .Where(p => p.Words is not null)
                    .SelectMany(p => p.Words)
                    .ToList();

                if (allWords.Count > 0)
                {
                    confidence = allWords.Average(w => w.Confidence);
                }
            }

            // F0 tier limit warning: max 2 pages per request
            if (pageCount >= 2)
            {
                _logger.LogWarning(
                    "Azure Document Intelligence F0 processed {PageCount} pages (F0 tier limit: 2 pages/request)",
                    pageCount);
            }

            // Redact PII from extracted text before returning to callers
            var sanitizedText = _piiRedactor?.Redact(fullText) ?? fullText;

            _logger.LogInformation(
                "Azure Document Intelligence extraction completed. Pages: {Pages}, Confidence: {Confidence:F3}, Text length: {Length} chars",
                pageCount, confidence, sanitizedText.Length);

            return new OcrResult
            {
                IsSuccess = true,
                ExtractedText = sanitizedText,
                PageCount = pageCount,
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
}
