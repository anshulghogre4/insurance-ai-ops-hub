using SentimentAnalyzer.API.Data.Entities;
using SentimentAnalyzer.API.Services.Documents;

namespace SentimentAnalyzer.Tests;

/// <summary>
/// Unit tests for BM25Scorer — a pure C# BM25 implementation for sparse text retrieval
/// in the hybrid RAG pipeline. Verifies IDF calculation, term frequency scoring,
/// stopword filtering, and ranking behavior with realistic insurance data.
/// </summary>
public class BM25ScorerTests
{
    /// <summary>
    /// Helper to create a DocumentChunkRecord with specified content for test scenarios.
    /// Uses realistic insurance document metadata.
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
    // IDF Calculation & Ranking Tests
    // ────────────────────────────────────────────────────────────

    [Fact]
    public void Score_QueryWithExactPolicyNumber_RanksMatchingChunkHigher()
    {
        // Arrange — chunk 1 contains the exact policy number, chunk 2 does not
        var candidates = new List<DocumentChunkRecord>
        {
            CreateChunk(1, "Policy Number POL-12345 was issued on January 15, 2025 for the insured property at 123 Main Street.", "DECLARATIONS"),
            CreateChunk(2, "Coverage A provides dwelling protection up to the stated replacement cost value of the insured structure.", "COVERAGE"),
            CreateChunk(3, "The deductible for wind and hail damage claims is set at $2,500 per occurrence as stated in the endorsement.", "ENDORSEMENTS")
        };

        // Act
        var results = BM25Scorer.Score("policy number POL-12345", candidates);

        // Assert — chunk 1 should score highest because it contains the exact policy number
        Assert.Equal(3, results.Count);
        Assert.Equal(1, results[0].Chunk.Id);
        Assert.True(results[0].Score > results[1].Score,
            "Chunk with exact policy number match should score higher than chunk without it.");
        Assert.True(results[0].Score > 0, "Matching chunk should have a positive BM25 score.");
    }

    [Fact]
    public void Score_IdfCalculation_RareTermsScoreHigher()
    {
        // Arrange — "endorsement" appears in only 1 doc, "coverage" appears in all 3
        var candidates = new List<DocumentChunkRecord>
        {
            CreateChunk(1, "Coverage limits for dwelling protection under this homeowners policy section.", "COVERAGE"),
            CreateChunk(2, "Coverage details including personal property and liability protection amounts.", "COVERAGE"),
            CreateChunk(3, "Endorsement HO-340 provides additional flood coverage for the insured dwelling.", "ENDORSEMENTS")
        };

        // Query for "endorsement" — rare term should have higher IDF
        var endorsementResults = BM25Scorer.Score("endorsement", candidates);

        // Query for "coverage" — common term should have lower IDF
        var coverageResults = BM25Scorer.Score("coverage", candidates);

        // Assert — the single endorsement match should have a higher BM25 score than
        // any individual coverage match because "endorsement" has higher IDF
        var topEndorsementScore = endorsementResults.Max(r => r.Score);
        var topCoverageScore = coverageResults.Max(r => r.Score);

        Assert.True(topEndorsementScore > topCoverageScore,
            "Rare term 'endorsement' should produce higher BM25 score than common term 'coverage'.");
    }

    // ────────────────────────────────────────────────────────────
    // Edge Case Tests
    // ────────────────────────────────────────────────────────────

    [Fact]
    public void Score_EmptyQuery_ReturnsZeroScores()
    {
        // Arrange
        var candidates = new List<DocumentChunkRecord>
        {
            CreateChunk(1, "Dwelling Coverage: $350,000 for the insured homeowners property.", "COVERAGE"),
            CreateChunk(2, "Personal Property: $175,000 with replacement cost valuation.", "COVERAGE")
        };

        // Act
        var results = BM25Scorer.Score("", candidates);

        // Assert — all scores should be zero
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal(0.0, r.Score));
    }

    [Fact]
    public void Score_WhitespaceOnlyQuery_ReturnsZeroScores()
    {
        // Arrange
        var candidates = new List<DocumentChunkRecord>
        {
            CreateChunk(1, "Auto insurance comprehensive coverage with collision protection.", "COVERAGE")
        };

        // Act
        var results = BM25Scorer.Score("   \t\n  ", candidates);

        // Assert
        Assert.Single(results);
        Assert.Equal(0.0, results[0].Score);
    }

    [Fact]
    public void Score_SingleCandidate_ReturnsNonZeroScore()
    {
        // Arrange — single document with a matching term
        var candidates = new List<DocumentChunkRecord>
        {
            CreateChunk(1, "Workers compensation claim filed for workplace injury at the construction site on February 10, 2025.", "CLAIMS")
        };

        // Act
        var results = BM25Scorer.Score("workers compensation claim", candidates);

        // Assert — single candidate with matching terms should have a positive score
        Assert.Single(results);
        Assert.True(results[0].Score > 0,
            "Single candidate matching the query should have a positive BM25 score.");
    }

    [Fact]
    public void Score_NoCandidates_ReturnsEmptyList()
    {
        // Act
        var results = BM25Scorer.Score("policy coverage limits", new List<DocumentChunkRecord>());

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void Score_NoMatchingTerms_ReturnsZeroScores()
    {
        // Arrange — query terms do not appear in any candidate
        var candidates = new List<DocumentChunkRecord>
        {
            CreateChunk(1, "The insured dwelling is located at 456 Oak Avenue in Springfield.", "DECLARATIONS"),
            CreateChunk(2, "Premium payment schedule requires quarterly installments.", "BILLING")
        };

        // Act
        var results = BM25Scorer.Score("motorcycle liability umbrella", candidates);

        // Assert — no terms match, so all scores should be zero
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal(0.0, r.Score));
    }

    // ────────────────────────────────────────────────────────────
    // Stopword Filtering Tests
    // ────────────────────────────────────────────────────────────

    [Fact]
    public void Score_StopwordsAreFiltered_QueryOnlyScoresOnContentWords()
    {
        // Arrange — query "the policy" should filter out "the" and only score on "policy"
        var candidates = new List<DocumentChunkRecord>
        {
            CreateChunk(1, "Policy number HO-2024-001 was issued for homeowners coverage.", "DECLARATIONS"),
            CreateChunk(2, "Dwelling is protected against fire and windstorm perils.", "COVERAGE")
        };

        // Act — "the policy" should behave the same as just "policy"
        var withStopword = BM25Scorer.Score("the policy", candidates);
        var withoutStopword = BM25Scorer.Score("policy", candidates);

        // Assert — scores should be identical since "the" is a stopword
        Assert.Equal(withStopword.Count, withoutStopword.Count);
        for (var i = 0; i < withStopword.Count; i++)
        {
            Assert.Equal(withStopword[i].Chunk.Id, withoutStopword[i].Chunk.Id);
            Assert.Equal(withStopword[i].Score, withoutStopword[i].Score, precision: 10);
        }
    }

    [Fact]
    public void Score_AllStopwordsQuery_ReturnsZeroScores()
    {
        // Arrange — query consists entirely of stopwords
        var candidates = new List<DocumentChunkRecord>
        {
            CreateChunk(1, "Comprehensive auto insurance policy with collision coverage.", "COVERAGE")
        };

        // Act
        var results = BM25Scorer.Score("the is a an", candidates);

        // Assert — no content words remain after stopword filtering
        Assert.Single(results);
        Assert.Equal(0.0, results[0].Score);
    }

    // ────────────────────────────────────────────────────────────
    // Tokenization Tests
    // ────────────────────────────────────────────────────────────

    [Fact]
    public void Tokenize_InsuranceIdentifiers_PreservesHyphenatedTokens()
    {
        // Act
        var tokens = BM25Scorer.Tokenize("Policy POL-12345 and claim CLM-2024-001 were filed.");

        // Assert — hyphenated insurance identifiers should be preserved
        Assert.Contains("pol-12345", tokens);
        Assert.Contains("clm-2024-001", tokens);
        Assert.Contains("policy", tokens);
        Assert.Contains("claim", tokens);
        Assert.Contains("filed", tokens);
        // Stopwords should be filtered
        Assert.DoesNotContain("and", tokens);
        Assert.DoesNotContain("were", tokens);
    }

    [Fact]
    public void Tokenize_CaseInsensitive_ReturnsLowercaseTokens()
    {
        // Act
        var tokens = BM25Scorer.Tokenize("DECLARATIONS Coverage EXCLUSIONS");

        // Assert — all tokens should be lowercase
        Assert.All(tokens, t => Assert.Equal(t, t.ToLowerInvariant()));
        Assert.Contains("declarations", tokens);
        Assert.Contains("coverage", tokens);
        Assert.Contains("exclusions", tokens);
    }

    [Fact]
    public void Tokenize_EmptyString_ReturnsEmptyList()
    {
        // Act
        var tokens = BM25Scorer.Tokenize("");

        // Assert
        Assert.Empty(tokens);
    }

    // ────────────────────────────────────────────────────────────
    // Multi-Term Ranking Tests
    // ────────────────────────────────────────────────────────────

    [Fact]
    public void Score_MultipleQueryTerms_ChunkWithMoreMatchesScoresHigher()
    {
        // Arrange — chunk 1 matches 3 query terms, chunk 2 matches only 1
        var candidates = new List<DocumentChunkRecord>
        {
            CreateChunk(1, "Flood damage claim filed for water damage at the insured property after hurricane.", "CLAIMS"),
            CreateChunk(2, "General liability coverage limits for commercial property insurance.", "COVERAGE"),
            CreateChunk(3, "The flood endorsement provides coverage for water damage claims up to $250,000.", "ENDORSEMENTS")
        };

        // Act
        var results = BM25Scorer.Score("flood damage claim water", candidates);

        // Assert — chunks with more matching terms should score higher
        var chunk1Score = results.First(r => r.Chunk.Id == 1).Score;
        var chunk2Score = results.First(r => r.Chunk.Id == 2).Score;

        Assert.True(chunk1Score > chunk2Score,
            "Chunk matching more query terms should score higher.");
        Assert.True(chunk1Score > 0);
    }

    [Fact]
    public void Score_ClaimIdSearch_FindsExactMatch()
    {
        // Arrange — search for a specific claim ID
        var candidates = new List<DocumentChunkRecord>
        {
            CreateChunk(1, "Claim CLM-2024-0789 was filed on March 15, 2025 for auto collision damage.", "CLAIMS"),
            CreateChunk(2, "Claim CLM-2024-0456 involves a homeowners water damage incident.", "CLAIMS"),
            CreateChunk(3, "General claims processing guidelines and adjuster assignment procedures.", "PROCEDURES")
        };

        // Act
        var results = BM25Scorer.Score("CLM-2024-0789", candidates);

        // Assert — chunk with the exact claim ID should rank first
        Assert.Equal(1, results[0].Chunk.Id);
        Assert.True(results[0].Score > results[1].Score);
    }

    [Fact]
    public void Score_DateSearch_RanksChunkWithDateHigher()
    {
        // Arrange — search for a specific date
        var candidates = new List<DocumentChunkRecord>
        {
            CreateChunk(1, "Policy effective date: January 15, 2025 through January 15, 2026.", "DECLARATIONS"),
            CreateChunk(2, "Coverage A dwelling limit: $350,000 replacement cost.", "COVERAGE"),
            CreateChunk(3, "Premium payment due on the 15th of each month.", "BILLING")
        };

        // Act
        var results = BM25Scorer.Score("January 2025", candidates);

        // Assert — chunk with the date should score highest
        Assert.Equal(1, results[0].Chunk.Id);
        Assert.True(results[0].Score > 0);
    }
}
