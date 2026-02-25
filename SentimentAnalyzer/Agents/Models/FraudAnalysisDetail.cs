using System.Text.Json.Serialization;

namespace SentimentAnalyzer.Agents.Models;

/// <summary>
/// Fraud analysis output from the FraudDetection agent.
/// Matches the JSON schema defined in fraud-detection.md prompt.
/// </summary>
public class FraudAnalysisDetail
{
    /// <summary>Fraud probability score (0-100).</summary>
    [JsonPropertyName("fraudProbabilityScore")]
    [JsonConverter(typeof(FlexibleIntJsonConverter))]
    public int FraudProbabilityScore { get; set; }

    /// <summary>Risk level: VeryLow, Low, Medium, High, VeryHigh.</summary>
    [JsonPropertyName("riskLevel")]
    public string RiskLevel { get; set; } = "VeryLow";

    /// <summary>Fraud indicators grouped by category.</summary>
    [JsonPropertyName("indicators")]
    public List<FraudIndicator> Indicators { get; set; } = [];

    /// <summary>Recommended investigative actions.</summary>
    [JsonPropertyName("recommendedActions")]
    public List<RecommendedAction> RecommendedActions { get; set; } = [];

    /// <summary>Whether to refer to Special Investigation Unit.</summary>
    [JsonPropertyName("referToSIU")]
    public bool ReferToSIU { get; set; }

    /// <summary>Reason for SIU referral (if applicable).</summary>
    [JsonPropertyName("siuReferralReason")]
    public string SiuReferralReason { get; set; } = string.Empty;

    /// <summary>Agent's confidence in the fraud assessment (0.0-1.0).</summary>
    [JsonPropertyName("confidenceInAssessment")]
    public double ConfidenceInAssessment { get; set; }

    /// <summary>Additional notes from the fraud detection agent.</summary>
    [JsonPropertyName("additionalNotes")]
    public string AdditionalNotes { get; set; } = string.Empty;
}

/// <summary>
/// A fraud indicator with category, description, and severity.
/// </summary>
public class FraudIndicator
{
    /// <summary>Indicator category: Timing, Behavioral, Financial, Pattern, Documentation.</summary>
    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    /// <summary>Description of the fraud indicator.</summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>Indicator severity: Low, Medium, High.</summary>
    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "Low";
}
