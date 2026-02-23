using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using SentimentAnalyzer.Agents.Definitions;

namespace SentimentAnalyzer.Agents.Orchestration;

/// <summary>
/// Custom agent selection strategy that follows the CTO's delegation pattern:
/// CTO -> BA -> Developer -> QA -> AI Expert -> UX Designer -> (Architect if present) -> CTO (final synthesis).
/// Dynamically adapts to the actual agents in the group chat.
/// </summary>
public class AgentSelectionStrategy : SelectionStrategy
{
    private static readonly string[] _fullSpeakingOrder =
    [
        AgentDefinitions.CTOAgentName,
        AgentDefinitions.BAAgentName,
        AgentDefinitions.DeveloperAgentName,
        AgentDefinitions.QAAgentName,
        AgentDefinitions.AIExpertAgentName,
        AgentDefinitions.UXDesignerAgentName,
        AgentDefinitions.ArchitectAgentName,
        AgentDefinitions.CTOAgentName  // Final synthesis
    ];

    private int _currentIndex = 0;
    private List<string>? _effectiveOrder;

    /// <inheritdoc />
    protected override Task<Agent> SelectAgentAsync(
        IReadOnlyList<Agent> agents,
        IReadOnlyList<ChatMessageContent> history,
        CancellationToken cancellationToken = default)
    {
        // Build the effective speaking order once, based on agents actually present
        _effectiveOrder ??= _fullSpeakingOrder
            .Where(name => agents.Any(a => a.Name == name))
            .ToList();

        if (_effectiveOrder.Count == 0)
        {
            return Task.FromResult(agents[0]);
        }

        var nextAgentName = _effectiveOrder[_currentIndex % _effectiveOrder.Count];
        _currentIndex++;

        var selectedAgent = agents.FirstOrDefault(a => a.Name == nextAgentName)
                            ?? agents[0];

        return Task.FromResult(selectedAgent);
    }
}
