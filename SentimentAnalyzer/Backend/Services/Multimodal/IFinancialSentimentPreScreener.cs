namespace SentimentAnalyzer.API.Services.Multimodal;

/// <summary>
/// Pre-screens text for financial/insurance sentiment using a lightweight ML model
/// before routing to the full multi-agent orchestration pipeline.
/// Insurance use case: rapid sentiment triage (~50ms) to avoid 10-30s orchestration
/// for high-confidence cases, saving 30-40% of LLM token usage.
/// </summary>
public interface IFinancialSentimentPreScreener
{
    /// <summary>
    /// Runs FinBERT sentiment pre-screening on the given text.
    /// </summary>
    /// <param name="text">The PII-redacted text to analyze.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Pre-screening result with sentiment scores and confidence assessment.</returns>
    Task<FinancialSentimentResult> PreScreenAsync(
        string text,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a FinBERT financial sentiment pre-screening.
/// </summary>
public class FinancialSentimentResult
{
    /// <summary>Whether the pre-screening call succeeded.</summary>
    public bool IsSuccess { get; set; }

    /// <summary>Top sentiment label: "positive", "negative", or "neutral".</summary>
    public string Sentiment { get; set; } = string.Empty;

    /// <summary>Confidence score of the top sentiment label (0.0 to 1.0).</summary>
    public double TopScore { get; set; }

    /// <summary>All sentiment scores from FinBERT (label -> score).</summary>
    public Dictionary<string, double> Scores { get; set; } = new();

    /// <summary>
    /// Whether the pre-screening confidence meets the threshold for short-circuiting.
    /// When true, the full multi-agent pipeline can be skipped.
    /// </summary>
    public bool IsHighConfidence { get; set; }

    /// <summary>Provider that performed the pre-screening.</summary>
    public string Provider { get; set; } = "HuggingFace/FinBERT";

    /// <summary>Error message if pre-screening failed.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Processing time in milliseconds.</summary>
    public long ElapsedMilliseconds { get; set; }
}
