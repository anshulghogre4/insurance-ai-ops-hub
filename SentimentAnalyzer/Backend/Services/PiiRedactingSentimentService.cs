using SentimentAnalyzer.Agents.Orchestration;
using SentimentAnalyzer.API.Models;

namespace SentimentAnalyzer.API.Services;

/// <summary>
/// Decorator that wraps ISentimentService to add PII redaction before external AI calls.
/// Used to protect the frozen v1 SentimentController pipeline without modifying frozen files.
/// </summary>
public class PiiRedactingSentimentService : ISentimentService
{
    private readonly ISentimentService _inner;
    private readonly IPIIRedactor _redactor;
    private readonly ILogger<PiiRedactingSentimentService> _logger;

    public PiiRedactingSentimentService(
        ISentimentService inner,
        IPIIRedactor redactor,
        ILogger<PiiRedactingSentimentService> logger)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SentimentResponse> AnalyzeSentimentAsync(string text)
    {
        var redactedText = _redactor.Redact(text);

        if (redactedText != text)
        {
            _logger.LogInformation("PII redacted from v1 sentiment analysis input before external AI call");
        }

        return await _inner.AnalyzeSentimentAsync(redactedText);
    }
}
