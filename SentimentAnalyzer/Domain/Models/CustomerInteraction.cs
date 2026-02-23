using SentimentAnalyzer.Domain.Enums;

namespace SentimentAnalyzer.Domain.Models;

/// <summary>
/// Represents a customer interaction to be analyzed.
/// </summary>
public class CustomerInteraction
{
    /// <summary>The raw text of the customer interaction.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Type of interaction (email, call, chat, etc.).</summary>
    public InteractionType InteractionType { get; set; } = InteractionType.General;

    /// <summary>Optional customer identifier for trend tracking.</summary>
    public string? CustomerId { get; set; }

    /// <summary>Timestamp when the interaction occurred.</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
