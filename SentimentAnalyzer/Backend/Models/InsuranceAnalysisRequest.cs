namespace SentimentAnalyzer.API.Models;

/// <summary>
/// Request model for insurance domain sentiment analysis.
/// </summary>
public class InsuranceAnalysisRequest
{
    /// <summary>The customer interaction text to analyze.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Type of interaction: General, Email, Call, Chat, Review, Complaint.</summary>
    public string InteractionType { get; set; } = "General";

    /// <summary>Optional customer identifier for trend tracking.</summary>
    public string? CustomerId { get; set; }
}
