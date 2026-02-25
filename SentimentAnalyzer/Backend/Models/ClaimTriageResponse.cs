namespace SentimentAnalyzer.API.Models;

/// <summary>
/// Response model for a completed claims triage assessment.
/// </summary>
public class ClaimTriageResponse
{
    /// <summary>Unique claim identifier.</summary>
    public int ClaimId { get; set; }

    /// <summary>Triage severity: Critical, High, Medium, Low.</summary>
    public string Severity { get; set; } = string.Empty;

    /// <summary>Urgency classification: Immediate, Urgent, Standard, Low.</summary>
    public string Urgency { get; set; } = string.Empty;

    /// <summary>Claim type: Property, Auto, Liability, WorkersComp.</summary>
    public string ClaimType { get; set; } = string.Empty;

    /// <summary>Fraud probability score (0-100).</summary>
    public double FraudScore { get; set; }

    /// <summary>Fraud risk level: VeryLow, Low, Medium, High, VeryHigh.</summary>
    public string FraudRiskLevel { get; set; } = "VeryLow";

    /// <summary>Estimated loss range from triage agent.</summary>
    public string EstimatedLossRange { get; set; } = string.Empty;

    /// <summary>Recommended actions from the triage pipeline.</summary>
    public List<ClaimActionResponse> RecommendedActions { get; set; } = [];

    /// <summary>Fraud flags identified during triage.</summary>
    public List<string> FraudFlags { get; set; } = [];

    /// <summary>Processed evidence attached to this claim.</summary>
    public List<ClaimEvidenceResponse> Evidence { get; set; } = [];

    /// <summary>Current claim processing status.</summary>
    public string Status { get; set; } = "Triaged";

    /// <summary>When the claim was submitted.</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Response model for a recommended claim action.
/// </summary>
public class ClaimActionResponse
{
    public string Action { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Reasoning { get; set; } = string.Empty;
}
