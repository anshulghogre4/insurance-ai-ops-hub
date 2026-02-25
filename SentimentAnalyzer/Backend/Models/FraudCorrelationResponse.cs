namespace SentimentAnalyzer.API.Models;

/// <summary>
/// API response model for a fraud correlation between two claims.
/// Includes summary data from both source and correlated claims for display in dashboards.
/// </summary>
public class FraudCorrelationResponse
{
    /// <summary>Unique identifier for this correlation record.</summary>
    public int Id { get; set; }

    /// <summary>ID of the source claim that initiated the correlation analysis.</summary>
    public int SourceClaimId { get; set; }

    /// <summary>ID of the claim found to correlate with the source claim.</summary>
    public int CorrelatedClaimId { get; set; }

    /// <summary>
    /// Raw composite correlation type string (e.g., "DateProximity+SharedFlags+SameSeverity").
    /// </summary>
    public string CorrelationType { get; set; } = string.Empty;

    /// <summary>
    /// Parsed array of individual correlation types from the composite string.
    /// Example: ["DateProximity", "SharedFlags", "SameSeverity"].
    /// </summary>
    public string[] CorrelationTypes { get; set; } = [];

    /// <summary>Correlation confidence score from 0.0 to 1.0.</summary>
    public double CorrelationScore { get; set; }

    /// <summary>Human-readable description of the correlation finding.</summary>
    public string Details { get; set; } = string.Empty;

    /// <summary>Severity of the source claim (Critical, High, Medium, Low) for context.</summary>
    public string? SourceClaimSeverity { get; set; }

    /// <summary>Claim type of the source claim (Property, Auto, Liability, WorkersComp) for context.</summary>
    public string? SourceClaimType { get; set; }

    /// <summary>Fraud score of the source claim (0-100) for context.</summary>
    public double? SourceFraudScore { get; set; }

    /// <summary>Severity of the correlated claim (Critical, High, Medium, Low) for display.</summary>
    public string? CorrelatedClaimSeverity { get; set; }

    /// <summary>Claim type of the correlated claim (Property, Auto, Liability, WorkersComp) for display.</summary>
    public string? CorrelatedClaimType { get; set; }

    /// <summary>Fraud score of the correlated claim (0-100) for display.</summary>
    public double? CorrelatedFraudScore { get; set; }

    /// <summary>Timestamp when this correlation was detected.</summary>
    public DateTime DetectedAt { get; set; }

    /// <summary>Review status: Pending, Confirmed, or Dismissed.</summary>
    public string Status { get; set; } = "Pending";

    /// <summary>Identity of the analyst who reviewed this correlation (null if unreviewed).</summary>
    public string? ReviewedBy { get; set; }

    /// <summary>Timestamp when this correlation was reviewed (null if unreviewed).</summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>Reason for dismissal if the correlation was a false positive (null otherwise).</summary>
    public string? DismissalReason { get; set; }
}
