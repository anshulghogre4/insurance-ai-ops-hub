using System.ComponentModel.DataAnnotations;

namespace SentimentAnalyzer.API.Data.Entities;

/// <summary>
/// EF Core entity representing a discovered fraud correlation between two claims.
/// Stores the correlation type, confidence score, and descriptive details.
/// Navigation properties use no FK constraint so claims can be deleted independently.
/// </summary>
public class FraudCorrelationRecord
{
    /// <summary>Primary key.</summary>
    [Key]
    public int Id { get; set; }

    /// <summary>ID of the source claim that initiated the correlation analysis.</summary>
    public int SourceClaimId { get; set; }

    /// <summary>ID of the claim correlated with the source claim.</summary>
    public int CorrelatedClaimId { get; set; }

    /// <summary>
    /// Composite type of correlation detected. Multiple types are joined with '+'.
    /// Examples: "DateProximity+SharedFlags", "DateProximity+SharedFlags+SameSeverity+SimilarNarrative".
    /// </summary>
    [MaxLength(100)]
    public string CorrelationType { get; set; } = string.Empty;

    /// <summary>Correlation confidence score from 0.0 to 1.0.</summary>
    public double CorrelationScore { get; set; }

    /// <summary>Human-readable description of the correlation finding.</summary>
    [MaxLength(500)]
    public string Details { get; set; } = string.Empty;

    /// <summary>Timestamp when this correlation was first detected.</summary>
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Review status: Pending (default), Confirmed (analyst verified), Dismissed (false positive).
    /// </summary>
    [MaxLength(20)]
    public string Status { get; set; } = "Pending";

    /// <summary>Identity of the analyst who reviewed this correlation (null if unreviewed).</summary>
    [MaxLength(100)]
    public string? ReviewedBy { get; set; }

    /// <summary>Timestamp when this correlation was reviewed (null if unreviewed).</summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>Reason for dismissal if the correlation was a false positive (null otherwise).</summary>
    [MaxLength(500)]
    public string? DismissalReason { get; set; }

    /// <summary>
    /// Navigation property to the source claim. No FK constraint — claims can be deleted independently.
    /// </summary>
    public ClaimRecord? SourceClaim { get; set; }

    /// <summary>
    /// Navigation property to the correlated claim. No FK constraint — claims can be deleted independently.
    /// </summary>
    public ClaimRecord? CorrelatedClaim { get; set; }
}
