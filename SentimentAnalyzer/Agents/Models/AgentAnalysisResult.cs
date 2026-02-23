using System.Text.Json;
using System.Text.Json.Serialization;

namespace SentimentAnalyzer.Agents.Models;

/// <summary>
/// Complete result from the multi-agent insurance analysis pipeline.
/// </summary>
public class AgentAnalysisResult
{
    /// <summary>Whether the multi-agent analysis completed successfully.</summary>
    [JsonIgnore]
    public bool IsSuccess { get; set; }

    /// <summary>Raw agent conversation log for debugging.</summary>
    [JsonIgnore]
    public string RawAgentConversation { get; set; } = string.Empty;

    // --- Backward-compatible v1 fields ---

    /// <summary>Overall sentiment: Positive, Negative, Neutral, or Mixed.</summary>
    [JsonPropertyName("sentiment")]
    public string Sentiment { get; set; } = "Neutral";

    /// <summary>Confidence in the sentiment classification (0.0 to 1.0).</summary>
    [JsonPropertyName("confidenceScore")]
    public double ConfidenceScore { get; set; }

    /// <summary>AI-generated explanation of the analysis.</summary>
    [JsonPropertyName("explanation")]
    public string Explanation { get; set; } = string.Empty;

    /// <summary>Emotion scores (e.g., frustration: 0.8, trust: 0.2).</summary>
    [JsonPropertyName("emotionBreakdown")]
    public Dictionary<string, double> EmotionBreakdown { get; set; } = new();

    // --- Insurance-specific v2 fields ---

    /// <summary>Insurance domain analysis details.</summary>
    [JsonPropertyName("insuranceAnalysis")]
    public InsuranceAnalysisDetail InsuranceAnalysis { get; set; } = new();

    /// <summary>Quality validation metadata from QA agent.</summary>
    [JsonPropertyName("quality")]
    public QualityMetadata? Quality { get; set; }
}

/// <summary>
/// Insurance-specific analysis detail from the BA agent.
/// </summary>
public class InsuranceAnalysisDetail
{
    /// <summary>Purchase intent score (0-100).</summary>
    [JsonPropertyName("purchaseIntentScore")]
    [JsonConverter(typeof(FlexibleIntJsonConverter))]
    public int PurchaseIntentScore { get; set; }

    /// <summary>Customer persona classification.</summary>
    [JsonPropertyName("customerPersona")]
    public string CustomerPersona { get; set; } = "NewBuyer";

    /// <summary>Customer journey stage.</summary>
    [JsonPropertyName("journeyStage")]
    public string JourneyStage { get; set; } = "Awareness";

    /// <summary>Risk assessment indicators.</summary>
    [JsonPropertyName("riskIndicators")]
    public RiskIndicatorDetail RiskIndicators { get; set; } = new();

    /// <summary>Recommended insurance products.</summary>
    [JsonPropertyName("policyRecommendations")]
    public List<PolicyRecommendationDetail> PolicyRecommendations { get; set; } = [];

    /// <summary>Type of customer interaction.</summary>
    [JsonPropertyName("interactionType")]
    public string InteractionType { get; set; } = "General";

    /// <summary>Key topics detected in the text.</summary>
    [JsonPropertyName("keyTopics")]
    public List<string> KeyTopics { get; set; } = [];
}

/// <summary>
/// Risk indicator details from the analysis.
/// </summary>
public class RiskIndicatorDetail
{
    [JsonPropertyName("churnRisk")]
    public string ChurnRisk { get; set; } = "Low";

    [JsonPropertyName("complaintEscalationRisk")]
    public string ComplaintEscalationRisk { get; set; } = "Low";

    [JsonPropertyName("fraudIndicators")]
    public string FraudIndicators { get; set; } = "None";
}

/// <summary>
/// A recommended insurance policy from the BA agent.
/// </summary>
public class PolicyRecommendationDetail
{
    [JsonPropertyName("product")]
    public string Product { get; set; } = string.Empty;

    [JsonPropertyName("reasoning")]
    public string Reasoning { get; set; } = string.Empty;
}

/// <summary>
/// Quality validation metadata from the QA agent.
/// </summary>
public class QualityMetadata
{
    [JsonPropertyName("isValid")]
    public bool IsValid { get; set; } = true;

    [JsonPropertyName("qualityScore")]
    [JsonConverter(typeof(FlexibleIntJsonConverter))]
    public int QualityScore { get; set; } = 100;

    [JsonPropertyName("issues")]
    public List<QualityIssue> Issues { get; set; } = [];

    [JsonPropertyName("suggestions")]
    public List<string> Suggestions { get; set; } = [];
}

/// <summary>
/// A quality issue found by the QA agent.
/// </summary>
public class QualityIssue
{
    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "info";

    [JsonPropertyName("field")]
    public string Field { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Custom JSON converter that handles LLM output where integer fields (0-100 range)
/// may arrive as decimals in 0-1 range, decimals in 0-100 range, or string representations.
/// </summary>
public class FlexibleIntJsonConverter : JsonConverter<int>
{
    /// <inheritdoc />
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Number => NormalizeToInt100(reader.GetDouble()),
            JsonTokenType.String when int.TryParse(reader.GetString(), out var i) => i,
            JsonTokenType.String when double.TryParse(reader.GetString(), out var d) => NormalizeToInt100(d),
            _ => 0
        };
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }

    private static int NormalizeToInt100(double value)
    {
        if (value > 0.0 && value < 1.0)
            return (int)Math.Round(value * 100);
        return (int)Math.Round(Math.Clamp(value, 0, 100));
    }
}
