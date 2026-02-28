namespace SentimentAnalyzer.API.Models;

/// <summary>
/// Result of a batch CSV claim upload operation.
/// Contains summary counts, individual triage results, and any validation errors.
/// </summary>
public class BatchClaimUploadResult
{
    /// <summary>Unique identifier for this batch processing run.</summary>
    public string BatchId { get; set; } = string.Empty;

    /// <summary>Total number of rows found in the CSV (excluding header).</summary>
    public int TotalCount { get; set; }

    /// <summary>Number of rows that were processed (valid + invalid attempts).</summary>
    public int ProcessedCount { get; set; }

    /// <summary>Number of rows that were successfully triaged.</summary>
    public int SuccessCount { get; set; }

    /// <summary>Number of rows that had validation or processing errors.</summary>
    public int ErrorCount { get; set; }

    /// <summary>Batch status: Processing, Completed, or Failed.</summary>
    public string Status { get; set; } = "Processing";

    /// <summary>Individual triage results for each successfully processed row.</summary>
    public List<BatchClaimItemResult> Results { get; set; } = [];

    /// <summary>Validation and processing errors keyed by row number.</summary>
    public List<BatchClaimError> Errors { get; set; } = [];
}

/// <summary>
/// Triage result for a single claim row within a batch upload.
/// </summary>
public class BatchClaimItemResult
{
    /// <summary>1-based row number from the CSV file.</summary>
    public int RowNumber { get; set; }

    /// <summary>Claim identifier from the CSV data.</summary>
    public string ClaimId { get; set; } = string.Empty;

    /// <summary>AI-assessed severity level (Critical, High, Medium, Low).</summary>
    public string Severity { get; set; } = string.Empty;

    /// <summary>Fraud risk score on a 0-100 scale.</summary>
    public int FraudScore { get; set; }

    /// <summary>Processing status for this individual claim.</summary>
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Describes a validation or processing error for a specific CSV row.
/// </summary>
public class BatchClaimError
{
    /// <summary>1-based row number where the error occurred.</summary>
    public int RowNumber { get; set; }

    /// <summary>Name of the field that caused the error.</summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>Human-readable description of what went wrong.</summary>
    public string ErrorMessage { get; set; } = string.Empty;
}
