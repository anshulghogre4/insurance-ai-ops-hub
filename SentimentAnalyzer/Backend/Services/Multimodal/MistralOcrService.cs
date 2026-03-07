using System.Text.Json;
using Microsoft.Extensions.Options;
using SentimentAnalyzer.Agents.Configuration;
using SentimentAnalyzer.Agents.Orchestration;

namespace SentimentAnalyzer.API.Services.Multimodal;

/// <summary>
/// Tier 2b OCR: Mistral OCR API for high-accuracy document text extraction.
/// Reuses existing Mistral API key from LLM chain. 1,000 pages/doc, 50MB max.
/// DATA PRIVACY WARNING: Free tier data may be used for model training (same policy as Gemini Vision).
/// Insurance use case: high-accuracy OCR for complex policy documents when Azure DocIntel F0 is exhausted.
/// PII redaction is applied to extracted text before returning to callers.
/// </summary>
public class MistralOcrService : IDocumentOcrService
{
    private const string OcrEndpoint = "https://api.mistral.ai/v1/ocr";
    private const double DefaultConfidence = 0.90;
    private const int MaxFileSizeBytes = 50 * 1024 * 1024; // 50MB

    private readonly HttpClient _httpClient;
    private readonly MistralSettings _settings;
    private readonly ILogger<MistralOcrService> _logger;
    private readonly IPIIRedactor? _piiRedactor;

    public MistralOcrService(
        HttpClient httpClient,
        IOptions<AgentSystemSettings> settings,
        ILogger<MistralOcrService> logger,
        IPIIRedactor? piiRedactor = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _settings = settings?.Value?.Mistral ?? throw new ArgumentNullException(nameof(settings));
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
                Provider = "MistralOCR",
                ErrorMessage = "Mistral API key not configured."
            };
        }

        if (documentData.Length > MaxFileSizeBytes)
        {
            _logger.LogWarning(
                "Document size {Size} bytes exceeds Mistral OCR 50MB limit. Skipping.",
                documentData.Length);
            return new OcrResult
            {
                IsSuccess = false,
                Provider = "MistralOCR",
                ErrorMessage = "Document exceeds Mistral OCR 50MB file size limit"
            };
        }

        try
        {
            var base64Data = Convert.ToBase64String(documentData);
            var dataUrl = $"data:{mimeType};base64,{base64Data}";

            var requestBody = new
            {
                model = "mistral-ocr-latest",
                document = new
                {
                    type = "document_url",
                    document_url = dataUrl
                }
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);
            using var httpContent = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, OcrEndpoint);
            request.Headers.Add("Authorization", $"Bearer {_settings.ApiKey}");
            request.Content = httpContent;

            _logger.LogInformation(
                "Starting Mistral OCR for {Size} byte document ({MimeType})",
                documentData.Length, mimeType);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Mistral OCR API returned {StatusCode}: {Error}", response.StatusCode, errorBody);
                return new OcrResult
                {
                    IsSuccess = false,
                    Provider = "MistralOCR",
                    ErrorMessage = $"Mistral OCR API error: {response.StatusCode}"
                };
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            // Parse response: pages array with markdown content
            var allText = new List<string>();
            var pageCount = 0;

            if (root.TryGetProperty("pages", out var pages))
            {
                foreach (var page in pages.EnumerateArray())
                {
                    pageCount++;
                    var markdown = page.TryGetProperty("markdown", out var md)
                        ? md.GetString() ?? string.Empty
                        : string.Empty;
                    allText.Add(markdown);
                }
            }

            var fullText = string.Join("\n\n", allText);

            if (string.IsNullOrWhiteSpace(fullText))
            {
                return new OcrResult
                {
                    IsSuccess = false,
                    Provider = "MistralOCR",
                    ErrorMessage = "Mistral OCR returned empty text"
                };
            }

            var sanitizedText = _piiRedactor?.Redact(fullText) ?? fullText;

            _logger.LogInformation(
                "Mistral OCR completed. Pages: {Pages}, Text length: {Length} chars",
                pageCount, sanitizedText.Length);

            return new OcrResult
            {
                IsSuccess = true,
                ExtractedText = sanitizedText,
                PageCount = pageCount,
                Confidence = DefaultConfidence,
                Provider = "MistralOCR"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Mistral OCR extraction failed");
            return new OcrResult
            {
                IsSuccess = false,
                Provider = "MistralOCR",
                ErrorMessage = $"Mistral OCR error: {ex.Message}"
            };
        }
    }
}
