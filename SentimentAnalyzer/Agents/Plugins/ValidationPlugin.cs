using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace SentimentAnalyzer.Agents.Plugins;

/// <summary>
/// Semantic Kernel plugin providing validation functions for the QA agent.
/// </summary>
public class ValidationPlugin
{
    /// <summary>
    /// Validates that a confidence score is within the valid range.
    /// </summary>
    [KernelFunction, Description("Validate that a confidence score is between 0.0 and 1.0")]
    public string ValidateConfidenceScore(double score)
    {
        if (score < 0.0 || score > 1.0)
            return $"INVALID: Confidence score {score} is out of range [0.0, 1.0]";
        return $"VALID: Confidence score {score} is within range";
    }

    /// <summary>
    /// Validates that a purchase intent score is within the valid range.
    /// </summary>
    [KernelFunction, Description("Validate that a purchase intent score is between 0 and 100")]
    public string ValidatePurchaseIntentScore(int score)
    {
        if (score < 0 || score > 100)
            return $"INVALID: Purchase intent score {score} is out of range [0, 100]";
        return $"VALID: Purchase intent score {score} is within range";
    }

    /// <summary>
    /// Checks for logical consistency between sentiment and risk indicators.
    /// </summary>
    [KernelFunction, Description("Check logical consistency between sentiment, purchase intent, and risk level")]
    public string CheckLogicalConsistency(string sentiment, int purchaseIntent, string churnRisk)
    {
        var issues = new List<string>();

        if (sentiment == "Positive" && churnRisk == "High")
            issues.Add("INCONSISTENCY: Positive sentiment with High churn risk is unusual");

        if (purchaseIntent > 60 && sentiment == "Negative")
            issues.Add("WARNING: High purchase intent with Negative sentiment needs explanation");

        if (purchaseIntent < 20 && sentiment == "Positive" && churnRisk == "Low")
            issues.Add("WARNING: Low purchase intent with Positive sentiment and Low churn may indicate non-purchase interaction");

        return issues.Count == 0
            ? "CONSISTENT: No logical inconsistencies detected"
            : string.Join("; ", issues);
    }
}
