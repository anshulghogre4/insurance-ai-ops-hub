using Microsoft.EntityFrameworkCore;
using SentimentAnalyzer.API.Data.Entities;
using SentimentAnalyzer.API.Models;

namespace SentimentAnalyzer.API.Data;

public class SqliteAnalysisRepository : IAnalysisRepository
{
    private readonly InsuranceAnalysisDbContext _db;

    public SqliteAnalysisRepository(InsuranceAnalysisDbContext db)
    {
        _db = db;
    }

    public async Task SaveAsync(AnalysisRecord record)
    {
        _db.AnalysisRecords.Add(record);
        await _db.SaveChangesAsync();
    }

    public async Task<List<AnalysisRecord>> GetRecentAsync(int count = 20)
    {
        return await _db.AnalysisRecords
            .OrderByDescending(r => r.CreatedAt)
            .Take(count)
            .ToListAsync();
    }

    public async Task<DashboardMetrics> GetMetricsAsync()
    {
        // Single query using GroupBy(1) to compute all metrics in one DB round-trip
        var result = await _db.AnalysisRecords
            .GroupBy(_ => 1)
            .Select(g => new DashboardMetrics
            {
                TotalAnalyses = g.Count(),
                AvgPurchaseIntent = Math.Round(g.Average(r => (double)r.PurchaseIntentScore)),
                AvgSentimentScore = Math.Round(g.Average(r => r.ConfidenceScore), 2),
                HighRiskCount = g.Count(r => r.ChurnRisk == "High" || r.ComplaintEscalationRisk == "High")
            })
            .FirstOrDefaultAsync();

        return result ?? new DashboardMetrics();
    }

    public async Task<SentimentDistribution> GetSentimentDistributionAsync()
    {
        // Single query: group by sentiment and compute counts
        var groups = await _db.AnalysisRecords
            .GroupBy(r => r.Sentiment)
            .Select(g => new { Sentiment = g.Key, Count = g.Count() })
            .ToListAsync();

        var totalCount = groups.Sum(g => g.Count);
        if (totalCount == 0)
        {
            return new SentimentDistribution();
        }

        return new SentimentDistribution
        {
            Positive = Math.Round(100.0 * (groups.FirstOrDefault(g => g.Sentiment == "Positive")?.Count ?? 0) / totalCount),
            Negative = Math.Round(100.0 * (groups.FirstOrDefault(g => g.Sentiment == "Negative")?.Count ?? 0) / totalCount),
            Neutral = Math.Round(100.0 * (groups.FirstOrDefault(g => g.Sentiment == "Neutral")?.Count ?? 0) / totalCount),
            Mixed = Math.Round(100.0 * (groups.FirstOrDefault(g => g.Sentiment == "Mixed")?.Count ?? 0) / totalCount)
        };
    }

    public async Task<List<PersonaCount>> GetTopPersonasAsync()
    {
        // Single query: group, count, and compute percentage in one pass
        var groups = await _db.AnalysisRecords
            .GroupBy(r => r.CustomerPersona)
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .Take(6)
            .ToListAsync();

        var totalCount = groups.Sum(g => g.Count);
        if (totalCount == 0)
        {
            return [];
        }

        // Compute total from all records (not just top 6) for accurate percentages
        var allCount = await _db.AnalysisRecords.CountAsync();

        return groups.Select(g => new PersonaCount
        {
            Name = g.Name,
            Count = g.Count,
            Percentage = Math.Round(100.0 * g.Count / allCount)
        }).ToList();
    }

    public async Task<List<AnalysisRecord>> GetByCustomerIdAsync(string customerId)
    {
        return await _db.AnalysisRecords
            .Where(r => r.CustomerId == customerId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<AnalysisRecord?> GetByIdAsync(int id)
    {
        return await _db.AnalysisRecords.FirstOrDefaultAsync(r => r.Id == id);
    }
}
