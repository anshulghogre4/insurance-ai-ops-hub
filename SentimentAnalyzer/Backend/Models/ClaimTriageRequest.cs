namespace SentimentAnalyzer.API.Models;

/// <summary>
/// Request model for submitting a claim for triage assessment.
/// </summary>
public class ClaimTriageRequest
{
    /// <summary>Claim description text (required, 1-10,000 characters).</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Interaction type for context (optional, defaults to "Complaint").</summary>
    public string InteractionType { get; set; } = "Complaint";
}
