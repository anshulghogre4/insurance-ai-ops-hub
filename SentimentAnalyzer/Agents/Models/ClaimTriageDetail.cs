using System.Text.Json.Serialization;

namespace SentimentAnalyzer.Agents.Models;

/// <summary>
/// Claims triage output from the ClaimsTriage agent.
/// Matches the JSON schema defined in claims-triage.md prompt.
/// </summary>
public class ClaimTriageDetail
{
    /// <summary>Claim severity: Critical, High, Medium, Low.</summary>
    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "Medium";

    /// <summary>Urgency classification: Immediate, Urgent, Standard, Low.</summary>
    [JsonPropertyName("urgency")]
    public string Urgency { get; set; } = "Standard";

    /// <summary>Claim type: Property, Auto, Liability, WorkersComp.</summary>
    [JsonPropertyName("claimType")]
    public string ClaimType { get; set; } = string.Empty;

    /// <summary>Claim sub-type for more specific classification.</summary>
    [JsonPropertyName("claimSubType")]
    public string ClaimSubType { get; set; } = string.Empty;

    /// <summary>Estimated loss range (e.g., "$5,000-$25,000").</summary>
    [JsonPropertyName("estimatedLossRange")]
    public string EstimatedLossRange { get; set; } = string.Empty;

    /// <summary>Recommended actions from the triage agent.</summary>
    [JsonPropertyName("recommendedActions")]
    public List<RecommendedAction> RecommendedActions { get; set; } = [];

    /// <summary>Preliminary fraud risk: None, Low, Medium, High.</summary>
    [JsonPropertyName("preliminaryFraudRisk")]
    public string PreliminaryFraudRisk { get; set; } = "None";

    /// <summary>Specific fraud flags identified during triage.</summary>
    [JsonPropertyName("fraudFlags")]
    public List<string> FraudFlags { get; set; } = [];

    /// <summary>Additional notes from the triage agent.</summary>
    [JsonPropertyName("additionalNotes")]
    public string AdditionalNotes { get; set; } = string.Empty;
}

/// <summary>
/// A recommended action from the claims triage or fraud detection agent.
/// </summary>
public class RecommendedAction
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("priority")]
    public string Priority { get; set; } = "Standard";

    [JsonPropertyName("reasoning")]
    public string Reasoning { get; set; } = string.Empty;
}
