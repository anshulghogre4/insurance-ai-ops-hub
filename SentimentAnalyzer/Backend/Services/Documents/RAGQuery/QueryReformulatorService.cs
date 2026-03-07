using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SentimentAnalyzer.Agents.Orchestration;

namespace SentimentAnalyzer.API.Services.Documents.RAGQuery;

/// <summary>
/// Rewrites user questions into multiple search-optimized queries using LLM.
/// Handles vague questions ("what's covered?") by expanding them with insurance domain context.
/// Short, clear questions pass through unchanged for efficiency.
/// </summary>
public class QueryReformulatorService : IQueryReformulator
{
    private readonly IResilientKernelProvider _kernelProvider;
    private readonly ILogger<QueryReformulatorService> _logger;

    private const int MinQueryLengthForReformulation = 15;

    private const string SystemPrompt = """
        You are an insurance document search query optimizer.
        Given a user's question about insurance documents, generate 2-3 alternative search queries
        that would help find relevant passages in insurance policy documents.

        Focus on:
        - Expanding abbreviations (e.g., "BI" -> "bodily injury")
        - Adding insurance domain synonyms (e.g., "deductible" -> "out-of-pocket", "self-insured retention")
        - Rephrasing for keyword matching (e.g., "am I covered for floods?" -> "flood coverage exclusions policy terms")

        Return a JSON array of strings. Example:
        ["flood coverage exclusions policy terms", "water damage coverage limits homeowner", "natural disaster policy provisions"]

        Return ONLY the raw JSON array, no markdown fences.
        """;

    /// <summary>Initializes the query reformulator with LLM kernel provider.</summary>
    public QueryReformulatorService(
        IResilientKernelProvider kernelProvider,
        ILogger<QueryReformulatorService> logger)
    {
        _kernelProvider = kernelProvider ?? throw new ArgumentNullException(nameof(kernelProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<QueryReformulationResult> ReformulateAsync(
        string originalQuestion, CancellationToken cancellationToken = default)
    {
        // Short, specific queries don't need reformulation
        if (originalQuestion.Length < MinQueryLengthForReformulation)
        {
            return new QueryReformulationResult
            {
                OriginalQuery = originalQuestion,
                ReformulatedQueries = [originalQuestion],
                WasReformulated = false
            };
        }

        try
        {
            var kernel = _kernelProvider.GetKernel();
            var chatService = kernel.GetRequiredService<IChatCompletionService>();

            var chatHistory = new ChatHistory(SystemPrompt);
            chatHistory.AddUserMessage(originalQuestion);

            var response = await chatService.GetChatMessageContentAsync(
                chatHistory, cancellationToken: cancellationToken);

            var responseText = response.Content?.Trim() ?? "";

            // Strip markdown fences if present
            if (responseText.StartsWith("```"))
            {
                responseText = responseText
                    .Replace("```json", "").Replace("```", "").Trim();
            }

            var queries = JsonSerializer.Deserialize<List<string>>(responseText);
            if (queries is { Count: > 0 })
            {
                _logger.LogInformation(
                    "Query reformulated: original='{Original}' -> {Count} variants",
                    originalQuestion[..Math.Min(50, originalQuestion.Length)], queries.Count);

                return new QueryReformulationResult
                {
                    OriginalQuery = originalQuestion,
                    ReformulatedQueries = queries,
                    WasReformulated = true
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Query reformulation failed, using original query");
        }

        // Fallback: return original query unchanged
        return new QueryReformulationResult
        {
            OriginalQuery = originalQuestion,
            ReformulatedQueries = [originalQuestion],
            WasReformulated = false
        };
    }
}
