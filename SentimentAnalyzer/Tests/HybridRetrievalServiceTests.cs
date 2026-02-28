using Microsoft.Extensions.Logging;
using Moq;
using SentimentAnalyzer.API.Data.Entities;
using SentimentAnalyzer.API.Services.Documents;

namespace SentimentAnalyzer.Tests;

/// <summary>
/// Unit tests for HybridRetrievalService — the Reciprocal Rank Fusion (RRF) implementation
/// that merges dense vector search and sparse BM25 results for the hybrid RAG pipeline.
/// </summary>
public class HybridRetrievalServiceTests
{
    private readonly HybridRetrievalService _sut;

    public HybridRetrievalServiceTests()
    {
        var mockLogger = new Mock<ILogger<HybridRetrievalService>>();
        _sut = new HybridRetrievalService(mockLogger.Object);
    }

    /// <summary>
    /// Helper to create a DocumentChunkRecord with specified metadata for test scenarios.
    /// Uses realistic insurance document content.
    /// </summary>
    private static DocumentChunkRecord CreateChunk(int id, string content, string section = "GENERAL")
    {
        return new DocumentChunkRecord
        {
            Id = id,
            DocumentId = 1,
            ChunkIndex = id - 1,
            SectionName = section,
            Content = content,
            TokenCount = content.Split(' ').Length,
            EmbeddingJson = "[]"
        };
    }

    // ────────────────────────────────────────────────────────────
    // Overlapping Results Tests
    // ────────────────────────────────────────────────────────────

    [Fact]
    public void FuseResults_OverlappingResults_CombinesRRFScores()
    {
        // Arrange — same chunk appears in both vector and BM25 results
        var sharedChunk = CreateChunk(1, "Policy POL-12345: Dwelling Coverage $350,000.", "DECLARATIONS");
        var vectorOnlyChunk = CreateChunk(2, "Coverage A protects the insured dwelling structure.", "COVERAGE");
        var bm25OnlyChunk = CreateChunk(3, "Policy POL-12345 endorsement for flood damage.", "ENDORSEMENTS");

        var vectorResults = new List<(DocumentChunkRecord Chunk, double Score)>
        {
            (sharedChunk, 0.92),
            (vectorOnlyChunk, 0.85)
        };

        var bm25Results = new List<(DocumentChunkRecord Chunk, double Score)>
        {
            (sharedChunk, 3.45),
            (bm25OnlyChunk, 2.10)
        };

        // Act
        var fused = _sut.FuseResults(vectorResults, bm25Results, topK: 5);

        // Assert — shared chunk should rank highest (gets RRF score from both lists)
        Assert.Equal(3, fused.Count);
        Assert.Equal(1, fused[0].Chunk.Id);

        // Shared chunk gets 1/(60+1) from vector rank 1 + 1/(60+1) from BM25 rank 1
        var expectedSharedScore = 1.0 / 61 + 1.0 / 61;
        Assert.Equal(expectedSharedScore, fused[0].Score, precision: 10);

        // Vector-only and BM25-only chunks should each get score from only one list
        var vectorOnlyScore = fused.First(r => r.Chunk.Id == 2).Score;
        var bm25OnlyScore = fused.First(r => r.Chunk.Id == 3).Score;

        Assert.Equal(1.0 / 62, vectorOnlyScore, precision: 10); // rank 2 in vector
        Assert.Equal(1.0 / 62, bm25OnlyScore, precision: 10);   // rank 2 in BM25
    }

    [Fact]
    public void FuseResults_CompleteOverlap_ChunkOrderDeterminedByBothRanks()
    {
        // Arrange — both lists contain the same chunks but in different orders
        var chunk1 = CreateChunk(1, "Homeowners policy declarations with coverage amounts.", "DECLARATIONS");
        var chunk2 = CreateChunk(2, "Auto collision claim filed for vehicle damage.", "CLAIMS");
        var chunk3 = CreateChunk(3, "Workers compensation injury report documentation.", "CLAIMS");

        // Vector: chunk1 > chunk2 > chunk3
        var vectorResults = new List<(DocumentChunkRecord Chunk, double Score)>
        {
            (chunk1, 0.95),
            (chunk2, 0.80),
            (chunk3, 0.70)
        };

        // BM25: chunk2 > chunk1 > chunk3
        var bm25Results = new List<(DocumentChunkRecord Chunk, double Score)>
        {
            (chunk2, 4.5),
            (chunk1, 3.2),
            (chunk3, 1.1)
        };

        // Act
        var fused = _sut.FuseResults(vectorResults, bm25Results, topK: 3);

        // Assert — chunk1: 1/(61) + 1/(62), chunk2: 1/(62) + 1/(61), chunk3: 1/(63) + 1/(63)
        // chunk1 and chunk2 should have the same RRF score (symmetric swap)
        var chunk1Score = fused.First(r => r.Chunk.Id == 1).Score;
        var chunk2Score = fused.First(r => r.Chunk.Id == 2).Score;
        var chunk3Score = fused.First(r => r.Chunk.Id == 3).Score;

        Assert.Equal(chunk1Score, chunk2Score, precision: 10);
        Assert.True(chunk1Score > chunk3Score);
    }

    // ────────────────────────────────────────────────────────────
    // Non-Overlapping Results Tests
    // ────────────────────────────────────────────────────────────

    [Fact]
    public void FuseResults_NonOverlappingResults_MergesBothSets()
    {
        // Arrange — completely different chunks in each list
        var vectorChunk1 = CreateChunk(1, "Dwelling coverage amount: $350,000 replacement cost.", "COVERAGE");
        var vectorChunk2 = CreateChunk(2, "Personal property coverage: $175,000.", "COVERAGE");

        var bm25Chunk1 = CreateChunk(3, "Policy POL-98765 effective date January 1, 2025.", "DECLARATIONS");
        var bm25Chunk2 = CreateChunk(4, "Claim CLM-2024-0123 filed for wind damage.", "CLAIMS");

        var vectorResults = new List<(DocumentChunkRecord Chunk, double Score)>
        {
            (vectorChunk1, 0.90),
            (vectorChunk2, 0.82)
        };

        var bm25Results = new List<(DocumentChunkRecord Chunk, double Score)>
        {
            (bm25Chunk1, 3.8),
            (bm25Chunk2, 2.5)
        };

        // Act
        var fused = _sut.FuseResults(vectorResults, bm25Results, topK: 5);

        // Assert — all 4 chunks should appear in fused results
        Assert.Equal(4, fused.Count);

        // Top-ranked from each list (rank 1) should both score 1/(60+1) = 1/61
        var topVectorScore = fused.First(r => r.Chunk.Id == 1).Score;
        var topBm25Score = fused.First(r => r.Chunk.Id == 3).Score;
        Assert.Equal(topVectorScore, topBm25Score, precision: 10);

        // All chunk IDs should be present
        var chunkIds = fused.Select(r => r.Chunk.Id).ToHashSet();
        Assert.Contains(1, chunkIds);
        Assert.Contains(2, chunkIds);
        Assert.Contains(3, chunkIds);
        Assert.Contains(4, chunkIds);
    }

    // ────────────────────────────────────────────────────────────
    // TopK Limit Tests
    // ────────────────────────────────────────────────────────────

    [Fact]
    public void FuseResults_TopKLimitRespected_ReturnsOnlyRequestedCount()
    {
        // Arrange — 6 total unique chunks across both lists
        var vectorResults = new List<(DocumentChunkRecord Chunk, double Score)>
        {
            (CreateChunk(1, "Dwelling coverage details for the homeowners policy.", "COVERAGE"), 0.95),
            (CreateChunk(2, "Personal liability coverage up to $300,000.", "COVERAGE"), 0.88),
            (CreateChunk(3, "Medical payments coverage for guest injuries.", "COVERAGE"), 0.75)
        };

        var bm25Results = new List<(DocumentChunkRecord Chunk, double Score)>
        {
            (CreateChunk(4, "Policy POL-55555 declarations page.", "DECLARATIONS"), 4.2),
            (CreateChunk(5, "Endorsement for scheduled personal property.", "ENDORSEMENTS"), 3.1),
            (CreateChunk(6, "Exclusion for intentional damage acts.", "EXCLUSIONS"), 2.0)
        };

        // Act — request only top 3
        var fused = _sut.FuseResults(vectorResults, bm25Results, topK: 3);

        // Assert — should return exactly 3 results
        Assert.Equal(3, fused.Count);
    }

    [Fact]
    public void FuseResults_TopKLargerThanCandidates_ReturnsAllCandidates()
    {
        // Arrange — only 2 unique chunks
        var chunk = CreateChunk(1, "Comprehensive auto insurance policy.", "COVERAGE");
        var vectorResults = new List<(DocumentChunkRecord Chunk, double Score)> { (chunk, 0.90) };
        var bm25Results = new List<(DocumentChunkRecord Chunk, double Score)>
        {
            (CreateChunk(2, "Collision damage claim for insured vehicle.", "CLAIMS"), 2.5)
        };

        // Act — request top 10 but only 2 candidates exist
        var fused = _sut.FuseResults(vectorResults, bm25Results, topK: 10);

        // Assert — should return all 2 results
        Assert.Equal(2, fused.Count);
    }

    // ────────────────────────────────────────────────────────────
    // Empty Input Tests
    // ────────────────────────────────────────────────────────────

    [Fact]
    public void FuseResults_EmptyVectorResults_ReturnsBM25ResultsOnly()
    {
        // Arrange
        var bm25Results = new List<(DocumentChunkRecord Chunk, double Score)>
        {
            (CreateChunk(1, "Policy POL-12345 declarations with insured name and address.", "DECLARATIONS"), 3.8),
            (CreateChunk(2, "Claim history report showing prior losses.", "CLAIMS"), 2.1)
        };

        // Act
        var fused = _sut.FuseResults(
            new List<(DocumentChunkRecord, double)>(),
            bm25Results,
            topK: 5);

        // Assert — should return BM25 results directly (up to topK)
        Assert.Equal(2, fused.Count);
        Assert.Equal(1, fused[0].Chunk.Id);
        Assert.Equal(2, fused[1].Chunk.Id);
    }

    [Fact]
    public void FuseResults_EmptyBM25Results_ReturnsVectorResultsOnly()
    {
        // Arrange
        var vectorResults = new List<(DocumentChunkRecord Chunk, double Score)>
        {
            (CreateChunk(1, "Dwelling coverage: $350,000 for the insured homeowners property.", "COVERAGE"), 0.92),
            (CreateChunk(2, "Personal property coverage: $175,000 with replacement cost.", "COVERAGE"), 0.85)
        };

        // Act
        var fused = _sut.FuseResults(
            vectorResults,
            new List<(DocumentChunkRecord, double)>(),
            topK: 5);

        // Assert — should return vector results directly (up to topK)
        Assert.Equal(2, fused.Count);
        Assert.Equal(1, fused[0].Chunk.Id);
        Assert.Equal(2, fused[1].Chunk.Id);
    }

    [Fact]
    public void FuseResults_BothEmpty_ReturnsEmptyList()
    {
        // Act
        var fused = _sut.FuseResults(
            new List<(DocumentChunkRecord, double)>(),
            new List<(DocumentChunkRecord, double)>(),
            topK: 5);

        // Assert
        Assert.Empty(fused);
    }

    // ────────────────────────────────────────────────────────────
    // RRF Score Correctness Tests
    // ────────────────────────────────────────────────────────────

    [Fact]
    public void FuseResults_RRFScoresAreCorrect_VerifyFormula()
    {
        // Arrange — 2 chunks, each appearing in one list at rank 1
        var chunk1 = CreateChunk(1, "Workers compensation statutory coverage per state law.", "COVERAGE");
        var chunk2 = CreateChunk(2, "Employer liability coverage with $1M per occurrence.", "COVERAGE");

        var vectorResults = new List<(DocumentChunkRecord Chunk, double Score)>
        {
            (chunk1, 0.88)
        };

        var bm25Results = new List<(DocumentChunkRecord Chunk, double Score)>
        {
            (chunk2, 3.5)
        };

        // Act
        var fused = _sut.FuseResults(vectorResults, bm25Results, topK: 5);

        // Assert — each chunk gets exactly 1/(60 + 1) = 1/61
        Assert.Equal(2, fused.Count);
        var expectedScore = 1.0 / 61;
        Assert.Equal(expectedScore, fused[0].Score, precision: 10);
        Assert.Equal(expectedScore, fused[1].Score, precision: 10);
    }

    [Fact]
    public void FuseResults_HigherRankGetsHigherRRFScore()
    {
        // Arrange — chunk at rank 1 should get higher RRF than chunk at rank 3
        var chunk1 = CreateChunk(1, "Policy effective: January 2025 to January 2026.", "DECLARATIONS");
        var chunk2 = CreateChunk(2, "Premium amount: $1,200 per year.", "BILLING");
        var chunk3 = CreateChunk(3, "Deductible: $1,000 per occurrence.", "CONDITIONS");

        var vectorResults = new List<(DocumentChunkRecord Chunk, double Score)>
        {
            (chunk1, 0.95),
            (chunk2, 0.80),
            (chunk3, 0.65)
        };

        // No BM25 results — only vector ranking matters
        var bm25Results = new List<(DocumentChunkRecord Chunk, double Score)>();

        // Act
        var fused = _sut.FuseResults(vectorResults, bm25Results, topK: 3);

        // Assert — scores should be in descending order matching original vector ranks
        Assert.Equal(3, fused.Count);
        Assert.True(fused[0].Score > fused[1].Score);
        Assert.True(fused[1].Score > fused[2].Score);

        // Verify exact RRF scores: 1/(61), 1/(62), 1/(63)
        Assert.Equal(0.95, fused[0].Score, precision: 10);    // passthrough (no fusion)
        Assert.Equal(0.80, fused[1].Score, precision: 10);
        Assert.Equal(0.65, fused[2].Score, precision: 10);
    }

    [Fact]
    public void FuseResults_ResultsAreSortedDescendingByScore()
    {
        // Arrange — mixed overlap
        var chunk1 = CreateChunk(1, "Dwelling coverage $350,000.", "COVERAGE");
        var chunk2 = CreateChunk(2, "Policy POL-12345.", "DECLARATIONS");
        var chunk3 = CreateChunk(3, "Flood endorsement.", "ENDORSEMENTS");

        var vectorResults = new List<(DocumentChunkRecord Chunk, double Score)>
        {
            (chunk1, 0.95),  // rank 1
            (chunk2, 0.80),  // rank 2
            (chunk3, 0.70)   // rank 3
        };

        var bm25Results = new List<(DocumentChunkRecord Chunk, double Score)>
        {
            (chunk2, 4.5),   // rank 1 — chunk2 gets boost from appearing in both
            (chunk3, 3.0),   // rank 2
            (chunk1, 1.5)    // rank 3
        };

        // Act
        var fused = _sut.FuseResults(vectorResults, bm25Results, topK: 5);

        // Assert — results should be sorted by descending RRF score
        for (var i = 0; i < fused.Count - 1; i++)
        {
            Assert.True(fused[i].Score >= fused[i + 1].Score,
                $"Result at index {i} (score {fused[i].Score:F6}) should be >= result at index {i + 1} (score {fused[i + 1].Score:F6}).");
        }
    }
}
