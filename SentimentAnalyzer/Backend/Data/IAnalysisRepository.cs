using SentimentAnalyzer.API.Data.Entities;
using SentimentAnalyzer.API.Models;

namespace SentimentAnalyzer.API.Data;

public interface IAnalysisRepository
{
    Task SaveAsync(AnalysisRecord record);
    Task<List<AnalysisRecord>> GetRecentAsync(int count = 20);
    Task<DashboardMetrics> GetMetricsAsync();
    Task<SentimentDistribution> GetSentimentDistributionAsync();
    Task<List<PersonaCount>> GetTopPersonasAsync();
    Task<List<AnalysisRecord>> GetByCustomerIdAsync(string customerId);
    Task<AnalysisRecord?> GetByIdAsync(int id);
}
