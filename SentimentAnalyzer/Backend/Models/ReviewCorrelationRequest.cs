namespace SentimentAnalyzer.API.Models;

/// <summary>
/// Request body for reviewing (confirming/dismissing) a fraud correlation.
/// Used by the PATCH /api/insurance/fraud/correlations/{id}/review endpoint.
/// </summary>
public class ReviewCorrelationRequest
{
    /// <summary>
    /// New status for the correlation: Pending, Confirmed, or Dismissed.
    /// </summary>
    public string Status { get; set; } = "Pending";

    /// <summary>
    /// Name or identifier of the reviewer performing the action.
    /// </summary>
    public string? ReviewedBy { get; set; }

    /// <summary>
    /// Reason for dismissal. Required when <see cref="Status"/> is "Dismissed".
    /// </summary>
    public string? DismissalReason { get; set; }
}
