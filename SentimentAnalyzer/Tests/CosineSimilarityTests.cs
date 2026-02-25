using SentimentAnalyzer.API.Services.Embeddings;
using Xunit;

namespace SentimentAnalyzer.Tests;

/// <summary>
/// Tests for SIMD-accelerated CosineSimilarity computation.
/// Validates correctness for insurance RAG embedding similarity search.
/// </summary>
public class CosineSimilarityTests
{
    [Fact]
    public void Compute_IdenticalVectors_Returns1()
    {
        float[] a = [1.0f, 2.0f, 3.0f, 4.0f];
        float[] b = [1.0f, 2.0f, 3.0f, 4.0f];

        var similarity = CosineSimilarity.Compute(a, b);

        Assert.Equal(1.0f, similarity, precision: 5);
    }

    [Fact]
    public void Compute_OppositeVectors_ReturnsNegative1()
    {
        float[] a = [1.0f, 2.0f, 3.0f];
        float[] b = [-1.0f, -2.0f, -3.0f];

        var similarity = CosineSimilarity.Compute(a, b);

        Assert.Equal(-1.0f, similarity, precision: 5);
    }

    [Fact]
    public void Compute_OrthogonalVectors_Returns0()
    {
        float[] a = [1.0f, 0.0f, 0.0f];
        float[] b = [0.0f, 1.0f, 0.0f];

        var similarity = CosineSimilarity.Compute(a, b);

        Assert.Equal(0.0f, similarity, precision: 5);
    }

    [Fact]
    public void Compute_SimilarVectors_ReturnsHighSimilarity()
    {
        // Simulating two insurance claim embeddings that are semantically similar
        float[] claimDenial1 = [0.8f, 0.6f, -0.3f, 0.1f, 0.5f];
        float[] claimDenial2 = [0.75f, 0.65f, -0.25f, 0.15f, 0.45f];

        var similarity = CosineSimilarity.Compute(claimDenial1, claimDenial2);

        Assert.True(similarity > 0.99f, $"Expected high similarity but got {similarity}");
    }

    [Fact]
    public void Compute_DissimilarVectors_ReturnsLowSimilarity()
    {
        // Simulating two very different insurance document embeddings
        float[] claimText = [0.9f, 0.8f, 0.7f, -0.1f];
        float[] policyExclusion = [-0.3f, -0.5f, 0.1f, 0.9f];

        var similarity = CosineSimilarity.Compute(claimText, policyExclusion);

        Assert.True(similarity < 0.0f, $"Expected low/negative similarity but got {similarity}");
    }

    [Fact]
    public void Compute_EmptyVectors_Returns0()
    {
        float[] a = [];
        float[] b = [];

        var similarity = CosineSimilarity.Compute(a, b);

        Assert.Equal(0f, similarity);
    }

    [Fact]
    public void Compute_ZeroVector_Returns0()
    {
        float[] a = [0.0f, 0.0f, 0.0f];
        float[] b = [1.0f, 2.0f, 3.0f];

        var similarity = CosineSimilarity.Compute(a, b);

        Assert.Equal(0f, similarity);
    }

    [Fact]
    public void Compute_DifferentDimensions_TruncatesToShorter()
    {
        // Simulates Voyage AI (1024-dim) vs Ollama (768-dim) mismatch
        // For testing, use small vectors: 5-dim vs 3-dim
        float[] longer = [1.0f, 2.0f, 3.0f, 4.0f, 5.0f];
        float[] shorter = [1.0f, 2.0f, 3.0f];

        // Should compute similarity using first 3 dimensions only
        var similarity = CosineSimilarity.Compute(longer, shorter);

        // Compare with manually computing cos(a[0:3], b[0:3])
        float[] longerTruncated = [1.0f, 2.0f, 3.0f];
        var expected = CosineSimilarity.Compute(longerTruncated, shorter);

        Assert.Equal(expected, similarity, precision: 5);
    }

    [Fact]
    public void Compute_NullFirstVector_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => CosineSimilarity.Compute(null!, [1f, 2f]));
    }

    [Fact]
    public void Compute_NullSecondVector_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => CosineSimilarity.Compute([1f, 2f], null!));
    }

    [Fact]
    public void Compute_LargeVectors_ProducesCorrectResult()
    {
        // Test with 1024-dimension vectors (matching Voyage AI / mxbai-embed-large)
        var random = new Random(42); // Deterministic seed
        var a = new float[1024];
        var b = new float[1024];
        for (var i = 0; i < 1024; i++)
        {
            a[i] = (float)(random.NextDouble() * 2 - 1);
            b[i] = (float)(random.NextDouble() * 2 - 1);
        }

        var similarity = CosineSimilarity.Compute(a, b);

        // Random 1024-dim vectors should have similarity near 0 (orthogonal in high dimensions)
        Assert.True(similarity >= -1.0f && similarity <= 1.0f,
            $"Similarity {similarity} out of valid range [-1, 1]");
        Assert.True(Math.Abs(similarity) < 0.2f,
            $"Random 1024-dim vectors should be nearly orthogonal, but got {similarity}");
    }

    [Fact]
    public void Compute_UnitVectors_EquivalentToDotProduct()
    {
        // For unit-length (L2-normalized) vectors, cosine similarity = dot product
        // Ollama /api/embed returns L2-normalized vectors
        float[] a = [0.6f, 0.8f]; // ||a|| = 1.0
        float[] b = [0.8f, 0.6f]; // ||b|| = 1.0

        var similarity = CosineSimilarity.Compute(a, b);
        var dotProduct = 0.6f * 0.8f + 0.8f * 0.6f; // 0.96

        Assert.Equal(dotProduct, similarity, precision: 5);
    }

    // --- FindTopK Tests ---

    [Fact]
    public void FindTopK_ReturnsTopKMostSimilar()
    {
        float[] query = [1.0f, 0.0f, 0.0f];

        var candidates = new List<(string Id, float[] Embedding)>
        {
            ("claim-001", new float[] { 0.9f, 0.1f, 0.0f }),     // Very similar
            ("claim-002", new float[] { 0.0f, 1.0f, 0.0f }),     // Orthogonal
            ("claim-003", new float[] { 0.95f, 0.05f, 0.0f }),   // Most similar
            ("policy-001", new float[] { -0.8f, -0.2f, 0.0f }),  // Opposite
            ("exclusion-001", new float[] { 0.5f, 0.5f, 0.5f }), // Moderate
        };

        var results = CosineSimilarity.FindTopK(query, candidates, topK: 3);

        Assert.Equal(3, results.Count);
        Assert.Equal("claim-003", results[0].Id); // Most similar first
        Assert.Equal("claim-001", results[1].Id);
        Assert.True(results[0].Score > results[1].Score);
        Assert.True(results[1].Score > results[2].Score);
    }

    [Fact]
    public void FindTopK_WithFewerCandidatesThanK_ReturnsAll()
    {
        float[] query = [1.0f, 0.0f];

        var candidates = new List<(string Id, float[] Embedding)>
        {
            ("doc-1", new float[] { 0.8f, 0.2f }),
            ("doc-2", new float[] { 0.5f, 0.5f }),
        };

        var results = CosineSimilarity.FindTopK(query, candidates, topK: 5);

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void FindTopK_WithZeroK_ThrowsArgumentOutOfRange()
    {
        float[] query = [1.0f];
        var candidates = new List<(string Id, float[] Embedding)>();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => CosineSimilarity.FindTopK(query, candidates, topK: 0));
    }

    [Fact]
    public void FindTopK_WithNullQuery_ThrowsArgumentNull()
    {
        var candidates = new List<(string Id, float[] Embedding)>();

        Assert.Throws<ArgumentNullException>(
            () => CosineSimilarity.FindTopK(null!, candidates));
    }

    [Fact]
    public void FindTopK_ResultsOrderedByDescendingSimilarity()
    {
        float[] query = [1.0f, 1.0f, 1.0f];

        var candidates = new List<(string Id, float[] Embedding)>
        {
            ("low", new float[] { -0.5f, 0.0f, 0.0f }),
            ("high", new float[] { 0.9f, 0.9f, 0.9f }),
            ("medium", new float[] { 0.5f, 0.5f, 0.0f }),
        };

        var results = CosineSimilarity.FindTopK(query, candidates, topK: 3);

        Assert.Equal("high", results[0].Id);
        for (var i = 1; i < results.Count; i++)
        {
            Assert.True(results[i - 1].Score >= results[i].Score,
                $"Results not in descending order at index {i}");
        }
    }

    [Fact]
    public void FindTopK_DefaultTopKIs5()
    {
        float[] query = [1.0f];
        var candidates = Enumerable.Range(1, 10)
            .Select(i => ($"doc-{i}", new float[] { i * 0.1f }))
            .ToList();

        var results = CosineSimilarity.FindTopK(query, candidates);

        Assert.Equal(5, results.Count);
    }
}
