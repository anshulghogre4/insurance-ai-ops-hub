using System.ComponentModel.DataAnnotations;

namespace SentimentAnalyzer.API.Data.Entities;

/// <summary>
/// Audit trail record for Customer Experience Copilot interactions.
/// Required for regulatory compliance — all AI-generated customer communications must be logged.
/// </summary>
public class CxInteractionRecord
{
    /// <summary>Primary key.</summary>
    [Key]
    public int Id { get; set; }

    /// <summary>Hash of the original customer message (SHA-256). Never store raw PII.</summary>
    [MaxLength(64)]
    public string MessageHash { get; set; } = string.Empty;

    /// <summary>Length of the original message in characters.</summary>
    public int MessageLength { get; set; }

    /// <summary>The AI-generated response text (PII-redacted).</summary>
    [MaxLength(5000)]
    public string ResponseText { get; set; } = string.Empty;

    /// <summary>Detected tone of the response.</summary>
    [MaxLength(20)]
    public string Tone { get; set; } = "Professional";

    /// <summary>Whether escalation was recommended.</summary>
    public bool EscalationRecommended { get; set; }

    /// <summary>Reason for escalation, if applicable.</summary>
    [MaxLength(500)]
    public string? EscalationReason { get; set; }

    /// <summary>The LLM provider that generated the response.</summary>
    [MaxLength(50)]
    public string LlmProvider { get; set; } = string.Empty;

    /// <summary>Response time in milliseconds.</summary>
    public long ElapsedMilliseconds { get; set; }

    /// <summary>Whether claim context was provided with the interaction.</summary>
    public bool HasClaimContext { get; set; }

    /// <summary>Whether the interaction was streamed (SSE) or non-streaming JSON.</summary>
    public bool WasStreamed { get; set; }

    /// <summary>Timestamp of the interaction.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
