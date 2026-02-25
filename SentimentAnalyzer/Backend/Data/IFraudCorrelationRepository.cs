using SentimentAnalyzer.API.Data.Entities;

namespace SentimentAnalyzer.API.Data;

/// <summary>
/// Repository interface for fraud correlation data access.
/// Abstracts storage for cross-claim fraud correlation records.
/// </summary>
public interface IFraudCorrelationRepository
{
    /// <summary>
    /// Persists a batch of newly discovered fraud correlations.
    /// Existing correlations for the same source claim are deleted first to avoid duplicates.
    /// Must be executed within a transaction to prevent data loss on partial failure.
    /// </summary>
    /// <param name="correlations">The correlation records to save.</param>
    Task SaveCorrelationsAsync(IEnumerable<FraudCorrelationRecord> correlations);

    /// <summary>
    /// Retrieves a single fraud correlation record by its ID.
    /// Includes navigation properties for both claim sides.
    /// </summary>
    /// <param name="id">The correlation record ID.</param>
    /// <returns>The correlation record, or null if not found.</returns>
    Task<FraudCorrelationRecord?> GetByIdAsync(int id);

    /// <summary>
    /// Retrieves correlations where the given claim is either the source or the correlated claim.
    /// Supports pagination. Includes navigation properties for both claim sides.
    /// </summary>
    /// <param name="claimId">The claim ID to look up correlations for.</param>
    /// <param name="page">Page number (1-based). Default: 1.</param>
    /// <param name="pageSize">Number of records per page. Default: 20.</param>
    /// <returns>Tuple of correlation records ordered by score descending and total count.</returns>
    Task<(List<FraudCorrelationRecord> Items, int TotalCount)> GetByClaimIdAsync(int claimId, int page = 1, int pageSize = 20);

    /// <summary>
    /// Retrieves all correlations above a minimum score, with pagination.
    /// Used for the system-wide fraud correlation dashboard.
    /// </summary>
    /// <param name="minScore">Minimum correlation score filter (0.0-1.0).</param>
    /// <param name="page">Page number (1-based). Default: 1.</param>
    /// <param name="pageSize">Maximum number of records per page. Default: 50.</param>
    /// <returns>Tuple of correlation records ordered by score descending and total count.</returns>
    Task<(List<FraudCorrelationRecord> Items, int TotalCount)> GetAllAsync(double minScore = 0.5, int page = 1, int pageSize = 50);

    /// <summary>
    /// Updates the review status of a fraud correlation record.
    /// Used by fraud analysts to confirm or dismiss correlations.
    /// </summary>
    /// <param name="id">The correlation record ID to update.</param>
    /// <param name="status">New status: Pending, Confirmed, or Dismissed.</param>
    /// <param name="reviewedBy">Identity of the reviewer.</param>
    /// <param name="reason">Dismissal reason (required when status is Dismissed, null otherwise).</param>
    Task UpdateCorrelationStatusAsync(int id, string status, string? reviewedBy, string? reason);

    /// <summary>
    /// Deletes all correlations associated with a specific claim (both source and correlated).
    /// Used before re-analysis to prevent stale data, or when a claim is deleted.
    /// </summary>
    /// <param name="claimId">The claim ID whose correlations should be removed.</param>
    Task DeleteByClaimIdAsync(int claimId);
}
