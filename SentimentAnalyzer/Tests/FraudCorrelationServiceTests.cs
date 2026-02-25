using Microsoft.Extensions.Logging;
using Moq;
using SentimentAnalyzer.Agents.Orchestration;
using SentimentAnalyzer.API.Data;
using SentimentAnalyzer.API.Data.Entities;
using SentimentAnalyzer.API.Models;
using SentimentAnalyzer.API.Services.Embeddings;
using SentimentAnalyzer.API.Services.Fraud;
using Xunit;

namespace SentimentAnalyzer.Tests;

/// <summary>
/// Tests for <see cref="FraudCorrelationService"/> — cross-claim fraud correlation analysis.
/// Covers all four correlation strategies (date proximity, shared fraud flags, same severity,
/// narrative similarity via embeddings) plus the 2-indicator minimum requirement, self-correlation
/// prevention, cap at 20 correlations, and read-through query methods.
/// </summary>
public class FraudCorrelationServiceTests
{
    private readonly Mock<IClaimsRepository> _mockClaimsRepo;
    private readonly Mock<IFraudCorrelationRepository> _mockCorrelationRepo;
    private readonly Mock<IEmbeddingService> _mockEmbeddingService;
    private readonly Mock<IPIIRedactor> _mockPiiRedactor;
    private readonly Mock<ILogger<FraudCorrelationService>> _mockLogger;
    private readonly FraudCorrelationService _service;

    public FraudCorrelationServiceTests()
    {
        _mockClaimsRepo = new Mock<IClaimsRepository>();
        _mockCorrelationRepo = new Mock<IFraudCorrelationRepository>();
        _mockEmbeddingService = new Mock<IEmbeddingService>();
        _mockPiiRedactor = new Mock<IPIIRedactor>();
        _mockLogger = new Mock<ILogger<FraudCorrelationService>>();

        // PII redactor returns text unchanged by default (tests focus on correlation logic)
        _mockPiiRedactor.Setup(p => p.Redact(It.IsAny<string>())).Returns((string s) => s);

        // Default: batch embedding returns empty (no narrative similarity) unless overridden
        _mockEmbeddingService
            .Setup(e => e.GenerateBatchEmbeddingsAsync(It.IsAny<string[]>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BatchEmbeddingResult { IsSuccess = true, Embeddings = [] });

        // Default: single embedding returns empty unless overridden
        _mockEmbeddingService
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { IsSuccess = true, Embedding = [] });

        _service = new FraudCorrelationService(
            _mockClaimsRepo.Object,
            _mockCorrelationRepo.Object,
            _mockEmbeddingService.Object,
            _mockPiiRedactor.Object,
            _mockLogger.Object);
    }

    // =========================================================================
    // Test 1: Date Proximity Correlation
    // =========================================================================

    [Fact]
    public async Task AnalyzeCorrelationsAsync_WithDateProximity_DetectsCorrelation()
    {
        // Arrange — two Property claims 30 days apart AND same severity with high fraud (2 indicators)
        var sourceClaim = CreateClaim(1, "Water damage from burst pipe in basement",
            claimType: "Property", createdAt: new DateTime(2025, 3, 1),
            severity: "High", fraudScore: 70);
        var candidateClaim = CreateClaim(2, "Water damage from roof leak in attic",
            claimType: "Property", createdAt: new DateTime(2025, 3, 31),
            severity: "High", fraudScore: 75);

        SetupClaimsRepo(sourceClaim, [candidateClaim]);

        // Act
        var results = await _service.AnalyzeCorrelationsAsync(1);

        // Assert — should detect correlation because we have DateProximity + SameSeverity (2 indicators)
        Assert.Single(results);
        Assert.Equal(1, results[0].SourceClaimId);
        Assert.Equal(2, results[0].CorrelatedClaimId);
        Assert.Contains("DateProximity", results[0].CorrelationType);
        Assert.True(results[0].CorrelationScore > 0);
        _mockCorrelationRepo.Verify(
            r => r.SaveCorrelationsAsync(It.IsAny<List<FraudCorrelationRecord>>()),
            Times.Once);
    }

    // =========================================================================
    // Test 2: Shared Fraud Flags Correlation
    // =========================================================================

    [Fact]
    public async Task AnalyzeCorrelationsAsync_WithSharedFlags_DetectsCorrelation()
    {
        // Arrange — two claims sharing 3 fraud flags AND same severity/high fraud (2+ indicators)
        var sharedFlags = "[\"Exaggerated damages\",\"Late reporting\",\"Prior claim history\"]";
        var candidateFlags = "[\"Exaggerated damages\",\"Late reporting\",\"Prior claim history\",\"Inconsistent statements\"]";

        var sourceClaim = CreateClaim(10, "Suspicious fire damage claim filed two weeks after policy upgrade",
            claimType: "Property", fraudFlags: sharedFlags,
            severity: "Critical", fraudScore: 82);
        var candidateClaim = CreateClaim(11, "Fire damage claim with inconsistent witness statements",
            claimType: "Auto", fraudFlags: candidateFlags,
            severity: "Critical", fraudScore: 88);

        SetupClaimsRepo(sourceClaim, [candidateClaim]);

        // Act
        var results = await _service.AnalyzeCorrelationsAsync(10);

        // Assert — SharedFlags (3 in common) + SameSeverity (both Critical, both >60 fraud score)
        Assert.Single(results);
        Assert.Contains("SharedFlags", results[0].CorrelationType);
        Assert.Equal(10, results[0].SourceClaimId);
        Assert.Equal(11, results[0].CorrelatedClaimId);
        Assert.True(results[0].CorrelationScore > 0);
    }

    // =========================================================================
    // Test 3: Single Indicator Not Flagged (Requires 2+)
    // =========================================================================

    [Fact]
    public async Task AnalyzeCorrelationsAsync_RequiresTwoIndicators_SingleIndicatorNotFlagged()
    {
        // Arrange — two claims with same type and within 90 days (DateProximity only, 1 indicator)
        // Different severity and low fraud score = no SameSeverity
        // No shared fraud flags
        var sourceClaim = CreateClaim(20, "Fender bender on Highway 101",
            claimType: "Auto", createdAt: new DateTime(2025, 5, 1),
            severity: "Low", fraudScore: 10);
        var candidateClaim = CreateClaim(21, "Rear-ended at traffic light on Route 66",
            claimType: "Auto", createdAt: new DateTime(2025, 5, 15),
            severity: "Medium", fraudScore: 15);

        SetupClaimsRepo(sourceClaim, [candidateClaim]);

        // Act
        var results = await _service.AnalyzeCorrelationsAsync(20);

        // Assert — only DateProximity found (1 indicator), so no correlation created
        Assert.Empty(results);
        _mockCorrelationRepo.Verify(
            r => r.SaveCorrelationsAsync(It.IsAny<IEnumerable<FraudCorrelationRecord>>()),
            Times.Never);
    }

    // =========================================================================
    // Test 4: Self-Correlation Prevention
    // =========================================================================

    [Fact]
    public async Task AnalyzeCorrelationsAsync_DoesNotCorrelateSameClaimWithItself()
    {
        // Arrange — the source claim appears in the candidates list (same ID = 30)
        var sourceClaim = CreateClaim(30, "Hail damage to vehicle roof and windshield",
            claimType: "Auto", severity: "High", fraudScore: 80,
            fraudFlags: "[\"Exaggerated damages\",\"Timing suspicious\"]");

        // GetClaimsAsync returns all claims including the source
        _mockClaimsRepo.Setup(r => r.GetClaimByIdAsync(30)).ReturnsAsync(sourceClaim);
        _mockClaimsRepo.Setup(r => r.GetClaimsAsync(It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), 100, 1))
            .ReturnsAsync(([sourceClaim], 1));

        // Act
        var results = await _service.AnalyzeCorrelationsAsync(30);

        // Assert — source claim filtered out; no candidates, no correlations
        Assert.Empty(results);
    }

    // =========================================================================
    // Test 5: Cap at 20 Correlations
    // =========================================================================

    [Fact]
    public async Task AnalyzeCorrelationsAsync_CapsAt20Correlations()
    {
        // Arrange — source claim + 25 candidates, all with 2+ indicators
        var sourceClaim = CreateClaim(40, "Large commercial property fire claim",
            claimType: "Property", createdAt: new DateTime(2025, 6, 1),
            severity: "Critical", fraudScore: 90,
            fraudFlags: "[\"Exaggerated damages\",\"Late reporting\"]");

        var candidates = new List<ClaimRecord>();
        for (var i = 1; i <= 25; i++)
        {
            candidates.Add(CreateClaim(40 + i,
                $"Related property fire claim #{i} in same region",
                claimType: "Property",
                createdAt: new DateTime(2025, 6, 1).AddDays(i), // All within 90-day window
                severity: "Critical", fraudScore: 90 - i, // All >= 60
                fraudFlags: "[\"Exaggerated damages\",\"Late reporting\"]"));
        }

        SetupClaimsRepo(sourceClaim, candidates);

        // Act
        var results = await _service.AnalyzeCorrelationsAsync(40);

        // Assert — capped at 20 even though 25 candidates qualified
        Assert.Equal(20, results.Count);
        _mockCorrelationRepo.Verify(
            r => r.SaveCorrelationsAsync(It.Is<List<FraudCorrelationRecord>>(list => list.Count == 20)),
            Times.Once);
    }

    // =========================================================================
    // Test 6: Narrative Similarity via Embeddings
    // =========================================================================

    [Fact]
    public async Task AnalyzeCorrelationsAsync_SimilarNarrative_UsesEmbeddings()
    {
        // Arrange — two claims with similar text. Also give them DateProximity+SameType so
        // the SimilarNarrative indicator (if triggered) combined with DateProximity = 2 indicators.
        var sourceClaim = CreateClaim(50, "Policyholder reported roof collapse after heavy snowfall. Adjuster found pre-existing damage.",
            claimType: "Property", createdAt: new DateTime(2025, 1, 15),
            severity: "Medium", fraudScore: 30);
        var candidateClaim = CreateClaim(51, "Insured reported roof damage following winter storm. Inspector noted prior deterioration.",
            claimType: "Property", createdAt: new DateTime(2025, 2, 10),
            severity: "Low", fraudScore: 25);

        SetupClaimsRepo(sourceClaim, [candidateClaim]);

        // Mock embedding service to return high-similarity vectors
        var sourceEmbedding = new float[] { 0.9f, 0.1f, 0.0f, 0.0f };
        var candidateEmbedding = new float[] { 0.88f, 0.12f, 0.0f, 0.0f }; // cosine similarity ~0.9998

        _mockEmbeddingService
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), "document", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { IsSuccess = true, Embedding = sourceEmbedding });

        _mockEmbeddingService
            .Setup(e => e.GenerateBatchEmbeddingsAsync(It.IsAny<string[]>(), "document", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BatchEmbeddingResult { IsSuccess = true, Embeddings = [candidateEmbedding] });

        // Act
        var results = await _service.AnalyzeCorrelationsAsync(50);

        // Assert — verify embedding service was called for narrative comparison
        _mockEmbeddingService.Verify(
            e => e.GenerateEmbeddingAsync(It.IsAny<string>(), "document", It.IsAny<CancellationToken>()),
            Times.Once);
        _mockEmbeddingService.Verify(
            e => e.GenerateBatchEmbeddingsAsync(It.IsAny<string[]>(), "document", It.IsAny<CancellationToken>()),
            Times.Once);

        // PII redactor called for both source and candidate texts before embedding
        _mockPiiRedactor.Verify(p => p.Redact(It.IsAny<string>()), Times.AtLeast(2));

        // DateProximity (same type, within 90 days) + SimilarNarrative = 2 indicators => correlation
        Assert.Single(results);
        Assert.Contains("DateProximity", results[0].CorrelationType);
        Assert.Contains("SimilarNarrative", results[0].CorrelationType);
    }

    // =========================================================================
    // Test 7: GetCorrelationsAsync Reads from Repository
    // =========================================================================

    [Fact]
    public async Task GetCorrelationsAsync_ReturnsStoredCorrelations()
    {
        // Arrange
        var storedRecords = new List<FraudCorrelationRecord>
        {
            new()
            {
                Id = 1,
                SourceClaimId = 100,
                CorrelatedClaimId = 101,
                CorrelationType = "DateProximity+SharedFlags",
                CorrelationScore = 0.78,
                Details = "Same claim type (Auto), 14 days apart | 2 shared fraud flags: Late reporting, Exaggerated damages",
                DetectedAt = new DateTime(2025, 4, 10),
                CorrelatedClaim = CreateClaim(101, "Correlated auto claim with matching flags",
                    claimType: "Auto", severity: "High", fraudScore: 72)
            },
            new()
            {
                Id = 2,
                SourceClaimId = 100,
                CorrelatedClaimId = 102,
                CorrelationType = "SameSeverity+SharedFlags",
                CorrelationScore = 0.65,
                Details = "Same severity (Critical) | 3 shared fraud flags",
                DetectedAt = new DateTime(2025, 4, 11),
                CorrelatedClaim = CreateClaim(102, "Another correlated claim with severity match",
                    claimType: "Property", severity: "Critical", fraudScore: 80)
            }
        };

        _mockCorrelationRepo.Setup(r => r.GetByClaimIdAsync(100, 1, 20))
            .ReturnsAsync((storedRecords, storedRecords.Count));

        // Act
        var result = await _service.GetCorrelationsAsync(100);

        // Assert
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(100, result.Items[0].SourceClaimId);
        Assert.Equal(101, result.Items[0].CorrelatedClaimId);
        Assert.Equal("DateProximity+SharedFlags", result.Items[0].CorrelationType);
        Assert.Equal(0.78, result.Items[0].CorrelationScore);
        Assert.Equal("High", result.Items[0].CorrelatedClaimSeverity);
        Assert.Equal("Auto", result.Items[0].CorrelatedClaimType);
        Assert.Equal(72, result.Items[0].CorrelatedFraudScore);

        Assert.Equal(102, result.Items[1].CorrelatedClaimId);
        Assert.Equal("Critical", result.Items[1].CorrelatedClaimSeverity);
        _mockCorrelationRepo.Verify(r => r.GetByClaimIdAsync(100, 1, 20), Times.Once);
    }

    // =========================================================================
    // Test 8: GetAllCorrelationsAsync Filters by MinScore
    // =========================================================================

    [Fact]
    public async Task GetAllCorrelationsAsync_FiltersByMinScore()
    {
        // Arrange
        var highScoreRecords = new List<FraudCorrelationRecord>
        {
            new()
            {
                Id = 10,
                SourceClaimId = 200,
                CorrelatedClaimId = 201,
                CorrelationType = "DateProximity+SameSeverity+SharedFlags",
                CorrelationScore = 0.92,
                Details = "Strong multi-indicator correlation",
                DetectedAt = new DateTime(2025, 5, 1),
                CorrelatedClaim = CreateClaim(201, "High-confidence correlated claim",
                    claimType: "Property", severity: "Critical", fraudScore: 95)
            }
        };

        _mockCorrelationRepo
            .Setup(r => r.GetAllAsync(0.7, 1, 50))
            .ReturnsAsync((highScoreRecords, highScoreRecords.Count));

        // Act
        var result = await _service.GetAllCorrelationsAsync(minScore: 0.7, pageSize: 50);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(0.92, result.Items[0].CorrelationScore);
        Assert.Equal("Property", result.Items[0].CorrelatedClaimType);
        Assert.Equal(95, result.Items[0].CorrelatedFraudScore);
        _mockCorrelationRepo.Verify(r => r.GetAllAsync(0.7, 1, 50), Times.Once);
    }

    // =========================================================================
    // Test 9: Claim Not Found Throws KeyNotFoundException
    // =========================================================================

    [Fact]
    public async Task AnalyzeCorrelationsAsync_ClaimNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        _mockClaimsRepo.Setup(r => r.GetClaimByIdAsync(999)).ReturnsAsync((ClaimRecord?)null);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.AnalyzeCorrelationsAsync(999));
    }

    // =========================================================================
    // Test 10: No Candidates Returns Empty List
    // =========================================================================

    [Fact]
    public async Task AnalyzeCorrelationsAsync_NoCandidates_ReturnsEmptyList()
    {
        // Arrange — only one claim in the system
        var sourceClaim = CreateClaim(60, "Sole claim in the system — water damage to warehouse",
            claimType: "Property");
        _mockClaimsRepo.Setup(r => r.GetClaimByIdAsync(60)).ReturnsAsync(sourceClaim);
        _mockClaimsRepo.Setup(r => r.GetClaimsAsync(It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), 100, 1))
            .ReturnsAsync(([sourceClaim], 1));

        // Act
        var results = await _service.AnalyzeCorrelationsAsync(60);

        // Assert
        Assert.Empty(results);
        _mockCorrelationRepo.Verify(
            r => r.SaveCorrelationsAsync(It.IsAny<IEnumerable<FraudCorrelationRecord>>()),
            Times.Never);
    }

    // =========================================================================
    // Test 11: SameSeverity + High Fraud Score Strategy
    // =========================================================================

    [Fact]
    public async Task AnalyzeCorrelationsAsync_SameSeverityHighFraud_DetectsCorrelation()
    {
        // Arrange — two claims with same severity, both high fraud score, AND shared flags (2 indicators)
        var sourceClaim = CreateClaim(70, "Suspected arson at commercial warehouse",
            claimType: "Property", createdAt: new DateTime(2025, 1, 1),
            severity: "Critical", fraudScore: 85,
            fraudFlags: "[\"Timing suspicious\",\"Financial motive\"]");
        var candidateClaim = CreateClaim(71, "Warehouse fire shortly after inventory insurance increase",
            claimType: "Liability", createdAt: new DateTime(2025, 7, 1), // >90 days apart, different type
            severity: "Critical", fraudScore: 90,
            fraudFlags: "[\"Timing suspicious\",\"Financial motive\",\"Prior claims\"]");

        SetupClaimsRepo(sourceClaim, [candidateClaim]);

        // Act
        var results = await _service.AnalyzeCorrelationsAsync(70);

        // Assert — SameSeverity (both Critical, both >60) + SharedFlags (2 shared) = 2 indicators
        Assert.Single(results);
        Assert.Contains("SameSeverity", results[0].CorrelationType);
        Assert.Contains("SharedFlags", results[0].CorrelationType);
        Assert.True(results[0].CorrelationScore > 0);
    }

    // =========================================================================
    // Test 12: Embedding Service Failure Does Not Crash Analysis
    // =========================================================================

    [Fact]
    public async Task AnalyzeCorrelationsAsync_EmbeddingServiceFails_ContinuesWithOtherStrategies()
    {
        // Arrange — claims with DateProximity + SameSeverity (2 indicators without embeddings)
        var sourceClaim = CreateClaim(80, "Vehicle theft from parking garage",
            claimType: "Auto", createdAt: new DateTime(2025, 4, 1),
            severity: "High", fraudScore: 75);
        var candidateClaim = CreateClaim(81, "Vehicle theft from shopping center lot",
            claimType: "Auto", createdAt: new DateTime(2025, 4, 20),
            severity: "High", fraudScore: 70);

        SetupClaimsRepo(sourceClaim, [candidateClaim]);

        // Embedding service throws exception
        _mockEmbeddingService
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Embedding API unavailable"));

        // Act — should not throw
        var results = await _service.AnalyzeCorrelationsAsync(80);

        // Assert — correlation still detected via DateProximity + SameSeverity
        Assert.Single(results);
        Assert.Contains("DateProximity", results[0].CorrelationType);
        Assert.Contains("SameSeverity", results[0].CorrelationType);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    /// <summary>
    /// Creates a realistic <see cref="ClaimRecord"/> for testing with insurance-domain data.
    /// </summary>
    private static ClaimRecord CreateClaim(
        int id,
        string claimText,
        string claimType = "Property",
        DateTime? createdAt = null,
        string severity = "Medium",
        double fraudScore = 0,
        string fraudRiskLevel = "VeryLow",
        string fraudFlags = "[]")
    {
        return new ClaimRecord
        {
            Id = id,
            ClaimText = claimText,
            ClaimType = claimType,
            CreatedAt = createdAt ?? DateTime.UtcNow,
            Severity = severity,
            Urgency = "Standard",
            Status = "Triaged",
            FraudScore = fraudScore,
            FraudRiskLevel = fraudRiskLevel,
            FraudFlagsJson = fraudFlags,
            Evidence = [],
            Actions = []
        };
    }

    /// <summary>
    /// Sets up the claims repository mocks to return the source claim and candidates.
    /// </summary>
    private void SetupClaimsRepo(ClaimRecord sourceClaim, List<ClaimRecord> candidates)
    {
        _mockClaimsRepo.Setup(r => r.GetClaimByIdAsync(sourceClaim.Id)).ReturnsAsync(sourceClaim);

        var allClaims = new List<ClaimRecord> { sourceClaim };
        allClaims.AddRange(candidates);

        _mockClaimsRepo.Setup(r => r.GetClaimsAsync(
                It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                100, 1))
            .ReturnsAsync((allClaims, allClaims.Count));
    }
}
