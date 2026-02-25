using System.ComponentModel.DataAnnotations;

namespace SentimentAnalyzer.API.Data.Entities;

/// <summary>
/// EF Core entity representing a recommended action from the claims triage agent.
/// Each action has a priority and tracks resolution status.
/// </summary>
public class ClaimActionRecord
{
    [Key]
    public int Id { get; set; }

    /// <summary>Foreign key to the parent claim.</summary>
    public int ClaimId { get; set; }

    /// <summary>Recommended action description.</summary>
    [MaxLength(500)]
    public string Action { get; set; } = string.Empty;

    /// <summary>Action priority: Immediate, High, Standard, Low.</summary>
    [MaxLength(20)]
    public string Priority { get; set; } = "Standard";

    /// <summary>Reasoning behind this recommendation.</summary>
    [MaxLength(1000)]
    public string Reasoning { get; set; } = string.Empty;

    /// <summary>Action status: Open, InProgress, Completed, Dismissed.</summary>
    [MaxLength(20)]
    public string Status { get; set; } = "Open";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    /// <summary>Navigation property to parent claim.</summary>
    public ClaimRecord? Claim { get; set; }
}
