using Azure;
using Azure.AI.ContentSafety;
using Microsoft.Extensions.Options;
using SentimentAnalyzer.Agents.Configuration;

namespace SentimentAnalyzer.API.Services.Multimodal;

/// <summary>
/// Azure AI Content Safety service for screening text and images for harmful content.
/// Free F0 tier: 5,000 text + 5,000 image analyses/month. Hard cap — 429 after limit.
/// Insurance use case: screens CX Copilot AI responses before delivering to policyholders,
/// preventing harmful, violent, self-harm, or sexually explicit content from reaching customers.
/// </summary>
public class AzureContentSafetyService : IContentSafetyService
{
    /// <summary>Safety threshold — severity scores at or above this value are flagged as unsafe.</summary>
    private const int SafetyThreshold = 2;

    private readonly AzureContentSafetySettings _settings;
    private readonly ILogger<AzureContentSafetyService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureContentSafetyService"/> class.
    /// </summary>
    /// <param name="settings">Agent system settings containing Azure Content Safety configuration.</param>
    /// <param name="logger">Logger instance for structured diagnostics.</param>
    public AzureContentSafetyService(
        IOptions<AgentSystemSettings> settings,
        ILogger<AzureContentSafetyService> logger)
    {
        _settings = settings?.Value?.AzureContentSafety ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<ContentSafetyResult> AnalyzeTextAsync(
        string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            return new ContentSafetyResult
            {
                IsSuccess = false,
                Provider = "AzureContentSafety",
                ErrorMessage = "Azure Content Safety API key not configured."
            };
        }

        if (string.IsNullOrWhiteSpace(_settings.Endpoint))
        {
            return new ContentSafetyResult
            {
                IsSuccess = false,
                Provider = "AzureContentSafety",
                ErrorMessage = "Azure Content Safety endpoint not configured."
            };
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return new ContentSafetyResult
            {
                IsSuccess = false,
                Provider = "AzureContentSafety",
                ErrorMessage = "Text content is empty or null."
            };
        }

        try
        {
            var client = new ContentSafetyClient(
                new Uri(_settings.Endpoint),
                new AzureKeyCredential(_settings.ApiKey));

            _logger.LogInformation(
                "Analyzing text content safety: {TextLength} chars",
                text.Length);

            var textOptions = new AnalyzeTextOptions(text);
            var response = await client.AnalyzeTextAsync(textOptions, cancellationToken);
            var analysisResult = response.Value;

            return MapCategoriesAnalysis(analysisResult.CategoriesAnalysis);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex,
                "Azure Content Safety text analysis failed with status {StatusCode}: {Message}",
                ex.Status, ex.Message);

            return new ContentSafetyResult
            {
                IsSuccess = false,
                Provider = "AzureContentSafety",
                ErrorMessage = $"Azure Content Safety API error (HTTP {ex.Status}): {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure Content Safety text analysis failed unexpectedly");

            return new ContentSafetyResult
            {
                IsSuccess = false,
                Provider = "AzureContentSafety",
                ErrorMessage = $"Azure Content Safety error: {ex.Message}"
            };
        }
    }

    /// <inheritdoc />
    public async Task<ContentSafetyResult> AnalyzeImageAsync(
        byte[] imageData, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            return new ContentSafetyResult
            {
                IsSuccess = false,
                Provider = "AzureContentSafety",
                ErrorMessage = "Azure Content Safety API key not configured."
            };
        }

        if (string.IsNullOrWhiteSpace(_settings.Endpoint))
        {
            return new ContentSafetyResult
            {
                IsSuccess = false,
                Provider = "AzureContentSafety",
                ErrorMessage = "Azure Content Safety endpoint not configured."
            };
        }

        if (imageData is null || imageData.Length == 0)
        {
            return new ContentSafetyResult
            {
                IsSuccess = false,
                Provider = "AzureContentSafety",
                ErrorMessage = "Image data is empty or null."
            };
        }

        try
        {
            var client = new ContentSafetyClient(
                new Uri(_settings.Endpoint),
                new AzureKeyCredential(_settings.ApiKey));

            _logger.LogInformation(
                "Analyzing image content safety: {ImageSize} bytes",
                imageData.Length);

            var contentSafetyImageData = new ContentSafetyImageData(BinaryData.FromBytes(imageData));
            var imageOptions = new AnalyzeImageOptions(contentSafetyImageData);
            var response = await client.AnalyzeImageAsync(imageOptions, cancellationToken);
            var analysisResult = response.Value;

            return MapCategoriesAnalysis(analysisResult.CategoriesAnalysis);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex,
                "Azure Content Safety image analysis failed with status {StatusCode}: {Message}",
                ex.Status, ex.Message);

            return new ContentSafetyResult
            {
                IsSuccess = false,
                Provider = "AzureContentSafety",
                ErrorMessage = $"Azure Content Safety API error (HTTP {ex.Status}): {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure Content Safety image analysis failed unexpectedly");

            return new ContentSafetyResult
            {
                IsSuccess = false,
                Provider = "AzureContentSafety",
                ErrorMessage = $"Azure Content Safety error: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Maps the Azure Content Safety categories analysis to our unified <see cref="ContentSafetyResult"/> model.
    /// Each category has a severity score from 0-6. Scores >= 2 are flagged as unsafe.
    /// TextCategory/ImageCategory are extensible enums (struct types) — use equality, not switch.
    /// </summary>
    /// <param name="categoriesAnalysis">The category analysis results from Azure Content Safety.</param>
    /// <returns>A unified content safety result with severity scores and flagged categories.</returns>
    private static ContentSafetyResult MapCategoriesAnalysis(
        IReadOnlyList<TextCategoriesAnalysis> categoriesAnalysis)
    {
        var result = new ContentSafetyResult
        {
            IsSuccess = true,
            Provider = "AzureContentSafety"
        };

        foreach (var category in categoriesAnalysis)
        {
            var severity = category.Severity ?? 0;
            var cat = category.Category;

            if (cat == TextCategory.Hate)
            {
                result.HateSeverity = severity;
                if (severity >= SafetyThreshold)
                    result.FlaggedCategories.Add("Hate");
            }
            else if (cat == TextCategory.Violence)
            {
                result.ViolenceSeverity = severity;
                if (severity >= SafetyThreshold)
                    result.FlaggedCategories.Add("Violence");
            }
            else if (cat == TextCategory.SelfHarm)
            {
                result.SelfHarmSeverity = severity;
                if (severity >= SafetyThreshold)
                    result.FlaggedCategories.Add("SelfHarm");
            }
            else if (cat == TextCategory.Sexual)
            {
                result.SexualSeverity = severity;
                if (severity >= SafetyThreshold)
                    result.FlaggedCategories.Add("Sexual");
            }
        }

        result.IsSafe = result.FlaggedCategories.Count == 0;

        return result;
    }

    /// <summary>
    /// Maps the Azure Content Safety image categories analysis to our unified <see cref="ContentSafetyResult"/> model.
    /// Overload for image analysis results which use <see cref="ImageCategoriesAnalysis"/>.
    /// </summary>
    /// <param name="categoriesAnalysis">The image category analysis results from Azure Content Safety.</param>
    /// <returns>A unified content safety result with severity scores and flagged categories.</returns>
    private static ContentSafetyResult MapCategoriesAnalysis(
        IReadOnlyList<ImageCategoriesAnalysis> categoriesAnalysis)
    {
        var result = new ContentSafetyResult
        {
            IsSuccess = true,
            Provider = "AzureContentSafety"
        };

        foreach (var category in categoriesAnalysis)
        {
            var severity = category.Severity ?? 0;
            var cat = category.Category;

            if (cat == ImageCategory.Hate)
            {
                result.HateSeverity = severity;
                if (severity >= SafetyThreshold)
                    result.FlaggedCategories.Add("Hate");
            }
            else if (cat == ImageCategory.Violence)
            {
                result.ViolenceSeverity = severity;
                if (severity >= SafetyThreshold)
                    result.FlaggedCategories.Add("Violence");
            }
            else if (cat == ImageCategory.SelfHarm)
            {
                result.SelfHarmSeverity = severity;
                if (severity >= SafetyThreshold)
                    result.FlaggedCategories.Add("SelfHarm");
            }
            else if (cat == ImageCategory.Sexual)
            {
                result.SexualSeverity = severity;
                if (severity >= SafetyThreshold)
                    result.FlaggedCategories.Add("Sexual");
            }
        }

        result.IsSafe = result.FlaggedCategories.Count == 0;

        return result;
    }
}
