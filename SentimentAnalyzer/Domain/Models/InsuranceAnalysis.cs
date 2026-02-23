using SentimentAnalyzer.Domain.Enums;

namespace SentimentAnalyzer.Domain.Models;

/// <summary>
/// Complete insurance domain analysis result from the multi-agent system.
/// </summary>
public class InsuranceAnalysis
{
    /// <summary>Overall sentiment classification.</summary>
    public SentimentType Sentiment { get; set; }

    /// <summary>Confidence in the sentiment classification (0.0 to 1.0).</summary>
    public double ConfidenceScore { get; set; }

    /// <summary>AI-generated explanation of the analysis.</summary>
    public string Explanation { get; set; } = string.Empty;

    /// <summary>Insurance-relevant emotion scores (e.g., frustration, trust, anxiety).</summary>
    public Dictionary<string, double> EmotionBreakdown { get; set; } = new();

    /// <summary>Purchase intent score (0-100).</summary>
    public int PurchaseIntentScore { get; set; }

    /// <summary>Classified customer persona.</summary>
    public CustomerPersonaType CustomerPersona { get; set; }

    /// <summary>Detected customer journey stage.</summary>
    public JourneyStage JourneyStage { get; set; }

    /// <summary>Risk assessment indicators.</summary>
    public RiskIndicators RiskIndicators { get; set; } = new();

    /// <summary>Recommended insurance products based on detected needs.</summary>
    public List<PolicyRecommendation> PolicyRecommendations { get; set; } = [];

    /// <summary>Type of customer interaction.</summary>
    public InteractionType InteractionType { get; set; }

    /// <summary>Key topics detected in the text.</summary>
    public List<string> KeyTopics { get; set; } = [];
}

/// <summary>
/// Risk assessment indicators for the customer interaction.
/// </summary>
public class RiskIndicators
{
    /// <summary>Likelihood of customer leaving.</summary>
    public RiskLevel ChurnRisk { get; set; } = RiskLevel.Low;

    /// <summary>Likelihood of formal complaint escalation.</summary>
    public RiskLevel ComplaintEscalationRisk { get; set; } = RiskLevel.Low;

    /// <summary>Suspicious language pattern indicators.</summary>
    public RiskLevel FraudIndicators { get; set; } = RiskLevel.None;
}
