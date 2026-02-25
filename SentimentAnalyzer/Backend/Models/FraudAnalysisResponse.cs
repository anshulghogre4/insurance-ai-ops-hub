namespace SentimentAnalyzer.API.Models;

/// <summary>
/// Response model for a fraud analysis result.
/// </summary>
public class FraudAnalysisResponse
{
    /// <summary>Claim ID that was analyzed.</summary>
    public int ClaimId { get; set; }

    /// <summary>Fraud probability score (0-100).</summary>
    public double FraudScore { get; set; }

    /// <summary>Risk level: VeryLow, Low, Medium, High, VeryHigh.</summary>
    public string RiskLevel { get; set; } = "VeryLow";

    /// <summary>Fraud indicators by category.</summary>
    public List<FraudIndicatorResponse> Indicators { get; set; } = [];

    /// <summary>Recommended investigative actions.</summary>
    public List<ClaimActionResponse> RecommendedActions { get; set; } = [];

    /// <summary>Whether SIU referral is recommended (score >= 75).</summary>
    public bool ReferToSIU { get; set; }

    /// <summary>Reason for SIU referral.</summary>
    public string SiuReferralReason { get; set; } = string.Empty;

    /// <summary>Confidence in the fraud assessment (0.0-1.0).</summary>
    public double Confidence { get; set; }
}

/// <summary>
/// A fraud indicator detail in the API response.
/// </summary>
public class FraudIndicatorResponse
{
    /// <summary>Category: Timing, Behavioral, Financial, Pattern, Documentation.</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Description of the indicator.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Severity: Low, Medium, High.</summary>
    public string Severity { get; set; } = "Low";
}
