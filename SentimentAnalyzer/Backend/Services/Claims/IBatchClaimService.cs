using SentimentAnalyzer.API.Models;

namespace SentimentAnalyzer.API.Services.Claims;

/// <summary>
/// Service for processing batch CSV uploads of insurance claims.
/// Parses CSV rows, validates fields, redacts PII, and simulates triage results.
/// </summary>
public interface IBatchClaimService
{
    /// <summary>
    /// Processes a CSV file stream containing multiple insurance claims.
    /// Each row is validated, PII-redacted, and triaged.
    /// </summary>
    /// <param name="csvStream">Stream containing the CSV file data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Batch result with individual claim results and any validation errors.</returns>
    Task<BatchClaimUploadResult> ProcessBatchAsync(Stream csvStream, CancellationToken ct = default);
}
