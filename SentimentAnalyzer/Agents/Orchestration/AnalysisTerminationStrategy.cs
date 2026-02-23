using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using SentimentAnalyzer.Agents.Definitions;

namespace SentimentAnalyzer.Agents.Orchestration;

/// <summary>
/// Terminates the agent conversation when:
/// 1. The CTO agent outputs "ANALYSIS_COMPLETE", or
/// 2. The maximum number of turns is reached.
/// </summary>
public class AnalysisTerminationStrategy : TerminationStrategy
{
    private readonly int _maxTurns;
    private readonly int _minTurnsBeforeCompletion;

    public AnalysisTerminationStrategy(int maxTurns = 15, int minTurnsBeforeCompletion = 5)
    {
        _maxTurns = maxTurns;
        _minTurnsBeforeCompletion = minTurnsBeforeCompletion;
        MaximumIterations = maxTurns;
    }

    /// <inheritdoc />
    protected override Task<bool> ShouldAgentTerminateAsync(
        Agent agent,
        IReadOnlyList<ChatMessageContent> history,
        CancellationToken cancellationToken = default)
    {
        // Terminate if max turns reached
        if (history.Count >= _maxTurns)
        {
            return Task.FromResult(true);
        }

        // Only allow ANALYSIS_COMPLETE termination after minimum turns.
        // This prevents the CTO from short-circuiting the multi-agent pipeline
        // by producing the full JSON in turn 1 without delegating to BA/Dev/QA.
        if (agent.Name == AgentDefinitions.CTOAgentName &&
            history.Count >= _minTurnsBeforeCompletion)
        {
            var lastMessage = history.LastOrDefault();
            if (lastMessage?.Content?.Contains("ANALYSIS_COMPLETE") == true)
            {
                return Task.FromResult(true);
            }
        }

        return Task.FromResult(false);
    }
}
