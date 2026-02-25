using SentimentAnalyzer.API.Models;
using SentimentAnalyzer.Domain.Enums;

namespace SentimentAnalyzer.API.Services.Claims;

/// <summary>
/// Facade for the claims triage pipeline.
/// Accepts claim text, orchestrates AI analysis, and persists results.
/// </summary>
public interface IClaimsOrchestrationService
{
    /// <summary>
    /// Triages a claim: PII-redacts text, runs ClaimsTriage profile agents,
    /// maps results to response, and persists to database.
    /// </summary>
    Task<ClaimTriageResponse> TriageClaimAsync(string claimText, InteractionType interactionType = InteractionType.Complaint);

    /// <summary>
    /// Retrieves a previously triaged claim by ID.
    /// </summary>
    Task<ClaimTriageResponse?> GetClaimAsync(int claimId);

    /// <summary>
    /// Retrieves claims history with optional filters and pagination metadata.
    /// </summary>
    Task<PaginatedResponse<ClaimTriageResponse>> GetClaimsHistoryAsync(
        string? severity = null,
        string? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int pageSize = 20,
        int page = 1);
}
