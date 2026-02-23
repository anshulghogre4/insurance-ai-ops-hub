namespace SentimentAnalyzer.Agents.Configuration;

/// <summary>
/// Configuration options for the agent orchestration system.
/// </summary>
public class AgentConfiguration
{
    /// <summary>Maximum number of agent conversation turns before forcing completion.</summary>
    public int MaxAgentTurns { get; set; } = 15;

    /// <summary>Timeout in seconds for the complete multi-agent analysis.</summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>Whether to include the Solution Architect agent in the analysis pipeline.</summary>
    public bool IncludeArchitectAgent { get; set; } = true;

    /// <summary>Whether to fall back to simple single-agent analysis on orchestration failure.</summary>
    public bool FallbackToSimpleAnalysis { get; set; } = true;
}
