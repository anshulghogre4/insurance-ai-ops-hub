namespace SentimentAnalyzer.API.Models;

/// <summary>
/// Response model for insurance domain sentiment analysis.
/// Extends v1 SentimentResponse with insurance-specific fields.
/// </summary>
public class InsuranceAnalysisResponse
{
    // --- Backward-compatible v1 fields ---

    /// <summary>Overall sentiment: Positive, Negative, Neutral, or Mixed.</summary>
    public string Sentiment { get; set; } = string.Empty;

    /// <summary>Confidence in the sentiment classification (0.0 to 1.0).</summary>
    public double ConfidenceScore { get; set; }

    /// <summary>AI-generated explanation of the analysis.</summary>
    public string Explanation { get; set; } = string.Empty;

    /// <summary>Emotion scores (e.g., frustration: 0.8, trust: 0.2).</summary>
    public Dictionary<string, double> EmotionBreakdown { get; set; } = new();

    // --- Insurance-specific v2 fields ---

    /// <summary>Insurance domain analysis details.</summary>
    public InsuranceAnalysisDetail InsuranceAnalysis { get; set; } = new();

    /// <summary>Quality validation metadata from QA agent.</summary>
    public QualityDetail Quality { get; set; } = new();
}

/// <summary>
/// Insurance-specific analysis detail.
/// </summary>
public class InsuranceAnalysisDetail
{
    /// <summary>Purchase intent score (0-100).</summary>
    public int PurchaseIntentScore { get; set; }

    /// <summary>Customer persona classification.</summary>
    public string CustomerPersona { get; set; } = string.Empty;

    /// <summary>Customer journey stage.</summary>
    public string JourneyStage { get; set; } = string.Empty;

    /// <summary>Risk assessment indicators.</summary>
    public RiskIndicatorDetail RiskIndicators { get; set; } = new();

    /// <summary>Recommended insurance products.</summary>
    public List<PolicyRecommendationDetail> PolicyRecommendations { get; set; } = [];

    /// <summary>Type of customer interaction.</summary>
    public string InteractionType { get; set; } = string.Empty;

    /// <summary>Key topics detected in the text.</summary>
    public List<string> KeyTopics { get; set; } = [];
}

/// <summary>
/// Risk indicator details.
/// </summary>
public class RiskIndicatorDetail
{
    public string ChurnRisk { get; set; } = "Low";
    public string ComplaintEscalationRisk { get; set; } = "Low";
    public string FraudIndicators { get; set; } = "None";
}

/// <summary>
/// Policy recommendation detail.
/// </summary>
public class PolicyRecommendationDetail
{
    public string Product { get; set; } = string.Empty;
    public string Reasoning { get; set; } = string.Empty;
}

/// <summary>
/// Quality validation metadata.
/// </summary>
public class QualityDetail
{
    public bool IsValid { get; set; } = true;
    public int QualityScore { get; set; } = 100;

    /// <summary>Structured quality issues from QA agent (severity, field, message).</summary>
    public List<QualityIssueDetail> Issues { get; set; } = [];

    /// <summary>Actionable suggestions from QA agent.</summary>
    public List<string> Suggestions { get; set; } = [];

    /// <summary>Flattened warnings for backward compatibility (issues + suggestions combined).</summary>
    public List<string> Warnings { get; set; } = [];
}

/// <summary>
/// A structured quality issue from the QA agent.
/// </summary>
public class QualityIssueDetail
{
    public string Severity { get; set; } = "info";
    public string Field { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
