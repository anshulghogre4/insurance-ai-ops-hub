namespace SentimentAnalyzer.API.Services.Multimodal;

/// <summary>
/// Analyzes images for content, labels, and damage assessment.
/// Insurance use case: claim damage photo analysis for severity and fraud detection.
/// </summary>
public interface IImageAnalysisService
{
    /// <summary>
    /// Analyzes an image and returns structured labels, descriptions, and damage indicators.
    /// </summary>
    /// <param name="imageData">Raw image bytes.</param>
    /// <param name="mimeType">MIME type (e.g., "image/jpeg", "image/png").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Image analysis result with labels and damage indicators.</returns>
    Task<ImageAnalysisResult> AnalyzeImageAsync(
        byte[] imageData,
        string mimeType = "image/jpeg",
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of an image analysis operation.
/// </summary>
public class ImageAnalysisResult
{
    /// <summary>Whether the analysis succeeded.</summary>
    public bool IsSuccess { get; set; }

    /// <summary>AI-generated description of the image.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Detected labels/tags in the image.</summary>
    public List<ImageLabel> Labels { get; set; } = [];

    /// <summary>Insurance-specific damage indicators detected.</summary>
    public List<string> DamageIndicators { get; set; } = [];

    /// <summary>Overall confidence score (0.0 to 1.0).</summary>
    public double ConfidenceScore { get; set; }

    /// <summary>Provider that performed the analysis.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Error message if analysis failed.</summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// A single label/tag detected in an image.
/// </summary>
public class ImageLabel
{
    /// <summary>Label name (e.g., "water damage", "fire", "vehicle").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Confidence score for this label (0.0 to 1.0).</summary>
    public double Confidence { get; set; }
}
