using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SentimentAnalyzer.API.Data;
using SentimentAnalyzer.API.Data.Entities;
using Xunit;

namespace SentimentAnalyzer.Tests;

/// <summary>
/// Tests for SqliteClaimsRepository using in-memory SQLite.
/// </summary>
public class ClaimsRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly InsuranceAnalysisDbContext _db;
    private readonly SqliteClaimsRepository _repo;

    public ClaimsRepositoryTests()
    {
        // Use SQLite in-memory with shared connection (kept open for test duration)
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<InsuranceAnalysisDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new InsuranceAnalysisDbContext(options);
        _db.Database.EnsureCreated();

        var mockLogger = new Mock<ILogger<SqliteClaimsRepository>>();
        _repo = new SqliteClaimsRepository(_db, mockLogger.Object);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task SaveClaimAsync_AssignsIdAndPersists()
    {
        // Arrange
        var claim = new ClaimRecord
        {
            ClaimText = "Water damage to basement after pipe burst. Policy HO-2024-789456.",
            Severity = "High",
            Urgency = "Urgent",
            ClaimType = "Property",
            Status = "Triaged"
        };

        // Act
        var saved = await _repo.SaveClaimAsync(claim);

        // Assert
        Assert.True(saved.Id > 0);
        Assert.Equal("High", saved.Severity);
    }

    [Fact]
    public async Task GetClaimByIdAsync_ExistingClaim_IncludesNavigationProperties()
    {
        // Arrange
        var claim = new ClaimRecord { ClaimText = "Auto collision on Main Street", Severity = "Medium" };
        _db.Claims.Add(claim);
        await _db.SaveChangesAsync();

        var evidence = new ClaimEvidenceRecord
        {
            ClaimId = claim.Id,
            EvidenceType = "image",
            MimeType = "image/jpeg",
            Provider = "AzureVision",
            ProcessedText = "Front bumper damage visible"
        };
        _db.ClaimEvidence.Add(evidence);

        var action = new ClaimActionRecord
        {
            ClaimId = claim.Id,
            Action = "Schedule body shop inspection",
            Priority = "High"
        };
        _db.ClaimActions.Add(action);
        await _db.SaveChangesAsync();

        // Act
        var result = await _repo.GetClaimByIdAsync(claim.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Evidence);
        Assert.Single(result.Actions);
        Assert.Equal("AzureVision", result.Evidence[0].Provider);
    }

    [Fact]
    public async Task GetClaimByIdAsync_NonExistent_ReturnsNull()
    {
        // Act
        var result = await _repo.GetClaimByIdAsync(999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetClaimsAsync_FilterBySeverity()
    {
        // Arrange
        _db.Claims.AddRange(
            new ClaimRecord { ClaimText = "Critical fire damage", Severity = "Critical" },
            new ClaimRecord { ClaimText = "Minor scratch on bumper", Severity = "Low" },
            new ClaimRecord { ClaimText = "Major flood damage", Severity = "Critical" }
        );
        await _db.SaveChangesAsync();

        // Act
        var (items, totalCount) = await _repo.GetClaimsAsync(severity: "Critical");

        // Assert
        Assert.Equal(2, items.Count);
        Assert.Equal(2, totalCount);
        Assert.All(items, c => Assert.Equal("Critical", c.Severity));
    }

    [Fact]
    public async Task GetClaimsAsync_FilterByDateRange()
    {
        // Arrange
        _db.Claims.AddRange(
            new ClaimRecord { ClaimText = "Old claim", Severity = "Low", CreatedAt = new DateTime(2024, 1, 1) },
            new ClaimRecord { ClaimText = "Recent claim", Severity = "Medium", CreatedAt = new DateTime(2024, 6, 15) },
            new ClaimRecord { ClaimText = "New claim", Severity = "High", CreatedAt = new DateTime(2024, 12, 1) }
        );
        await _db.SaveChangesAsync();

        // Act
        var (items, totalCount) = await _repo.GetClaimsAsync(
            fromDate: new DateTime(2024, 6, 1),
            toDate: new DateTime(2024, 12, 31));

        // Assert
        Assert.Equal(2, items.Count);
        Assert.Equal(2, totalCount);
    }

    [Fact]
    public async Task GetClaimsAsync_Pagination_ReturnsTotalCount()
    {
        // Arrange
        for (var i = 0; i < 10; i++)
        {
            _db.Claims.Add(new ClaimRecord { ClaimText = $"Claim {i}", Severity = "Medium" });
        }
        await _db.SaveChangesAsync();

        // Act
        var (page1Items, totalCount1) = await _repo.GetClaimsAsync(pageSize: 3, page: 1);
        var (page2Items, totalCount2) = await _repo.GetClaimsAsync(pageSize: 3, page: 2);

        // Assert
        Assert.Equal(3, page1Items.Count);
        Assert.Equal(3, page2Items.Count);
        Assert.Equal(10, totalCount1);
        Assert.Equal(10, totalCount2);
    }

    [Fact]
    public async Task GetFraudAlertsAsync_ReturnsClaimsAboveThreshold()
    {
        // Arrange
        _db.Claims.AddRange(
            new ClaimRecord { ClaimText = "Suspicious claim A", FraudScore = 80, FraudRiskLevel = "High" },
            new ClaimRecord { ClaimText = "Clean claim B", FraudScore = 20, FraudRiskLevel = "VeryLow" },
            new ClaimRecord { ClaimText = "Borderline claim C", FraudScore = 58, FraudRiskLevel = "Medium" }
        );
        await _db.SaveChangesAsync();

        // Act
        var result = await _repo.GetFraudAlertsAsync(55);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.True(result[0].FraudScore >= result[1].FraudScore); // Sorted descending
    }

    [Fact]
    public async Task UpdateClaimAsync_SetsUpdatedAt()
    {
        // Arrange
        var claim = new ClaimRecord { ClaimText = "Pending claim", Status = "Triaged" };
        _db.Claims.Add(claim);
        await _db.SaveChangesAsync();

        // Act
        claim.Status = "UnderReview";
        await _repo.UpdateClaimAsync(claim);

        // Assert
        var updated = await _db.Claims.FindAsync(claim.Id);
        Assert.NotNull(updated);
        Assert.Equal("UnderReview", updated.Status);
        Assert.NotNull(updated.UpdatedAt);
    }

    [Fact]
    public async Task SaveEvidenceAsync_PersistsAndReturnsWithId()
    {
        // Arrange
        var claim = new ClaimRecord { ClaimText = "Test claim for evidence" };
        _db.Claims.Add(claim);
        await _db.SaveChangesAsync();

        var evidence = new ClaimEvidenceRecord
        {
            ClaimId = claim.Id,
            EvidenceType = "document",
            MimeType = "application/pdf",
            Provider = "OcrSpace",
            ProcessedText = "Police report content extracted via OCR"
        };

        // Act
        var saved = await _repo.SaveEvidenceAsync(evidence);

        // Assert
        Assert.True(saved.Id > 0);
        Assert.Equal(claim.Id, saved.ClaimId);
    }

    [Fact]
    public async Task SaveActionsAsync_PersistsMultipleActions()
    {
        // Arrange
        var claim = new ClaimRecord { ClaimText = "Multi-action claim" };
        _db.Claims.Add(claim);
        await _db.SaveChangesAsync();

        var actions = new List<ClaimActionRecord>
        {
            new() { ClaimId = claim.Id, Action = "Assign adjuster", Priority = "High" },
            new() { ClaimId = claim.Id, Action = "Request photos", Priority = "Standard" }
        };

        // Act
        await _repo.SaveActionsAsync(actions);

        // Assert
        var savedActions = await _db.ClaimActions.Where(a => a.ClaimId == claim.Id).ToListAsync();
        Assert.Equal(2, savedActions.Count);
    }
}
