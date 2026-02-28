using SentimentAnalyzer.API.Data.Entities;

namespace SentimentAnalyzer.API.Services.Documents;

/// <summary>
/// Interface for hybrid retrieval that fuses dense (vector) and sparse (BM25) search results
/// using Reciprocal Rank Fusion (RRF).
/// </summary>
public interface IHybridRetrievalService
{
    /// <summary>
    /// Fuses vector similarity and BM25 ranked results using Reciprocal Rank Fusion.
    /// Produces a single ranked list that benefits from both semantic and keyword matching.
    /// </summary>
    /// <param name="vectorResults">Dense retrieval results from embedding cosine similarity search.</param>
    /// <param name="bm25Results">Sparse retrieval results from BM25 keyword scoring.</param>
    /// <param name="topK">Maximum number of results to return after fusion.</param>
    /// <returns>Fused and re-ranked list of chunks with combined RRF scores.</returns>
    List<(DocumentChunkRecord Chunk, double Score)> FuseResults(
        List<(DocumentChunkRecord Chunk, double Score)> vectorResults,
        List<(DocumentChunkRecord Chunk, double Score)> bm25Results,
        int topK = 5);
}

/// <summary>
/// Implements Reciprocal Rank Fusion (RRF) to merge dense vector search and sparse BM25 results.
/// RRF formula: score = sum(1 / (k + rank_i)) where k = 60 (standard constant from Cormack et al., 2009).
/// This approach is robust to score scale differences between retrieval methods.
/// </summary>
public class HybridRetrievalService : IHybridRetrievalService
{
    /// <summary>
    /// RRF constant k. Standard value of 60 balances the contribution of high-ranked
    /// and lower-ranked documents across retrieval methods.
    /// </summary>
    private const int RrfK = 60;

    private readonly ILogger<HybridRetrievalService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="HybridRetrievalService"/> class.
    /// </summary>
    /// <param name="logger">Logger for hybrid retrieval diagnostics.</param>
    public HybridRetrievalService(ILogger<HybridRetrievalService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public List<(DocumentChunkRecord Chunk, double Score)> FuseResults(
        List<(DocumentChunkRecord Chunk, double Score)> vectorResults,
        List<(DocumentChunkRecord Chunk, double Score)> bm25Results,
        int topK = 5)
    {
        // Handle edge cases: if one list is empty, return the other (up to topK)
        if (vectorResults.Count == 0 && bm25Results.Count == 0)
        {
            return [];
        }

        if (vectorResults.Count == 0)
        {
            return bm25Results.Take(topK).ToList();
        }

        if (bm25Results.Count == 0)
        {
            return vectorResults.Take(topK).ToList();
        }

        // RRF: assign rank-based scores to each chunk from each result list
        // Both lists should already be sorted by their respective scores (descending)
        var rrfScores = new Dictionary<int, (DocumentChunkRecord Chunk, double Score)>();

        // Process vector results (rank starts at 1)
        for (var rank = 0; rank < vectorResults.Count; rank++)
        {
            var (chunk, _) = vectorResults[rank];
            var rrfScore = 1.0 / (RrfK + rank + 1); // rank + 1 because ranks are 1-based

            if (rrfScores.TryGetValue(chunk.Id, out var existing))
            {
                rrfScores[chunk.Id] = (chunk, existing.Score + rrfScore);
            }
            else
            {
                rrfScores[chunk.Id] = (chunk, rrfScore);
            }
        }

        // Process BM25 results (rank starts at 1)
        for (var rank = 0; rank < bm25Results.Count; rank++)
        {
            var (chunk, _) = bm25Results[rank];
            var rrfScore = 1.0 / (RrfK + rank + 1);

            if (rrfScores.TryGetValue(chunk.Id, out var existing))
            {
                rrfScores[chunk.Id] = (chunk, existing.Score + rrfScore);
            }
            else
            {
                rrfScores[chunk.Id] = (chunk, rrfScore);
            }
        }

        var fusedResults = rrfScores.Values
            .OrderByDescending(r => r.Score)
            .Take(topK)
            .ToList();

        _logger.LogInformation(
            "Hybrid retrieval: {VectorCount} vector + {BM25Count} BM25 candidates → {FusedCount} fused results (top-{TopK})",
            vectorResults.Count, bm25Results.Count, fusedResults.Count, topK);

        return fusedResults;
    }
}
