using System.Numerics;
using System.Runtime.InteropServices;

namespace SentimentAnalyzer.API.Services.Embeddings;

/// <summary>
/// SIMD-accelerated cosine similarity computation using System.Numerics.Vector&lt;float&gt;.
/// Zero NuGet dependencies. Efficient for comparing embeddings in SQLite-backed RAG
/// with up to ~10K document chunks.
///
/// Cosine similarity = (A . B) / (||A|| * ||B||)
/// Range: -1.0 (opposite) to 1.0 (identical). For normalized embeddings (unit-length),
/// this simplifies to just the dot product.
/// </summary>
public static class CosineSimilarity
{
    /// <summary>
    /// Computes cosine similarity between two embedding vectors using SIMD acceleration.
    /// Handles vectors of any dimension (1024, 768, etc.) including mismatched sizes
    /// by zero-padding the shorter vector.
    /// </summary>
    /// <param name="a">First embedding vector.</param>
    /// <param name="b">Second embedding vector.</param>
    /// <returns>
    /// Cosine similarity in range [-1.0, 1.0].
    /// Returns 0.0 if either vector is zero-length or all-zeros.
    /// </returns>
    /// <exception cref="ArgumentNullException">If either vector is null.</exception>
    public static float Compute(float[] a, float[] b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        if (a.Length == 0 || b.Length == 0)
            return 0f;

        // Handle dimension mismatch by using the shorter length.
        // This is a design decision: when Voyage AI (1024-dim) embeddings are compared
        // with Ollama (768-dim) embeddings, we truncate to the shorter dimension.
        // In practice, all embeddings in a single index should have the same dimension.
        var length = Math.Min(a.Length, b.Length);

        return ComputeSimd(a.AsSpan(0, length), b.AsSpan(0, length));
    }

    /// <summary>
    /// Computes cosine similarity using SIMD-accelerated dot product and magnitude.
    /// Processes Vector&lt;float&gt;.Count elements per iteration (typically 4-8 on x86/x64 with SSE/AVX).
    /// </summary>
    private static float ComputeSimd(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        var dotProduct = 0f;
        var magnitudeA = 0f;
        var magnitudeB = 0f;

        var simdLength = Vector<float>.Count;
        var i = 0;

        // SIMD path: process simdLength floats at a time
        if (Vector.IsHardwareAccelerated && a.Length >= simdLength)
        {
            var vDot = Vector<float>.Zero;
            var vMagA = Vector<float>.Zero;
            var vMagB = Vector<float>.Zero;

            var aVectors = MemoryMarshal.Cast<float, Vector<float>>(a[..(a.Length - a.Length % simdLength)]);
            var bVectors = MemoryMarshal.Cast<float, Vector<float>>(b[..(b.Length - b.Length % simdLength)]);

            for (var vi = 0; vi < aVectors.Length; vi++)
            {
                vDot += aVectors[vi] * bVectors[vi];
                vMagA += aVectors[vi] * aVectors[vi];
                vMagB += bVectors[vi] * bVectors[vi];
            }

            // Horizontal sum of SIMD vectors
            dotProduct = Vector.Sum(vDot);
            magnitudeA = Vector.Sum(vMagA);
            magnitudeB = Vector.Sum(vMagB);

            i = a.Length - a.Length % simdLength;
        }

        // Scalar tail: process remaining elements that don't fill a full SIMD vector
        for (; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            magnitudeA += a[i] * a[i];
            magnitudeB += b[i] * b[i];
        }

        // Guard against zero-magnitude vectors (would cause division by zero)
        if (magnitudeA == 0f || magnitudeB == 0f)
            return 0f;

        return dotProduct / (MathF.Sqrt(magnitudeA) * MathF.Sqrt(magnitudeB));
    }

    /// <summary>
    /// Finds the top-K most similar embeddings to the query embedding from a collection.
    /// Used for RAG retrieval: given a query embedding, find the K most relevant document chunks.
    /// </summary>
    /// <param name="queryEmbedding">The query embedding vector.</param>
    /// <param name="candidateEmbeddings">Collection of (id, embedding) pairs to search.</param>
    /// <param name="topK">Number of top results to return (default: 5).</param>
    /// <returns>
    /// Top-K results ordered by descending similarity, each containing the candidate ID
    /// and its cosine similarity score.
    /// </returns>
    public static List<SimilarityResult> FindTopK(
        float[] queryEmbedding,
        IEnumerable<(string Id, float[] Embedding)> candidateEmbeddings,
        int topK = 5)
    {
        ArgumentNullException.ThrowIfNull(queryEmbedding);
        ArgumentNullException.ThrowIfNull(candidateEmbeddings);

        if (topK <= 0)
            throw new ArgumentOutOfRangeException(nameof(topK), "topK must be positive.");

        // For <10K chunks, a simple sorted list is efficient enough.
        // No need for a min-heap or approximate nearest neighbor index.
        var results = new List<SimilarityResult>();

        foreach (var (id, embedding) in candidateEmbeddings)
        {
            var similarity = Compute(queryEmbedding, embedding);
            results.Add(new SimilarityResult { Id = id, Score = similarity });
        }

        results.Sort((x, y) => y.Score.CompareTo(x.Score));

        return results.Count <= topK ? results : results.GetRange(0, topK);
    }
}

/// <summary>
/// A single similarity search result pairing a document chunk ID with its cosine similarity score.
/// </summary>
public class SimilarityResult
{
    /// <summary>Identifier of the candidate document chunk.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Cosine similarity score in range [-1.0, 1.0]. Higher = more similar.</summary>
    public float Score { get; set; }
}
