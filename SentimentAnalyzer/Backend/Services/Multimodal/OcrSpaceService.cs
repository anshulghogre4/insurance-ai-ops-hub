using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SentimentAnalyzer.Agents.Configuration;
using SentimentAnalyzer.Agents.Orchestration;

namespace SentimentAnalyzer.API.Services.Multimodal;

/// <summary>
/// Document OCR service using OCR.space free API.
/// Free tier: 500 requests/day.
/// Insurance use case: digitizing scanned policy documents and claim forms.
/// PII redaction is applied to extracted text before returning to callers.
/// </summary>
public class OcrSpaceService : IDocumentOcrService
{
    private readonly HttpClient _httpClient;
    private readonly OcrSpaceSettings _settings;
    private readonly ILogger<OcrSpaceService> _logger;
    private readonly IPIIRedactor? _piiRedactor;

    public OcrSpaceService(
        HttpClient httpClient,
        IOptions<AgentSystemSettings> settings,
        ILogger<OcrSpaceService> logger,
        IPIIRedactor? piiRedactor = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _settings = settings?.Value?.OcrSpace ?? throw new ArgumentNullException(nameof(settings));
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
                Provider = "OcrSpace",
                ErrorMessage = "OCR.space API key not configured."
            };
        }

        // OCR.space free tier has a 1MB file size limit
        const int maxFileSizeBytes = 1 * 1024 * 1024;
        if (documentData.Length > maxFileSizeBytes)
        {
            _logger.LogWarning(
                "Document size {Size} bytes exceeds OCR.space 1MB limit ({MaxSize} bytes). Skipping to next provider.",
                documentData.Length, maxFileSizeBytes);
            return new OcrResult
            {
                IsSuccess = false,
                Provider = "OcrSpace",
                ErrorMessage = "Document exceeds OCR.space 1MB file size limit"
            };
        }

        try
        {
            var fileExtension = mimeType switch
            {
                "application/pdf" => "pdf",
                "image/png" => "png",
                "image/jpeg" or "image/jpg" => "jpg",
                "image/tiff" => "tiff",
                _ => "pdf"
            };

            using var formContent = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(documentData);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
            formContent.Add(fileContent, "file", $"document.{fileExtension}");
            formContent.Add(new StringContent("2"), "OCREngine");
            formContent.Add(new StringContent("true"), "isTable");
            formContent.Add(new StringContent("true"), "scale");

            using var request = new HttpRequestMessage(HttpMethod.Post, _settings.Endpoint);
            request.Headers.Add("apikey", _settings.ApiKey);
            request.Content = formContent;

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("OCR.space API returned {StatusCode}: {Error}", response.StatusCode, errorBody);
                return new OcrResult
                {
                    IsSuccess = false,
                    Provider = "OcrSpace",
                    ErrorMessage = $"OCR.space API error: {response.StatusCode}"
                };
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Check for OCR-level errors
            if (root.TryGetProperty("IsErroredOnProcessing", out var isErrored) && isErrored.GetBoolean())
            {
                var errorMsg = root.TryGetProperty("ErrorMessage", out var errArray)
                    ? string.Join("; ", errArray.EnumerateArray().Select(e => e.GetString()))
                    : "Unknown OCR error";
                return new OcrResult
                {
                    IsSuccess = false,
                    Provider = "OcrSpace",
                    ErrorMessage = errorMsg
                };
            }

            var parsedResults = root.GetProperty("ParsedResults");
            var allText = new List<string>();
            var pageCount = 0;

            foreach (var page in parsedResults.EnumerateArray())
            {
                pageCount++;
                var pageText = page.GetProperty("ParsedText").GetString() ?? string.Empty;
                allText.Add(pageText);
            }

            var fullText = string.Join("\n\n", allText);

            // Redact PII from extracted text before returning to callers
            var sanitizedText = _piiRedactor?.Redact(fullText) ?? fullText;

            _logger.LogInformation("OCR.space extraction completed. Pages: {Pages}, Text length: {Length} chars",
                pageCount, sanitizedText.Length);

            return new OcrResult
            {
                IsSuccess = true,
                ExtractedText = sanitizedText,
                PageCount = pageCount,
                Confidence = 0.85, // OCR.space doesn't return per-result confidence, use reasonable default
                Provider = "OcrSpace"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OCR.space extraction failed");
            return new OcrResult
            {
                IsSuccess = false,
                Provider = "OcrSpace",
                ErrorMessage = $"OCR extraction error: {ex.Message}"
            };
        }
    }
}
