using SentimentAnalyzer.API.Data.Entities;

namespace SentimentAnalyzer.API.Data;

/// <summary>
/// Repository interface for CX interaction audit trail data access.
/// All AI-generated customer communications must be persisted for regulatory compliance.
/// </summary>
public interface ICxInteractionRepository
{
    /// <summary>
    /// Saves a CX interaction audit record to the database.
    /// Called after every CX Copilot response — both streamed and non-streamed.
    /// </summary>
    /// <param name="record">The interaction record to persist.</param>
    Task SaveInteractionAsync(CxInteractionRecord record);

    /// <summary>
    /// Gets paginated interaction history for the audit dashboard.
    /// Returns items ordered by most recent first.
    /// </summary>
    /// <param name="pageSize">Number of records per page (1-200, default 50).</param>
    /// <param name="page">Page number (1-based, default 1).</param>
    /// <returns>Tuple of paginated items and total count for pagination UI.</returns>
    Task<(List<CxInteractionRecord> Items, int TotalCount)> GetInteractionsAsync(int pageSize = 50, int page = 1);
}
