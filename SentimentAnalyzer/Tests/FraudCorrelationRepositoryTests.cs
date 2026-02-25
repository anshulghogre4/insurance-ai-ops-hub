using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SentimentAnalyzer.API.Data;
using SentimentAnalyzer.API.Data.Entities;
using Xunit;

namespace SentimentAnalyzer.Tests;

/// <summary>
/// Tests for <see cref="SqliteFraudCorrelationRepository"/> using in-memory SQLite.
/// Covers correlation persistence, bidirectional lookup, min-score filtering,
/// and cascade deletion of correlations by claim ID.
/// </summary>
public class FraudCorrelationRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly InsuranceAnalysisDbContext _db;
    private readonly SqliteFraudCorrelationRepository _repo;

    public FraudCorrelationRepositoryTests()
    {
        // Use SQLite in-memory with shared connection (kept open for test duration)
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<InsuranceAnalysisDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new InsuranceAnalysisDbContext(options);
        _db.Database.EnsureCreated();

        var mockLogger = new Mock<ILogger<SqliteFraudCorrelationRepository>>();
        _repo = new SqliteFraudCorrelationRepository(_db, mockLogger.Object);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    // =========================================================================
    // Test 1: SaveCorrelationsAsync Persists Records
    // =========================================================================

    [Fact]
    public async Task SaveCorrelationsAsync_PersistsRecords()
    {
        // Arrange — create claims first (FK references needed)
        var claim1 = new ClaimRecord
        {
            ClaimText = "Basement flooding after heavy rain. Policy HO-2025-001234.",
            Severity = "High",
            ClaimType = "Property",
            FraudScore = 72
        };
        var claim2 = new ClaimRecord
        {
            ClaimText = "Roof leak during spring storms. Extensive water damage reported.",
            Severity = "High",
            ClaimType = "Property",
            FraudScore = 68
        };
        var claim3 = new ClaimRecord
        {
            ClaimText = "Foundation crack after earthquake. Structural assessment pending.",
            Severity = "Critical",
            ClaimType = "Property",
            FraudScore = 55
        };
        _db.Claims.AddRange(claim1, claim2, claim3);
        await _db.SaveChangesAsync();

        var correlations = new List<FraudCorrelationRecord>
        {
            new()
            {
                SourceClaimId = claim1.Id,
                CorrelatedClaimId = claim2.Id,
                CorrelationType = "DateProximity+SharedFlags",
                CorrelationScore = 0.82,
                Details = "Same claim type (Property), 21 days apart | 2 shared fraud flags: Late reporting, Exaggerated damages",
                DetectedAt = DateTime.UtcNow
            },
            new()
            {
                SourceClaimId = claim1.Id,
                CorrelatedClaimId = claim3.Id,
                CorrelationType = "DateProximity+SameSeverity",
                CorrelationScore = 0.65,
                Details = "Same claim type (Property), 45 days apart | Same severity (Critical)",
                DetectedAt = DateTime.UtcNow
            }
        };

        // Act
        await _repo.SaveCorrelationsAsync(correlations);

        // Assert
        var saved = await _db.FraudCorrelations.ToListAsync();
        Assert.Equal(2, saved.Count);
        Assert.Contains(saved, c => c.CorrelatedClaimId == claim2.Id && c.CorrelationScore == 0.82);
        Assert.Contains(saved, c => c.CorrelatedClaimId == claim3.Id && c.CorrelationScore == 0.65);
        Assert.All(saved, c => Assert.Equal(claim1.Id, c.SourceClaimId));
    }

    // =========================================================================
    // Test 2: GetByClaimIdAsync Returns Both Directions
    // =========================================================================

    [Fact]
    public async Task GetByClaimIdAsync_ReturnsBothDirections()
    {
        // Arrange — create three claims with correlations in both directions
        var claimA = new ClaimRecord
        {
            ClaimText = "Commercial auto collision on Interstate 95",
            Severity = "High",
            ClaimType = "Auto",
            FraudScore = 60
        };
        var claimB = new ClaimRecord
        {
            ClaimText = "Fleet vehicle damage from parking lot incident",
            Severity = "High",
            ClaimType = "Auto",
            FraudScore = 65
        };
        var claimC = new ClaimRecord
        {
            ClaimText = "Company van rear-ended at warehouse loading dock",
            Severity = "Medium",
            ClaimType = "Auto",
            FraudScore = 58
        };
        _db.Claims.AddRange(claimA, claimB, claimC);
        await _db.SaveChangesAsync();

        // Correlation 1: A is source, B is correlated
        _db.FraudCorrelations.Add(new FraudCorrelationRecord
        {
            SourceClaimId = claimA.Id,
            CorrelatedClaimId = claimB.Id,
            CorrelationType = "DateProximity+SameSeverity",
            CorrelationScore = 0.75,
            Details = "Same claim type (Auto), 10 days apart | Same severity (High)",
            DetectedAt = DateTime.UtcNow
        });
        // Correlation 2: C is source, A is correlated (A on the correlated side)
        _db.FraudCorrelations.Add(new FraudCorrelationRecord
        {
            SourceClaimId = claimC.Id,
            CorrelatedClaimId = claimA.Id,
            CorrelationType = "SharedFlags",
            CorrelationScore = 0.60,
            Details = "2 shared fraud flags: Timing suspicious, Prior claims",
            DetectedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        // Act — query for claimA should return both directions
        var (results, totalCount) = await _repo.GetByClaimIdAsync(claimA.Id);

        // Assert — should find 2 correlations: one where A is source, one where A is correlated
        Assert.Equal(2, totalCount);
        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.SourceClaimId == claimA.Id && r.CorrelatedClaimId == claimB.Id);
        Assert.Contains(results, r => r.SourceClaimId == claimC.Id && r.CorrelatedClaimId == claimA.Id);
        // Results should be ordered by score descending
        Assert.True(results[0].CorrelationScore >= results[1].CorrelationScore);
    }

    // =========================================================================
    // Test 3: GetAllAsync Filters by MinScore
    // =========================================================================

    [Fact]
    public async Task GetAllAsync_FiltersByMinScore()
    {
        // Arrange
        var claim1 = new ClaimRecord { ClaimText = "Workers comp injury at construction site", Severity = "High", ClaimType = "WorkersComp" };
        var claim2 = new ClaimRecord { ClaimText = "Slip and fall at retail store", Severity = "Medium", ClaimType = "Liability" };
        var claim3 = new ClaimRecord { ClaimText = "Equipment damage during transport", Severity = "Low", ClaimType = "Property" };
        var claim4 = new ClaimRecord { ClaimText = "Vehicle collision in company parking lot", Severity = "Medium", ClaimType = "Auto" };
        _db.Claims.AddRange(claim1, claim2, claim3, claim4);
        await _db.SaveChangesAsync();

        _db.FraudCorrelations.AddRange(
            new FraudCorrelationRecord
            {
                SourceClaimId = claim1.Id,
                CorrelatedClaimId = claim2.Id,
                CorrelationType = "DateProximity+SharedFlags",
                CorrelationScore = 0.92,
                Details = "High-confidence correlation with multiple indicators",
                DetectedAt = DateTime.UtcNow
            },
            new FraudCorrelationRecord
            {
                SourceClaimId = claim1.Id,
                CorrelatedClaimId = claim3.Id,
                CorrelationType = "SharedFlags",
                CorrelationScore = 0.45,
                Details = "Low-confidence correlation below threshold",
                DetectedAt = DateTime.UtcNow
            },
            new FraudCorrelationRecord
            {
                SourceClaimId = claim2.Id,
                CorrelatedClaimId = claim4.Id,
                CorrelationType = "SameSeverity+DateProximity",
                CorrelationScore = 0.71,
                Details = "Medium-confidence correlation above threshold",
                DetectedAt = DateTime.UtcNow
            }
        );
        await _db.SaveChangesAsync();

        // Act — filter with minScore = 0.5 should exclude the 0.45 record
        var (results, totalCount) = await _repo.GetAllAsync(minScore: 0.5, pageSize: 50);

        // Assert
        Assert.Equal(2, totalCount);
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.True(r.CorrelationScore >= 0.5));
        // Should be ordered descending by score
        Assert.Equal(0.92, results[0].CorrelationScore);
        Assert.Equal(0.71, results[1].CorrelationScore);
    }

    // =========================================================================
    // Test 4: DeleteByClaimIdAsync Removes Correlations
    // =========================================================================

    [Fact]
    public async Task DeleteByClaimIdAsync_RemovesCorrelations()
    {
        // Arrange
        var claimX = new ClaimRecord
        {
            ClaimText = "Liability claim from customer slip on wet floor",
            Severity = "Medium",
            ClaimType = "Liability",
            FraudScore = 40
        };
        var claimY = new ClaimRecord
        {
            ClaimText = "Product liability claim for defective merchandise",
            Severity = "High",
            ClaimType = "Liability",
            FraudScore = 55
        };
        var claimZ = new ClaimRecord
        {
            ClaimText = "Third-party property damage from delivery truck",
            Severity = "Low",
            ClaimType = "Auto",
            FraudScore = 20
        };
        _db.Claims.AddRange(claimX, claimY, claimZ);
        await _db.SaveChangesAsync();

        // X->Y correlation and Z->X correlation (X appears on both sides)
        _db.FraudCorrelations.AddRange(
            new FraudCorrelationRecord
            {
                SourceClaimId = claimX.Id,
                CorrelatedClaimId = claimY.Id,
                CorrelationType = "DateProximity+SharedFlags",
                CorrelationScore = 0.80,
                Details = "Correlated liability claims",
                DetectedAt = DateTime.UtcNow
            },
            new FraudCorrelationRecord
            {
                SourceClaimId = claimZ.Id,
                CorrelatedClaimId = claimX.Id,
                CorrelationType = "SameSeverity",
                CorrelationScore = 0.55,
                Details = "Reverse direction correlation",
                DetectedAt = DateTime.UtcNow
            },
            new FraudCorrelationRecord
            {
                SourceClaimId = claimY.Id,
                CorrelatedClaimId = claimZ.Id,
                CorrelationType = "SharedFlags",
                CorrelationScore = 0.60,
                Details = "Unrelated to claimX — should survive deletion",
                DetectedAt = DateTime.UtcNow
            }
        );
        await _db.SaveChangesAsync();

        var countBefore = await _db.FraudCorrelations.CountAsync();
        Assert.Equal(3, countBefore);

        // Act — delete all correlations involving claimX
        await _repo.DeleteByClaimIdAsync(claimX.Id);

        // Assert — only the Y->Z correlation should remain
        var remaining = await _db.FraudCorrelations.ToListAsync();
        Assert.Single(remaining);
        Assert.Equal(claimY.Id, remaining[0].SourceClaimId);
        Assert.Equal(claimZ.Id, remaining[0].CorrelatedClaimId);
        Assert.Equal(0.60, remaining[0].CorrelationScore);
    }
}
