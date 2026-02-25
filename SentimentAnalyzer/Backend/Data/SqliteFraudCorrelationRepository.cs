using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SentimentAnalyzer.API.Data.Entities;

namespace SentimentAnalyzer.API.Data;

/// <summary>
/// EF Core implementation of <see cref="IFraudCorrelationRepository"/>.
/// Handles fraud correlation persistence via SQLite or PostgreSQL.
/// </summary>
public class SqliteFraudCorrelationRepository : IFraudCorrelationRepository
{
    private readonly InsuranceAnalysisDbContext _db;
    private readonly ILogger<SqliteFraudCorrelationRepository> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="SqliteFraudCorrelationRepository"/>.
    /// </summary>
    /// <param name="db">The EF Core database context.</param>
    /// <param name="logger">Logger instance.</param>
    public SqliteFraudCorrelationRepository(
        InsuranceAnalysisDbContext db,
        ILogger<SqliteFraudCorrelationRepository> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task SaveCorrelationsAsync(IEnumerable<FraudCorrelationRecord> correlations)
    {
        ArgumentNullException.ThrowIfNull(correlations);

        var records = correlations.ToList();
        if (records.Count == 0) return;

        var sourceClaimId = records[0].SourceClaimId;

        // Wrap delete-then-insert in a transaction to prevent data loss on partial failure (QA-H2)
        await using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            // Delete existing correlations for the source claim to avoid duplicates on re-analysis
            var existing = await _db.FraudCorrelations
                .Where(fc => fc.SourceClaimId == sourceClaimId)
                .ToListAsync();

            if (existing.Count > 0)
            {
                _db.FraudCorrelations.RemoveRange(existing);
                _logger.LogInformation(
                    "Removed {Count} existing correlations for source claim {ClaimId} before re-analysis",
                    existing.Count, sourceClaimId);
            }

            _db.FraudCorrelations.AddRange(records);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation(
                "Saved {Count} fraud correlations for source claim {ClaimId}",
                records.Count, sourceClaimId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex,
                "Failed to save fraud correlations for source claim {ClaimId}; transaction rolled back",
                sourceClaimId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<FraudCorrelationRecord?> GetByIdAsync(int id)
    {
        return await _db.FraudCorrelations
            .Include(fc => fc.SourceClaim)
            .Include(fc => fc.CorrelatedClaim)
            .FirstOrDefaultAsync(fc => fc.Id == id);
    }

    /// <inheritdoc />
    public async Task<(List<FraudCorrelationRecord> Items, int TotalCount)> GetByClaimIdAsync(
        int claimId, int page = 1, int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.FraudCorrelations
            .AsNoTracking()
            .Include(fc => fc.SourceClaim)
            .Include(fc => fc.CorrelatedClaim)
            .Where(fc => fc.SourceClaimId == claimId || fc.CorrelatedClaimId == claimId)
            .OrderByDescending(fc => fc.CorrelationScore);

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    /// <inheritdoc />
    public async Task<(List<FraudCorrelationRecord> Items, int TotalCount)> GetAllAsync(
        double minScore = 0.5, int page = 1, int pageSize = 50)
    {
        minScore = Math.Clamp(minScore, 0.0, 1.0);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.FraudCorrelations
            .AsNoTracking()
            .Include(fc => fc.SourceClaim)
            .Include(fc => fc.CorrelatedClaim)
            .Where(fc => fc.CorrelationScore >= minScore)
            .OrderByDescending(fc => fc.CorrelationScore);

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    /// <inheritdoc />
    public async Task UpdateCorrelationStatusAsync(int id, string status, string? reviewedBy, string? reason)
    {
        var record = await _db.FraudCorrelations.FindAsync(id);
        if (record is null)
        {
            throw new KeyNotFoundException($"Fraud correlation {id} not found");
        }

        record.Status = status;
        record.ReviewedBy = reviewedBy;
        record.ReviewedAt = DateTime.UtcNow;
        record.DismissalReason = reason;

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Updated fraud correlation {Id} status to {Status} by {ReviewedBy}",
            id, status, reviewedBy ?? "unknown");
    }

    /// <inheritdoc />
    public async Task DeleteByClaimIdAsync(int claimId)
    {
        var toDelete = await _db.FraudCorrelations
            .Where(fc => fc.SourceClaimId == claimId || fc.CorrelatedClaimId == claimId)
            .ToListAsync();

        if (toDelete.Count > 0)
        {
            _db.FraudCorrelations.RemoveRange(toDelete);
            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "Deleted {Count} fraud correlations for claim {ClaimId}",
                toDelete.Count, claimId);
        }
    }
}
