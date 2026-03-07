namespace SentimentAnalyzer.API.Services.Documents.RAGQuery;

/// <summary>
/// Rewrites vague or ambiguous user questions into more precise search queries
/// optimized for hybrid retrieval (BM25 + vector).
/// </summary>
public interface IQueryReformulator
{
    /// <summary>
    /// Reformulates a user question into improved search queries.
    /// Returns the original query plus reformulated variants for broader retrieval.
    /// </summary>
    Task<QueryReformulationResult> ReformulateAsync(
        string originalQuestion,
        CancellationToken cancellationToken = default);
}

/// <summary>Result of query reformulation with original and expanded queries.</summary>
public class QueryReformulationResult
{
    /// <summary>Original user question (preserved for fallback).</summary>
    public string OriginalQuery { get; set; } = string.Empty;

    /// <summary>Reformulated queries optimized for retrieval.</summary>
    public List<string> ReformulatedQueries { get; set; } = [];

    /// <summary>Whether reformulation was applied (false = passthrough).</summary>
    public bool WasReformulated { get; set; }
}
