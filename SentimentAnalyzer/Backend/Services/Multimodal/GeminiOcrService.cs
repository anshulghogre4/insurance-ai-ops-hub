using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using SentimentAnalyzer.Agents.Configuration;
using SentimentAnalyzer.Agents.Orchestration;

namespace SentimentAnalyzer.API.Services.Multimodal;

/// <summary>
/// Tier 4 (last resort) document OCR service using Gemini Vision API (gemini-2.0-flash-lite model).
/// Uses the Gemini free tier for vision-based OCR on scanned documents.
/// Insurance use case: OCR fallback when Azure Document Intelligence and OCR Space are unavailable.
/// PII redaction is applied to extracted text before returning to callers.
///
/// DATA PRIVACY WARNING: Google's free-tier Gemini API terms state that submitted data may be used
/// to "provide, improve, and develop Google products and services and machine learning technologies."
/// Human reviewers may read/annotate input and output. PII redaction is mandatory before sending.
/// For paid-tier Gemini or EEA/Switzerland/UK users, these training provisions do not apply.
/// See: https://ai.google.dev/gemini-api/terms
/// </summary>
public class GeminiOcrService : IDocumentOcrService
{
    /// <summary>Hardcoded model for OCR — Flash-Lite has the best free tier for vision tasks.</summary>
    private const string OcrModel = "gemini-2.0-flash-lite";

    /// <summary>Page break marker inserted by Gemini between pages.</summary>
    private const string PageBreakMarker = "---PAGE BREAK---";

    /// <summary>Default confidence for vision-based OCR (no per-word confidence available).</summary>
    private const double DefaultConfidence = 0.75;

    /// <summary>Regex to detect conversational preamble lines from the model.</summary>
    private static readonly Regex PreamblePattern = new(
        @"^(Here|The|This|Below|I).*:?\s*$",
        RegexOptions.Compiled);

    private readonly HttpClient _httpClient;
    private readonly GeminiSettings _settings;
    private readonly ILogger<GeminiOcrService> _logger;
    private readonly IPIIRedactor? _piiRedactor;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeminiOcrService"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client for Gemini REST API calls.</param>
    /// <param name="settings">Agent system settings containing Gemini API key.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="piiRedactor">Optional PII redactor for sanitizing extracted text.</param>
    public GeminiOcrService(
        HttpClient httpClient,
        IOptions<AgentSystemSettings> settings,
        ILogger<GeminiOcrService> logger,
        IPIIRedactor? piiRedactor = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _settings = settings?.Value?.Gemini ?? throw new ArgumentNullException(nameof(settings));
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
                Provider = "GeminiVision",
                ErrorMessage = "Gemini API key not configured."
            };
        }

        try
        {
            var base64Data = Convert.ToBase64String(documentData);

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new
                            {
                                inline_data = new
                                {
                                    mime_type = mimeType,
                                    data = base64Data
                                }
                            },
                            new
                            {
                                text = "Extract all text from this document exactly as it appears, preserving the original formatting, paragraph structure, and any table layouts. Do not summarize, interpret, or add any commentary. If the document contains multiple pages, insert '---PAGE BREAK---' between each page's content. Return ONLY the extracted text with no preamble or explanation."
                            }
                        }
                    }
                }
            };

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{OcrModel}:generateContent?key={_settings.ApiKey}";

            var jsonContent = JsonSerializer.Serialize(requestBody);
            using var httpContent = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            _logger.LogInformation(
                "Starting Gemini Vision OCR with model '{Model}' for {Size} byte document ({MimeType})",
                OcrModel, documentData.Length, mimeType);

            var response = await _httpClient.PostAsync(url, httpContent, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Gemini Vision API returned {StatusCode}: {Error}", response.StatusCode, errorBody);
                return new OcrResult
                {
                    IsSuccess = false,
                    Provider = "GeminiVision",
                    ErrorMessage = $"Gemini Vision API error: {response.StatusCode}"
                };
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            // Parse response: candidates[0].content.parts[0].text
            var extractedText = root
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString() ?? string.Empty;

            // Strip conversational preamble if present
            extractedText = StripPreamble(extractedText);

            // Count pages from PAGE BREAK markers
            var pageBreakCount = CountOccurrences(extractedText, PageBreakMarker);
            var pageCount = pageBreakCount + 1;

            // Replace page break markers with double newlines in final text
            extractedText = extractedText.Replace(PageBreakMarker, "\n\n");

            // Redact PII from extracted text before returning to callers
            var sanitizedText = _piiRedactor?.Redact(extractedText) ?? extractedText;

            _logger.LogInformation(
                "Gemini Vision OCR completed. Pages: {Pages}, Text length: {Length} chars",
                pageCount, sanitizedText.Length);

            return new OcrResult
            {
                IsSuccess = true,
                ExtractedText = sanitizedText,
                PageCount = pageCount,
                Confidence = DefaultConfidence,
                Provider = "GeminiVision"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini Vision OCR extraction failed");
            return new OcrResult
            {
                IsSuccess = false,
                Provider = "GeminiVision",
                ErrorMessage = $"Gemini Vision OCR error: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Strips conversational preamble from the beginning of extracted text.
    /// Some models prepend lines like "Here is the extracted text:" despite being told not to.
    /// </summary>
    private static string StripPreamble(string text)
    {
        var lines = text.Split('\n');
        if (lines.Length > 1 && PreamblePattern.IsMatch(lines[0].Trim()))
        {
            return string.Join('\n', lines.Skip(1)).TrimStart();
        }

        return text;
    }

    /// <summary>
    /// Counts the number of non-overlapping occurrences of a substring.
    /// </summary>
    private static int CountOccurrences(string text, string pattern)
    {
        var count = 0;
        var index = 0;

        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += pattern.Length;
        }

        return count;
    }
}
