using System.Text;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SentimentAnalyzer.Agents.Orchestration;
using SentimentAnalyzer.API.Models;

namespace SentimentAnalyzer.API.Services.Documents.RAGQuery;

/// <summary>
/// Synthesizes answers across multiple insurance documents.
/// Identifies conflicts between policy provisions, endorsements overriding base policies,
/// and gaps where no document addresses the question.
/// Only activated when citations span 2+ documents.
/// </summary>
public class CrossDocReasonerService : ICrossDocReasoner
{
    private readonly IResilientKernelProvider _kernelProvider;
    private readonly ILogger<CrossDocReasonerService> _logger;

    private const string SystemPrompt = """
        You are an insurance document cross-reference specialist.
        When a question requires information from multiple insurance documents,
        you synthesize a coherent answer that:

        1. Identifies which document(s) provide relevant information
        2. Highlights conflicts between documents (e.g., endorsement overrides base policy)
        3. Notes gaps where no document addresses part of the question
        4. Prioritizes more recent or more specific documents over general ones

        Insurance document hierarchy (most specific wins):
        - Endorsements override base policy provisions
        - Declarations page overrides printed policy for named items
        - State-specific addenda override general conditions

        Return JSON:
        {
          "synthesizedAnswer": "Comprehensive answer citing all relevant documents",
          "documentCount": 2,
          "conflicts": ["Document A says X but Document B says Y"],
          "wasSynthesized": true
        }

        Return ONLY the raw JSON object, no markdown fences.
        """;

    /// <summary>Initializes the cross-document reasoner with LLM kernel provider.</summary>
    public CrossDocReasonerService(
        IResilientKernelProvider kernelProvider,
        ILogger<CrossDocReasonerService> logger)
    {
        _kernelProvider = kernelProvider ?? throw new ArgumentNullException(nameof(kernelProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<CrossDocReasoningResult> SynthesizeAsync(
        string question, List<DocumentCitation> citations, List<string> sourceChunks,
        CancellationToken cancellationToken = default)
    {
        var uniqueDocIds = citations.Select(c => c.DocumentId).Distinct().ToList();

        // Single document — no cross-doc reasoning needed
        if (uniqueDocIds.Count <= 1)
        {
            return new CrossDocReasoningResult
            {
                DocumentCount = uniqueDocIds.Count,
                WasSynthesized = false
            };
        }

        try
        {
            var kernel = _kernelProvider.GetKernel();
            var chatService = kernel.GetRequiredService<IChatCompletionService>();

            var contextBuilder = new StringBuilder();
            for (var i = 0; i < citations.Count && i < sourceChunks.Count; i++)
            {
                var citation = citations[i];
                contextBuilder.AppendLine(
                    $"[{i + 1}] Document: \"{citation.FileName}\", Section: {citation.SectionName}");
                contextBuilder.AppendLine(sourceChunks[i]);
                contextBuilder.AppendLine();
            }

            var userPrompt = $"""
                QUESTION: {question}

                DOCUMENT EXCERPTS FROM {uniqueDocIds.Count} DOCUMENTS:
                {contextBuilder}
                """;

            var chatHistory = new ChatHistory(SystemPrompt);
            chatHistory.AddUserMessage(userPrompt);

            var response = await chatService.GetChatMessageContentAsync(
                chatHistory, cancellationToken: cancellationToken);

            var responseText = response.Content?.Trim() ?? "";
            if (responseText.StartsWith("```"))
            {
                responseText = responseText
                    .Replace("```json", "").Replace("```", "").Trim();
            }

            var result = JsonSerializer.Deserialize<CrossDocReasoningResult>(responseText,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result != null)
            {
                result.DocumentCount = uniqueDocIds.Count;
                result.WasSynthesized = true;

                _logger.LogInformation(
                    "Cross-doc reasoning: {DocCount} documents, {ConflictCount} conflicts found",
                    uniqueDocIds.Count, result.Conflicts.Count);

                return result;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cross-document reasoning failed, skipping synthesis");
        }

        return new CrossDocReasoningResult
        {
            DocumentCount = uniqueDocIds.Count,
            WasSynthesized = false
        };
    }
}
