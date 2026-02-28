using SentimentAnalyzer.API.Models;

namespace SentimentAnalyzer.API.Services.Fraud;

/// <summary>
/// Facade for the fraud analysis pipeline.
/// Runs FraudScoring profile agents, updates claim records, and manages alerts.
/// </summary>
public interface IFraudAnalysisService
{
    /// <summary>
    /// Performs detailed fraud analysis on an existing claim.
    /// Updates the claim's fraud score, risk level, and returns a full fraud report.
    /// </summary>
    Task<FraudAnalysisResponse> AnalyzeFraudAsync(int claimId);

    /// <summary>
    /// Retrieves fraud score and risk level for a claim.
    /// </summary>
    Task<FraudAnalysisResponse?> GetFraudScoreAsync(int claimId);

    /// <summary>
    /// Retrieves claims flagged as fraud alerts (FraudScore above threshold).
    /// Returns full claim triage data so the frontend can render severity, type, dates, and fraud flags.
    /// </summary>
    Task<List<ClaimTriageResponse>> GetFraudAlertsAsync(double minFraudScore = 55, int pageSize = 50);
}
