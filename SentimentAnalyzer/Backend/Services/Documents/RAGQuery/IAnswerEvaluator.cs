namespace SentimentAnalyzer.API.Services.Documents.RAGQuery;

/// <summary>
/// Evaluates RAG-generated answers for citation quality, groundedness, and confidence.
/// Ensures answers are backed by source documents and not hallucinated.
/// </summary>
public interface IAnswerEvaluator
{
    /// <summary>
    /// Evaluates a generated answer against its source citations.
    /// Returns quality metrics and flags potential hallucinations.
    /// </summary>
    Task<AnswerEvaluationResult> EvaluateAsync(
        string question,
        string answer,
        List<string> sourceChunks,
        CancellationToken cancellationToken = default);
}

/// <summary>Result of answer quality evaluation.</summary>
public class AnswerEvaluationResult
{
    /// <summary>Overall quality score (0.0-1.0).</summary>
    public double QualityScore { get; set; }

    /// <summary>Whether the answer is grounded in the source documents.</summary>
    public bool IsGrounded { get; set; } = true;

    /// <summary>Specific claims in the answer that lack source support.</summary>
    public List<string> UngroundedClaims { get; set; } = [];

    /// <summary>Whether citations are correctly referenced.</summary>
    public bool CitationsValid { get; set; } = true;

    /// <summary>Whether the answer fully addresses the question.</summary>
    public bool IsComplete { get; set; } = true;

    /// <summary>Suggestions for improving the answer.</summary>
    public List<string> Suggestions { get; set; } = [];
}
