namespace SentimentAnalyzer.API.Models;

public class SentimentResponse
{
    public string Sentiment { get; set; } = string.Empty; // Positive, Negative, Neutral
    public double ConfidenceScore { get; set; }
    public string Explanation { get; set; } = string.Empty;
    public Dictionary<string, double> EmotionBreakdown { get; set; } = new();
}
