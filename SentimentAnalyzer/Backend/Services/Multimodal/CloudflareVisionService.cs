using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SentimentAnalyzer.Agents.Configuration;
using SentimentAnalyzer.Agents.Orchestration;

namespace SentimentAnalyzer.API.Services.Multimodal;

/// <summary>
/// Image analysis service using Cloudflare Workers AI (multimodal vision model).
/// Free tier: 10,000 neurons/day.
/// Insurance use case: analyzing claim photos with natural language prompts
/// for damage assessment and fraud detection.
/// PII redaction is applied to AI-generated descriptions before returning to callers.
/// </summary>
public class CloudflareVisionService : IImageAnalysisService
{
    private readonly HttpClient _httpClient;
    private readonly CloudflareSettings _settings;
    private readonly ILogger<CloudflareVisionService> _logger;
    private readonly IPIIRedactor? _piiRedactor;

    public CloudflareVisionService(
        HttpClient httpClient,
        IOptions<AgentSystemSettings> settings,
        ILogger<CloudflareVisionService> logger,
        IPIIRedactor? piiRedactor = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _settings = settings?.Value?.Cloudflare ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _piiRedactor = piiRedactor;
    }

    /// <inheritdoc />
    public async Task<ImageAnalysisResult> AnalyzeImageAsync(
        byte[] imageData,
        string mimeType = "image/jpeg",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey) || string.IsNullOrWhiteSpace(_settings.AccountId))
        {
            return new ImageAnalysisResult
            {
                IsSuccess = false,
                Provider = "CloudflareVision",
                ErrorMessage = "Cloudflare API key or Account ID not configured."
            };
        }

        try
        {
            var requestUrl = $"https://api.cloudflare.com/client/v4/accounts/{_settings.AccountId}/ai/run/{_settings.VisionModel}";
            var base64Image = Convert.ToBase64String(imageData);

            var payload = new
            {
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "text", text = "Analyze this image for insurance purposes. Describe what you see, identify any damage, and list specific items or conditions visible. Focus on: damage type, severity, affected items, and any safety concerns." },
                            new { type = "image_url", image_url = new { url = $"data:{mimeType};base64,{base64Image}" } }
                        }
                    }
                },
                max_tokens = 512
            };

            var json = JsonSerializer.Serialize(payload);
            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            request.Headers.Add("Authorization", $"Bearer {_settings.ApiKey}");
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Cloudflare Vision API returned {StatusCode}: {Error}", response.StatusCode, errorBody);
                return new ImageAnalysisResult
                {
                    IsSuccess = false,
                    Provider = "CloudflareVision",
                    ErrorMessage = $"Cloudflare Vision API error: {response.StatusCode}"
                };
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            var description = string.Empty;
            if (root.TryGetProperty("result", out var resultEl))
            {
                if (resultEl.TryGetProperty("response", out var responseContent))
                {
                    var rawDescription = responseContent.GetString() ?? string.Empty;
                    description = _piiRedactor?.Redact(rawDescription) ?? rawDescription;
                }
            }

            // Extract damage indicators from the AI description
            var damageIndicators = ExtractDamageIndicators(description);

            _logger.LogInformation("Cloudflare Vision analysis completed. Description length: {Length}, Damage indicators: {Damage}",
                description.Length, damageIndicators.Count);

            return new ImageAnalysisResult
            {
                IsSuccess = true,
                Description = description,
                DamageIndicators = damageIndicators,
                ConfidenceScore = 0.8, // Cloudflare doesn't return confidence, use reasonable default
                Provider = "CloudflareVision"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cloudflare Vision analysis failed");
            return new ImageAnalysisResult
            {
                IsSuccess = false,
                Provider = "CloudflareVision",
                ErrorMessage = $"Image analysis error: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Extracts insurance-relevant damage indicators from the AI description.
    /// </summary>
    private static List<string> ExtractDamageIndicators(string description)
    {
        var indicators = new List<string>();
        var lowerDesc = description.ToLowerInvariant();

        var damageTerms = new Dictionary<string, string>
        {
            ["water damage"] = "Water Damage",
            ["fire damage"] = "Fire Damage",
            ["flood"] = "Flood Damage",
            ["mold"] = "Mold",
            ["crack"] = "Structural Crack",
            ["broken"] = "Broken/Shattered",
            ["shatter"] = "Broken/Shattered",
            ["dent"] = "Dent",
            ["scratch"] = "Scratch",
            ["collision"] = "Collision Damage",
            ["storm"] = "Storm Damage",
            ["hail"] = "Hail Damage",
            ["roof damage"] = "Roof Damage",
            ["structural"] = "Structural Damage",
            ["leak"] = "Water Leak",
            ["rust"] = "Corrosion/Rust",
            ["corrosion"] = "Corrosion/Rust",
            ["smoke"] = "Smoke Damage",
            ["vandalism"] = "Vandalism",
            ["theft"] = "Theft",
            ["wind"] = "Wind Damage",
            ["foundation"] = "Foundation Damage",
            ["glass"] = "Glass Breakage",
            ["tree"] = "Tree/Debris Impact",
            ["sinkhole"] = "Sinkhole",
            ["lightning"] = "Lightning Strike",
            ["explosion"] = "Explosion",
            ["sewage"] = "Sewage/Backup",
            ["asbestos"] = "Asbestos Exposure",
            ["collapse"] = "Structural Collapse",
            ["burst"] = "Pipe Burst",
            ["landslide"] = "Landslide/Erosion"
        };

        foreach (var (keyword, label) in damageTerms)
        {
            if (lowerDesc.Contains(keyword))
            {
                indicators.Add(label);
            }
        }

        return indicators;
    }
}
