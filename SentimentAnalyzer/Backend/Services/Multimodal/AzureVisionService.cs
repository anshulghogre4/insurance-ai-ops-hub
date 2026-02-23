using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SentimentAnalyzer.Agents.Configuration;
using SentimentAnalyzer.Agents.Orchestration;

namespace SentimentAnalyzer.API.Services.Multimodal;

/// <summary>
/// Image analysis service using Azure AI Vision (Computer Vision) F0 free tier.
/// Free tier: 5,000 transactions/month. Blocks at limit (429), never charges.
/// Insurance use case: analyzing claim damage photos for severity and content labels.
/// PII redaction is applied to AI-generated descriptions before returning to callers.
/// </summary>
public class AzureVisionService : IImageAnalysisService
{
    private readonly HttpClient _httpClient;
    private readonly AzureVisionSettings _settings;
    private readonly ILogger<AzureVisionService> _logger;
    private readonly IPIIRedactor? _piiRedactor;

    public AzureVisionService(
        HttpClient httpClient,
        IOptions<AgentSystemSettings> settings,
        ILogger<AzureVisionService> logger,
        IPIIRedactor? piiRedactor = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _settings = settings?.Value?.AzureVision ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _piiRedactor = piiRedactor;
    }

    /// <inheritdoc />
    public async Task<ImageAnalysisResult> AnalyzeImageAsync(
        byte[] imageData,
        string mimeType = "image/jpeg",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey) || string.IsNullOrWhiteSpace(_settings.Endpoint))
        {
            return new ImageAnalysisResult
            {
                IsSuccess = false,
                Provider = "AzureVision",
                ErrorMessage = "Azure Vision API key or endpoint not configured."
            };
        }

        try
        {
            var endpoint = _settings.Endpoint.TrimEnd('/');
            var requestUrl = $"{endpoint}/computervision/imageanalysis:analyze?api-version=2024-02-01&features=tags,caption,objects";

            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            request.Headers.Add("Ocp-Apim-Subscription-Key", _settings.ApiKey);
            request.Content = new ByteArrayContent(imageData);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(mimeType);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Azure Vision API returned {StatusCode}: {Error}", response.StatusCode, errorBody);
                return new ImageAnalysisResult
                {
                    IsSuccess = false,
                    Provider = "AzureVision",
                    ErrorMessage = $"Azure Vision API error: {response.StatusCode}"
                };
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var result = new ImageAnalysisResult
            {
                IsSuccess = true,
                Provider = "AzureVision"
            };

            // Extract caption and redact PII from AI-generated description
            if (root.TryGetProperty("captionResult", out var caption))
            {
                var rawDescription = caption.GetProperty("text").GetString() ?? string.Empty;
                result.Description = _piiRedactor?.Redact(rawDescription) ?? rawDescription;
                result.ConfidenceScore = caption.GetProperty("confidence").GetDouble();
            }

            // Extract tags
            if (root.TryGetProperty("tagsResult", out var tags) &&
                tags.TryGetProperty("values", out var tagValues))
            {
                foreach (var tag in tagValues.EnumerateArray())
                {
                    var name = tag.GetProperty("name").GetString() ?? string.Empty;
                    var confidence = tag.GetProperty("confidence").GetDouble();
                    result.Labels.Add(new ImageLabel { Name = name, Confidence = confidence });

                    // Detect insurance-relevant damage indicators
                    if (IsDamageIndicator(name))
                    {
                        result.DamageIndicators.Add(name);
                    }
                }
            }

            _logger.LogInformation("Azure Vision analysis completed. Labels: {Labels}, Damage indicators: {Damage}",
                result.Labels.Count, result.DamageIndicators.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure Vision analysis failed");
            return new ImageAnalysisResult
            {
                IsSuccess = false,
                Provider = "AzureVision",
                ErrorMessage = $"Image analysis error: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Checks if a tag name corresponds to an insurance damage indicator.
    /// </summary>
    private static bool IsDamageIndicator(string tagName)
    {
        var damageKeywords = new[]
        {
            "damage", "fire", "flood", "water", "crack", "broken", "dent", "scratch",
            "collision", "wreck", "debris", "mold", "rust", "leak", "storm", "hail",
            "vandalism", "theft", "wind", "foundation", "glass", "shatter", "tree",
            "smoke", "roof", "sinkhole", "lightning", "explosion", "sewage", "asbestos",
            "erosion", "corrosion", "collapse", "burst", "cave-in", "landslide"
        };
        var lowerTag = tagName.ToLowerInvariant();
        return damageKeywords.Any(keyword => lowerTag.Contains(keyword));
    }
}
