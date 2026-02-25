using System.ComponentModel.DataAnnotations;

namespace SentimentAnalyzer.API.Data.Entities;

/// <summary>
/// EF Core entity representing a persisted insurance analysis result.
/// </summary>
public class AnalysisRecord
{
    [Key]
    public int Id { get; set; }

    /// <summary>Original customer text (PII-redacted, truncated to 10000 chars for storage).</summary>
    [MaxLength(10000)]
    public string InputText { get; set; } = string.Empty;

    [MaxLength(20)]
    public string InteractionType { get; set; } = "General";

    [MaxLength(100)]
    public string? CustomerId { get; set; }

    // Sentiment fields
    [MaxLength(20)]
    public string Sentiment { get; set; } = string.Empty;

    public double ConfidenceScore { get; set; }

    [MaxLength(5000)]
    public string Explanation { get; set; } = string.Empty;

    // Insurance-specific fields
    public int PurchaseIntentScore { get; set; }

    [MaxLength(30)]
    public string CustomerPersona { get; set; } = string.Empty;

    [MaxLength(30)]
    public string JourneyStage { get; set; } = string.Empty;

    [MaxLength(10)]
    public string ChurnRisk { get; set; } = "Low";

    [MaxLength(10)]
    public string ComplaintEscalationRisk { get; set; } = "Low";

    [MaxLength(10)]
    public string FraudIndicators { get; set; } = "None";

    /// <summary>Comma-separated key topics.</summary>
    [MaxLength(500)]
    public string KeyTopics { get; set; } = string.Empty;

    /// <summary>JSON-serialized policy recommendations.</summary>
    [MaxLength(5000)]
    public string PolicyRecommendationsJson { get; set; } = "[]";

    /// <summary>JSON-serialized emotion breakdown.</summary>
    [MaxLength(1000)]
    public string EmotionBreakdownJson { get; set; } = "{}";

    // Quality
    public bool IsValid { get; set; } = true;
    public int QualityScore { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
