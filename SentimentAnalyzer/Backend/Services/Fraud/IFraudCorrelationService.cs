using SentimentAnalyzer.API.Models;

namespace SentimentAnalyzer.API.Services.Fraud;

/// <summary>
/// Service interface for cross-claim fraud correlation analysis.
/// Identifies suspicious patterns across multiple claims: date proximity,
/// narrative similarity (via embeddings), shared fraud flags, and severity clustering.
/// </summary>
public interface IFraudCorrelationService
{
    /// <summary>
    /// Analyzes a claim against all existing claims for fraud patterns.
    /// Discovers correlations using four strategies (DateProximity, SimilarNarrative,
    /// SharedFlags, SameSeverity) and persists results. Requires 2+ indicator types
    /// to create a correlation to reduce false positives.
    /// </summary>
    /// <param name="claimId">ID of the claim to analyze for cross-claim correlations.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of discovered fraud correlations.</returns>
    Task<List<FraudCorrelationResponse>> AnalyzeCorrelationsAsync(int claimId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves previously stored correlations for a specific claim with pagination.
    /// Returns correlations where the claim is either the source or the correlated side.
    /// </summary>
    /// <param name="claimId">The claim ID to retrieve correlations for.</param>
    /// <param name="page">Page number (1-based). Default: 1.</param>
    /// <param name="pageSize">Number of records per page. Default: 20.</param>
    /// <returns>Paginated list of stored fraud correlations.</returns>
    Task<PaginatedResponse<FraudCorrelationResponse>> GetCorrelationsAsync(int claimId, int page = 1, int pageSize = 20);

    /// <summary>
    /// Retrieves all high-confidence correlations across the system for dashboard display, with pagination.
    /// </summary>
    /// <param name="minScore">Minimum correlation score filter (0.0-1.0). Default: 0.5.</param>
    /// <param name="page">Page number (1-based). Default: 1.</param>
    /// <param name="pageSize">Maximum number of correlations per page. Default: 50.</param>
    /// <returns>Paginated list of high-confidence fraud correlations ordered by score descending.</returns>
    Task<PaginatedResponse<FraudCorrelationResponse>> GetAllCorrelationsAsync(double minScore = 0.5, int page = 1, int pageSize = 50);
}
