namespace SentimentAnalyzer.Agents.Configuration;

/// <summary>
/// Defines which agents participate in a given analysis type.
/// Each profile activates a focused subset of agents for efficiency (50-60% fewer tokens).
/// </summary>
public enum OrchestrationProfile
{
    /// <summary>Existing: CTO, BA, Developer, QA, AIExpert, UXDesigner, Architect (7 agents, 15 max turns).</summary>
    SentimentAnalysis,

    /// <summary>Claims triage: ClaimsTriage, FraudDetection, BA, QA (4 agents, 8 max turns).</summary>
    ClaimsTriage,

    /// <summary>Fraud scoring: FraudDetection, ClaimsTriage, BA, QA (4 agents, 8 max turns).</summary>
    FraudScoring,

    /// <summary>Document RAG query: Document, BA, QA (3 agents, 6 max turns). Future use.</summary>
    DocumentQuery,

    /// <summary>Customer experience: CX, BA, Sentiment, QA. Phase 2 — post-MVP.</summary>
    CustomerExperience
}
