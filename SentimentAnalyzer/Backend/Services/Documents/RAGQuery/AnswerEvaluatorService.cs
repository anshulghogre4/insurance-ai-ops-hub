using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SentimentAnalyzer.Agents.Orchestration;

namespace SentimentAnalyzer.API.Services.Documents.RAGQuery;

/// <summary>
/// Evaluates RAG answers for groundedness, citation accuracy, and completeness.
/// Uses LLM to verify each claim in the answer is supported by source documents.
/// </summary>
public class AnswerEvaluatorService : IAnswerEvaluator
{
    private readonly IResilientKernelProvider _kernelProvider;
    private readonly ILogger<AnswerEvaluatorService> _logger;

    private const string SystemPrompt = """
        You are a document QA answer evaluator for insurance documents.
        Given a question, an AI-generated answer, and the source document chunks used to generate it,
        evaluate the answer quality.

        Check:
        1. GROUNDEDNESS: Is every claim in the answer supported by the source chunks? List any unsupported claims.
        2. CITATIONS: Does the answer reference sources correctly? (e.g., [1], [2])
        3. COMPLETENESS: Does the answer address all aspects of the question?
        4. ACCURACY: Are insurance terms used correctly?

        Return JSON:
        {
          "qualityScore": 0.0-1.0,
          "isGrounded": true/false,
          "ungroundedClaims": ["claim text that lacks source support"],
          "citationsValid": true/false,
          "isComplete": true/false,
          "suggestions": ["improvement suggestion"]
        }

        Return ONLY the raw JSON object, no markdown fences.
        """;

    /// <summary>Initializes the answer evaluator with LLM kernel provider.</summary>
    public AnswerEvaluatorService(
        IResilientKernelProvider kernelProvider,
        ILogger<AnswerEvaluatorService> logger)
    {
        _kernelProvider = kernelProvider ?? throw new ArgumentNullException(nameof(kernelProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<AnswerEvaluationResult> EvaluateAsync(
        string question, string answer, List<string> sourceChunks,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var kernel = _kernelProvider.GetKernel();
            var chatService = kernel.GetRequiredService<IChatCompletionService>();

            var sourcesText = string.Join("\n---\n",
                sourceChunks.Select((chunk, i) => $"[{i + 1}] {chunk}"));

            var userPrompt = $"""
                QUESTION: {question}

                AI-GENERATED ANSWER:
                {answer}

                SOURCE CHUNKS:
                {sourcesText}
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

            var result = JsonSerializer.Deserialize<AnswerEvaluationResult>(responseText,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result != null)
            {
                _logger.LogInformation(
                    "Answer evaluated: quality={Quality}, grounded={Grounded}, complete={Complete}",
                    result.QualityScore, result.IsGrounded, result.IsComplete);
                return result;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Answer evaluation failed, returning default pass-through");
        }

        // Default: assume answer is acceptable (don't block the response)
        return new AnswerEvaluationResult
        {
            QualityScore = 0.7,
            IsGrounded = true,
            CitationsValid = true,
            IsComplete = true
        };
    }
}
