using SentimentAnalyzer.Agents.Configuration;

namespace SentimentAnalyzer.Agents.Orchestration;

/// <summary>
/// Factory that creates the correct agent group configuration for a given orchestration profile.
/// Enables selective agent activation — only relevant agents are invoked per request type.
/// </summary>
public interface IOrchestrationProfileFactory
{
    /// <summary>
    /// Returns the agent names that should participate for the given profile.
    /// </summary>
    /// <param name="profile">The orchestration profile to use.</param>
    /// <returns>Ordered list of agent names for the profile.</returns>
    IReadOnlyList<string> GetAgentNamesForProfile(OrchestrationProfile profile);

    /// <summary>
    /// Returns the maximum conversation turns for the given profile.
    /// Fewer agents = fewer turns needed.
    /// </summary>
    /// <param name="profile">The orchestration profile.</param>
    /// <returns>Maximum number of agent turns before forced termination.</returns>
    int GetMaxTurnsForProfile(OrchestrationProfile profile);

    /// <summary>
    /// Returns the minimum turns before ANALYSIS_COMPLETE is accepted for the given profile.
    /// </summary>
    /// <param name="profile">The orchestration profile.</param>
    /// <returns>Minimum turns before early termination is allowed.</returns>
    int GetMinTurnsForProfile(OrchestrationProfile profile);
}
