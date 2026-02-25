using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SentimentAnalyzer.API.Data.Entities;

namespace SentimentAnalyzer.API.Data;

/// <summary>
/// EF Core implementation of <see cref="IClaimsRepository"/>.
/// Handles claims, evidence, and action persistence via SQLite or PostgreSQL.
/// </summary>
public class SqliteClaimsRepository : IClaimsRepository
{
    private readonly InsuranceAnalysisDbContext _db;
    private readonly ILogger<SqliteClaimsRepository> _logger;

    public SqliteClaimsRepository(InsuranceAnalysisDbContext db, ILogger<SqliteClaimsRepository> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<ClaimRecord> SaveClaimAsync(ClaimRecord claim)
    {
        _db.Claims.Add(claim);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Saved claim record with ID {ClaimId}", claim.Id);
        return claim;
    }

    /// <inheritdoc />
    public async Task UpdateClaimAsync(ClaimRecord claim)
    {
        claim.UpdatedAt = DateTime.UtcNow;
        _db.Claims.Update(claim);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Updated claim record {ClaimId}", claim.Id);
    }

    /// <inheritdoc />
    public async Task<ClaimRecord?> GetClaimByIdAsync(int claimId)
    {
        return await _db.Claims
            .Include(c => c.Evidence)
            .Include(c => c.Actions)
            .FirstOrDefaultAsync(c => c.Id == claimId);
    }

    /// <inheritdoc />
    public async Task<(List<ClaimRecord> Items, int TotalCount)> GetClaimsAsync(
        string? severity = null,
        string? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int pageSize = 20,
        int page = 1)
    {
        // Guard against invalid pagination values
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);

        // List query: no .Include() to avoid over-fetching. Use GetClaimByIdAsync for full detail.
        var query = _db.Claims
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(severity))
            query = query.Where(c => c.Severity == severity);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(c => c.Status == status);

        if (fromDate.HasValue)
            query = query.Where(c => c.CreatedAt >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(c => c.CreatedAt <= toDate.Value);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    /// <inheritdoc />
    public async Task<List<ClaimRecord>> GetFraudAlertsAsync(double minFraudScore = 55, int pageSize = 50)
    {
        minFraudScore = Math.Clamp(minFraudScore, 0, 100);
        pageSize = Math.Clamp(pageSize, 1, 100);

        return await _db.Claims
            .AsNoTracking()
            .Where(c => c.FraudScore >= minFraudScore)
            .OrderByDescending(c => c.FraudScore)
            .Take(pageSize)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<ClaimEvidenceRecord> SaveEvidenceAsync(ClaimEvidenceRecord evidence)
    {
        _db.ClaimEvidence.Add(evidence);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Saved evidence record {EvidenceId} for claim {ClaimId}", evidence.Id, evidence.ClaimId);
        return evidence;
    }

    /// <inheritdoc />
    public async Task SaveActionsAsync(IEnumerable<ClaimActionRecord> actions)
    {
        _db.ClaimActions.AddRange(actions);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Saved action records for claims");
    }
}
