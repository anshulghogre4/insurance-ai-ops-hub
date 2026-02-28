using System.ComponentModel.DataAnnotations;

namespace SentimentAnalyzer.API.Data.Entities;

/// <summary>
/// Persisted conversation session for the CX Copilot.
/// Stores PII-redacted message history as a JSON array for sliding-window context.
/// Each session represents one continuous policyholder conversation.
/// </summary>
public class CxConversationRecord
{
    /// <summary>Primary key (auto-increment).</summary>
    [Key]
    public int Id { get; set; }

    /// <summary>Unique session identifier (GUID). Used to correlate messages across requests.</summary>
    [MaxLength(36)]
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// JSON-serialized array of <see cref="SentimentAnalyzer.API.Models.CxMessageRecord"/> objects.
    /// All content is PII-redacted before storage. Max 10 turns (sliding window).
    /// </summary>
    public string MessagesJson { get; set; } = "[]";

    /// <summary>UTC timestamp of the last message in this session.</summary>
    public DateTime LastActivityUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Number of conversational turns (user+assistant pairs) in this session.</summary>
    public int TurnCount { get; set; }
}
