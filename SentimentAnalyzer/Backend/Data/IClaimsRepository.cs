using SentimentAnalyzer.API.Data.Entities;

namespace SentimentAnalyzer.API.Data;

/// <summary>
/// Repository interface for claims triage data access.
/// Abstracts storage for claims, evidence, and recommended actions.
/// </summary>
public interface IClaimsRepository
{
    /// <summary>Saves a new claim record and returns it with generated ID.</summary>
    Task<ClaimRecord> SaveClaimAsync(ClaimRecord claim);

    /// <summary>Updates an existing claim record.</summary>
    Task UpdateClaimAsync(ClaimRecord claim);

    /// <summary>Retrieves a claim by ID, including evidence and actions.</summary>
    Task<ClaimRecord?> GetClaimByIdAsync(int claimId);

    /// <summary>Retrieves claims with optional filters, ordered by CreatedAt descending.</summary>
    Task<(List<ClaimRecord> Items, int TotalCount)> GetClaimsAsync(
        string? severity = null,
        string? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int pageSize = 20,
        int page = 1);

    /// <summary>Retrieves claims with fraud score above the specified threshold.</summary>
    Task<List<ClaimRecord>> GetFraudAlertsAsync(double minFraudScore = 55, int pageSize = 50);

    /// <summary>Saves a multimodal evidence record linked to a claim.</summary>
    Task<ClaimEvidenceRecord> SaveEvidenceAsync(ClaimEvidenceRecord evidence);

    /// <summary>Saves one or more recommended action records linked to a claim.</summary>
    Task SaveActionsAsync(IEnumerable<ClaimActionRecord> actions);
}
