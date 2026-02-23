namespace SentimentAnalyzer.Domain.Enums;

/// <summary>
/// Type of customer interaction being analyzed.
/// </summary>
public enum InteractionType
{
    General,
    Email,
    Call,
    Chat,
    Review,
    Complaint
}
