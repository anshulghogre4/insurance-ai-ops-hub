using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace SentimentAnalyzer.Agents.Plugins;

/// <summary>
/// Semantic Kernel plugin that provides sentiment analysis reference information.
/// Used by agents to access domain-specific sentiment classification guidelines.
/// </summary>
public class SentimentAnalysisPlugin
{
    /// <summary>
    /// Returns the sentiment classification guidelines for insurance domain.
    /// </summary>
    [KernelFunction, Description("Get insurance-domain sentiment classification guidelines")]
    public string GetSentimentGuidelines()
    {
        return """
            Insurance Sentiment Classification Guidelines:

            POSITIVE indicators:
            - Expressing satisfaction with service/coverage
            - Recommending to others, mentioning good experiences
            - Eager to purchase/renew, asking about additional products
            - Gratitude for claim handling, relief at coverage

            NEGATIVE indicators:
            - Frustration with claims process, delays, denials
            - Complaints about premium increases, hidden fees
            - Threats to switch providers, cancel policy
            - Mentions of legal action, regulatory complaints
            - Anger at customer service, lack of communication

            NEUTRAL indicators:
            - Purely informational questions
            - Requesting quotes without emotional language
            - Administrative requests (address change, ID cards)
            - Factual descriptions without sentiment markers

            MIXED indicators:
            - Satisfied with product but frustrated with price
            - Happy with claim outcome but unhappy with timeline
            - Likes the agent but dislikes the company policies

            INSURANCE-SPECIFIC CAUTION:
            - "I'm glad the claim was denied" may be sarcastic - check context
            - "The premium is reasonable" from a price-sensitive persona may still indicate risk
            - Formal/legal language may indicate complaint even without explicit negative words
            """;
    }
}
