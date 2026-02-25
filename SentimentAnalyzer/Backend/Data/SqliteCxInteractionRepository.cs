using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SentimentAnalyzer.API.Data.Entities;

namespace SentimentAnalyzer.API.Data;

/// <summary>
/// EF Core implementation of <see cref="ICxInteractionRepository"/>.
/// Persists CX Copilot interaction audit records to SQLite or PostgreSQL.
/// </summary>
public class SqliteCxInteractionRepository : ICxInteractionRepository
{
    private readonly InsuranceAnalysisDbContext _db;
    private readonly ILogger<SqliteCxInteractionRepository> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="SqliteCxInteractionRepository"/>.
    /// </summary>
    /// <param name="db">The EF Core database context.</param>
    /// <param name="logger">Logger instance.</param>
    public SqliteCxInteractionRepository(
        InsuranceAnalysisDbContext db,
        ILogger<SqliteCxInteractionRepository> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task SaveInteractionAsync(CxInteractionRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        _db.CxInteractions.Add(record);
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Saved CX interaction audit record {Id} (tone={Tone}, escalation={Escalation}, streamed={Streamed}, elapsed={Elapsed}ms)",
            record.Id, record.Tone, record.EscalationRecommended, record.WasStreamed, record.ElapsedMilliseconds);
    }

    /// <inheritdoc />
    public async Task<(List<CxInteractionRecord> Items, int TotalCount)> GetInteractionsAsync(int pageSize = 50, int page = 1)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        page = Math.Max(1, page);

        var totalCount = await _db.CxInteractions.CountAsync();
        var items = await _db.CxInteractions
            .AsNoTracking()
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }
}
