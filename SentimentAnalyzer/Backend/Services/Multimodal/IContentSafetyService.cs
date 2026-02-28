namespace SentimentAnalyzer.API.Services.Multimodal;

/// <summary>
/// Screens text and images for harmful content before sending to policyholders.
/// Insurance use case: ensures CX Copilot responses don't contain harmful, violent,
/// self-harm, or sexually explicit content before reaching customers.
/// </summary>
public interface IContentSafetyService
{
    /// <summary>
    /// Analyzes text content for safety violations.
    /// </summary>
    Task<ContentSafetyResult> AnalyzeTextAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes image content for safety violations.
    /// </summary>
    Task<ContentSafetyResult> AnalyzeImageAsync(byte[] imageData, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a content safety analysis.
/// </summary>
public class ContentSafetyResult
{
    /// <summary>Whether the analysis completed successfully.</summary>
    public bool IsSuccess { get; set; }

    /// <summary>Whether the content is safe (all severity scores below threshold of 2).</summary>
    public bool IsSafe { get; set; } = true;

    /// <summary>Hate speech severity (0-6, safe below 2).</summary>
    public int HateSeverity { get; set; }

    /// <summary>Violence severity (0-6, safe below 2).</summary>
    public int ViolenceSeverity { get; set; }

    /// <summary>Self-harm severity (0-6, safe below 2).</summary>
    public int SelfHarmSeverity { get; set; }

    /// <summary>Sexual content severity (0-6, safe below 2).</summary>
    public int SexualSeverity { get; set; }

    /// <summary>Categories that were flagged as unsafe.</summary>
    public List<string> FlaggedCategories { get; set; } = [];

    /// <summary>Provider that performed the analysis.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Error message if analysis failed.</summary>
    public string? ErrorMessage { get; set; }
}
