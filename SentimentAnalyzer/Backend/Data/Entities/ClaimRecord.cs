using System.ComponentModel.DataAnnotations;

namespace SentimentAnalyzer.API.Data.Entities;

/// <summary>
/// EF Core entity representing a claims triage result.
/// Stores severity, urgency, fraud scoring, and full triage JSON output.
/// </summary>
public class ClaimRecord
{
    [Key]
    public int Id { get; set; }

    /// <summary>Original claim description text (truncated to 5000 chars).</summary>
    [MaxLength(5000)]
    public string ClaimText { get; set; } = string.Empty;

    /// <summary>Triage severity: Critical, High, Medium, Low.</summary>
    [MaxLength(20)]
    public string Severity { get; set; } = string.Empty;

    /// <summary>Urgency classification: Immediate, Urgent, Standard, Low.</summary>
    [MaxLength(20)]
    public string Urgency { get; set; } = string.Empty;

    /// <summary>Claim type: Property, Auto, Liability, WorkersComp.</summary>
    [MaxLength(30)]
    public string ClaimType { get; set; } = string.Empty;

    /// <summary>Fraud probability score from 0-100.</summary>
    public double FraudScore { get; set; }

    /// <summary>Fraud risk level: VeryLow, Low, Medium, High, VeryHigh.</summary>
    [MaxLength(20)]
    public string FraudRiskLevel { get; set; } = "VeryLow";

    /// <summary>Claim processing status: Submitted, Triaging, Triaged, UnderReview, Resolved.</summary>
    [MaxLength(20)]
    public string Status { get; set; } = "Submitted";

    /// <summary>Full agent triage output serialized as JSON. Preserves complete analysis.</summary>
    [MaxLength(10000)]
    public string TriageJson { get; set; } = "{}";

    /// <summary>Full agent fraud analysis output serialized as JSON.</summary>
    [MaxLength(10000)]
    public string FraudAnalysisJson { get; set; } = "{}";

    /// <summary>JSON-serialized list of fraud flag strings.</summary>
    [MaxLength(2000)]
    public string FraudFlagsJson { get; set; } = "[]";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    /// <summary>Navigation property for associated evidence.</summary>
    public List<ClaimEvidenceRecord> Evidence { get; set; } = [];

    /// <summary>Navigation property for recommended actions.</summary>
    public List<ClaimActionRecord> Actions { get; set; } = [];
}
