namespace SentimentAnalyzer.API.Models;

/// <summary>
/// Response model for processed multimodal evidence attached to a claim.
/// </summary>
public class ClaimEvidenceResponse
{
    /// <summary>Type of evidence: image, audio, document.</summary>
    public string EvidenceType { get; set; } = string.Empty;

    /// <summary>Multimodal service that processed the evidence.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Processed text output (transcription, OCR text, image description).</summary>
    public string ProcessedText { get; set; } = string.Empty;

    /// <summary>Damage indicators detected by vision services.</summary>
    public List<string> DamageIndicators { get; set; } = [];

    /// <summary>When the evidence was processed.</summary>
    public DateTime CreatedAt { get; set; }
}
