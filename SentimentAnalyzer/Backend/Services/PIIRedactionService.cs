using System.Text.RegularExpressions;
using SentimentAnalyzer.Agents.Orchestration;

namespace SentimentAnalyzer.API.Services;

/// <summary>
/// Redacts personally identifiable information (PII) from text before sending to external AI providers.
/// Patterns: SSN, policy numbers, claim numbers, phone numbers, email addresses.
/// </summary>
public partial class PIIRedactionService : IPIIRedactor
{
    public string Redact(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        text = SsnRegex().Replace(text, "[SSN-REDACTED]");
        text = ClaimNumberRegex().Replace(text, "[CLAIM-REDACTED]");
        text = PolicyNumberRegex().Replace(text, "[POLICY-REDACTED]");
        text = EmailRegex().Replace(text, "[EMAIL-REDACTED]");
        text = PhoneRegex().Replace(text, "[PHONE-REDACTED]");

        return text;
    }

    // SSN: 123-45-6789
    [GeneratedRegex(@"\b\d{3}-\d{2}-\d{4}\b")]
    private static partial Regex SsnRegex();

    // Claim numbers: CLM-1234-56789
    [GeneratedRegex(@"\bCLM-\d{4}-\d{4,8}\b", RegexOptions.IgnoreCase)]
    private static partial Regex ClaimNumberRegex();

    // Policy numbers: HO-2024-789456, AUTO-12345678
    [GeneratedRegex(@"\b[A-Z]{2,4}-\d{4,10}\b")]
    private static partial Regex PolicyNumberRegex();

    // Email addresses
    [GeneratedRegex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b")]
    private static partial Regex EmailRegex();

    // Phone numbers: (555) 123-4567, 555-123-4567, 5551234567, +1-555-123-4567
    [GeneratedRegex(@"(\+?1[-.\s]?)?\(?\d{3}\)?[-.\s]?\d{3}[-.\s]?\d{4}\b")]
    private static partial Regex PhoneRegex();
}
