namespace SentimentAnalyzer.Agents.Definitions;

/// <summary>
/// Defines the roles in the multi-agent insurance analysis system.
/// </summary>
public enum AgentRole
{
    /// <summary>Chief Technology Officer - orchestrates all other agents.</summary>
    CTO,

    /// <summary>Business Analyst - insurance domain expert.</summary>
    BusinessAnalyst,

    /// <summary>Full Stack Developer - formats and validates output.</summary>
    Developer,

    /// <summary>QA/Tester - validates consistency and quality.</summary>
    QATester,

    /// <summary>Solution Architect - advises on storage and performance.</summary>
    SolutionArchitect,

    /// <summary>UX/UI Designer - defines screen layouts, interaction patterns, and design system governance.</summary>
    UXDesigner,

    /// <summary>AI/ML Expert - advises on model selection, cloud adoption, training strategies, and responsible AI governance.</summary>
    AIExpert,

    /// <summary>Claims Triage Specialist - analyzes claims for severity, urgency, and recommended actions.</summary>
    ClaimsTriage,

    /// <summary>Fraud Detection Specialist - scores claims for fraud probability and flags suspicious patterns.</summary>
    FraudDetection
}
