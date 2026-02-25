using SentimentAnalyzer.Agents.Configuration;
using SentimentAnalyzer.Agents.Definitions;

namespace SentimentAnalyzer.Agents.Orchestration;

/// <summary>
/// Maps orchestration profiles to agent subsets, enabling selective agent activation.
/// Reduces token usage by 50-60% for focused tasks like claims triage or fraud scoring.
/// </summary>
public class OrchestrationProfileFactory : IOrchestrationProfileFactory
{
    /// <inheritdoc />
    public IReadOnlyList<string> GetAgentNamesForProfile(OrchestrationProfile profile)
    {
        return profile switch
        {
            OrchestrationProfile.SentimentAnalysis =>
            [
                AgentDefinitions.CTOAgentName,
                AgentDefinitions.BAAgentName,
                AgentDefinitions.DeveloperAgentName,
                AgentDefinitions.QAAgentName,
                AgentDefinitions.AIExpertAgentName,
                AgentDefinitions.UXDesignerAgentName,
                AgentDefinitions.ArchitectAgentName
            ],
            OrchestrationProfile.ClaimsTriage =>
            [
                AgentDefinitions.ClaimsTriageAgentName,
                AgentDefinitions.FraudDetectionAgentName,
                AgentDefinitions.BAAgentName,
                AgentDefinitions.QAAgentName
            ],
            OrchestrationProfile.FraudScoring =>
            [
                AgentDefinitions.FraudDetectionAgentName,
                AgentDefinitions.ClaimsTriageAgentName,
                AgentDefinitions.BAAgentName,
                AgentDefinitions.QAAgentName
            ],
            OrchestrationProfile.DocumentQuery =>
            [
                AgentDefinitions.BAAgentName,
                AgentDefinitions.QAAgentName,
                AgentDefinitions.DeveloperAgentName
            ],
            OrchestrationProfile.CustomerExperience =>
            [
                AgentDefinitions.CustomerExperienceAgentName,
                AgentDefinitions.BAAgentName,
                AgentDefinitions.DeveloperAgentName,
                AgentDefinitions.UXDesignerAgentName,
                AgentDefinitions.QAAgentName
            ],
            _ =>
            [
                AgentDefinitions.CTOAgentName,
                AgentDefinitions.BAAgentName,
                AgentDefinitions.QAAgentName
            ]
        };
    }

    /// <inheritdoc />
    public int GetMaxTurnsForProfile(OrchestrationProfile profile)
    {
        return profile switch
        {
            OrchestrationProfile.SentimentAnalysis => 15,
            OrchestrationProfile.ClaimsTriage => 8,
            OrchestrationProfile.FraudScoring => 8,
            OrchestrationProfile.DocumentQuery => 6,
            OrchestrationProfile.CustomerExperience => 8,
            _ => 10
        };
    }

    /// <inheritdoc />
    public int GetMinTurnsForProfile(OrchestrationProfile profile)
    {
        return profile switch
        {
            OrchestrationProfile.SentimentAnalysis => 5,
            OrchestrationProfile.ClaimsTriage => 3,
            OrchestrationProfile.FraudScoring => 3,
            OrchestrationProfile.DocumentQuery => 2,
            OrchestrationProfile.CustomerExperience => 3,
            _ => 3
        };
    }
}
