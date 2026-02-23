namespace SentimentAnalyzer.Domain.Models;

/// <summary>
/// A recommended insurance product based on customer analysis.
/// </summary>
public class PolicyRecommendation
{
    /// <summary>Name of the insurance product (e.g., "Health Gold Plan", "Auto Comprehensive").</summary>
    public string Product { get; set; } = string.Empty;

    /// <summary>Reasoning for why this product is recommended based on the analysis.</summary>
    public string Reasoning { get; set; } = string.Empty;
}
