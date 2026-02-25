namespace SentimentAnalyzer.API.Services.Embeddings;

/// <summary>
/// Generates vector embeddings from text for RAG document indexing and similarity search.
/// Insurance use case: embedding insurance policy documents, claim descriptions, and
/// regulatory text for semantic retrieval in the Document Intelligence pipeline.
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Generates a single embedding vector for the given text.
    /// Text MUST be PII-redacted before calling this method for external providers.
    /// </summary>
    /// <param name="text">The PII-redacted text to embed.</param>
    /// <param name="inputType">
    /// Hint for the embedding model: "query" for search queries, "document" for indexed content,
    /// or null for general-purpose embedding.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The embedding result containing the float vector.</returns>
    Task<EmbeddingResult> GenerateEmbeddingAsync(
        string text,
        string? inputType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates embedding vectors for multiple texts in a single batch request.
    /// More efficient than calling GenerateEmbeddingAsync in a loop.
    /// Text MUST be PII-redacted before calling this method for external providers.
    /// </summary>
    /// <param name="texts">The PII-redacted texts to embed (max 128 per batch).</param>
    /// <param name="inputType">
    /// Hint for the embedding model: "query" for search queries, "document" for indexed content,
    /// or null for general-purpose embedding.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The batch embedding result containing float vectors in input order.</returns>
    Task<BatchEmbeddingResult> GenerateBatchEmbeddingsAsync(
        string[] texts,
        string? inputType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The dimensionality of embedding vectors produced by this provider.
    /// Voyage AI voyage-finance-2: 1024, Ollama mxbai-embed-large: 1024.
    /// </summary>
    int EmbeddingDimension { get; }

    /// <summary>
    /// Human-readable name of the embedding provider (e.g., "VoyageAI", "Ollama").
    /// </summary>
    string ProviderName { get; }
}

/// <summary>
/// Result of a single text embedding operation.
/// </summary>
public class EmbeddingResult
{
    /// <summary>Whether the embedding generation succeeded.</summary>
    public bool IsSuccess { get; set; }

    /// <summary>The embedding vector. Empty array on failure.</summary>
    public float[] Embedding { get; set; } = [];

    /// <summary>Dimensionality of the returned embedding vector.</summary>
    public int Dimension => Embedding.Length;

    /// <summary>Provider that generated the embedding.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Number of tokens consumed by this embedding request.</summary>
    public int TokensUsed { get; set; }

    /// <summary>Error message if embedding generation failed.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Processing time in milliseconds.</summary>
    public long ElapsedMilliseconds { get; set; }
}

/// <summary>
/// Result of a batch text embedding operation.
/// </summary>
public class BatchEmbeddingResult
{
    /// <summary>Whether the batch embedding generation succeeded.</summary>
    public bool IsSuccess { get; set; }

    /// <summary>The embedding vectors in the same order as the input texts. Empty on failure.</summary>
    public float[][] Embeddings { get; set; } = [];

    /// <summary>Dimensionality of the returned embedding vectors.</summary>
    public int Dimension => Embeddings.Length > 0 ? Embeddings[0].Length : 0;

    /// <summary>Number of texts successfully embedded.</summary>
    public int Count => Embeddings.Length;

    /// <summary>Provider that generated the embeddings.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Total number of tokens consumed by this batch request.</summary>
    public int TotalTokensUsed { get; set; }

    /// <summary>Error message if batch embedding generation failed.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Processing time in milliseconds.</summary>
    public long ElapsedMilliseconds { get; set; }
}
