using SentimentAnalyzer.Agents.Configuration;
using SentimentAnalyzer.Domain.Enums;
using SentimentAnalyzer.Agents.Models;

namespace SentimentAnalyzer.Agents.Orchestration;

/// <summary>
/// Redacts PII from text before sending to external AI providers.
/// </summary>
public interface IPIIRedactor
{
    string Redact(string text);
}

/// <summary>
/// Interface for the multi-agent insurance analysis orchestrator.
/// </summary>
public interface IAnalysisOrchestrator
{
    /// <summary>
    /// Runs the full multi-agent analysis pipeline on the given customer interaction text.
    /// </summary>
    /// <param name="text">The customer interaction text to analyze.</param>
    /// <param name="interactionType">Type of interaction (email, call, chat, etc.).</param>
    /// <returns>Complete insurance analysis result from the agent pipeline.</returns>
    Task<AgentAnalysisResult> AnalyzeAsync(string text, InteractionType interactionType = InteractionType.General);

    /// <summary>
    /// Runs analysis using a specific orchestration profile, activating only the
    /// relevant subset of agents for the given task (e.g., ClaimsTriage, FraudScoring).
    /// </summary>
    /// <param name="text">The customer interaction text to analyze.</param>
    /// <param name="profile">The orchestration profile controlling agent selection and turn limits.</param>
    /// <param name="interactionType">Type of interaction (email, call, chat, etc.).</param>
    /// <returns>Complete insurance analysis result from the agent pipeline.</returns>
    Task<AgentAnalysisResult> AnalyzeAsync(string text, OrchestrationProfile profile, InteractionType interactionType = InteractionType.General);
}
