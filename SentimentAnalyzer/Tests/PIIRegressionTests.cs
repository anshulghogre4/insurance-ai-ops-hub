using System.Text.RegularExpressions;
using SentimentAnalyzer.API.Services;
using Xunit;

namespace SentimentAnalyzer.Tests;

/// <summary>
/// PII regression tests — verifies that PIIRedactionService catches all known PII
/// patterns before text reaches external AI providers or persistent storage.
/// Guards against regressions if regex patterns are modified.
/// </summary>
public class PIIRegressionTests
{
    private readonly PIIRedactionService _redactor = new();

    // Patterns that should NEVER appear in redacted output
    private static readonly Regex SsnPattern = new(@"\b\d{3}-\d{2}-\d{4}\b");
    private static readonly Regex ClaimPattern = new(@"\bCLM-\d{4}-\d{4,8}\b", RegexOptions.IgnoreCase);
    private static readonly Regex PolicyPattern = new(@"\b[A-Z]{2,4}-\d{4,10}\b");
    private static readonly Regex EmailPattern = new(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b");
    private static readonly Regex PhonePattern = new(@"(\+?1[-.\s]?)?\(?\d{3}\)?[-.\s]?\d{3}[-.\s]?\d{4}\b");

    [Fact]
    public void Redact_SSN_NeverSurvivesInOutput()
    {
        // Arrange — multiple SSN formats embedded in insurance text
        var input = "Policyholder SSN 123-45-6789. Secondary insured SSN: 987-65-4321. " +
                    "Filed under account with SSN 456-78-9012.";

        // Act
        var result = _redactor.Redact(input);

        // Assert — no SSN pattern survives
        Assert.DoesNotMatch(SsnPattern.ToString(), result);
        Assert.Contains("[SSN-REDACTED]", result);
        Assert.Equal(3, Regex.Matches(result, @"\[SSN-REDACTED\]").Count);
    }

    [Fact]
    public void Redact_PolicyNumbers_NeverSurviveInOutput()
    {
        // Arrange — various policy number formats
        var input = "Homeowners policy HO-2024-789456. Auto policy AUTO-12345678. " +
                    "Umbrella policy UMB-2024-001234. GL-9876543210.";

        // Act
        var result = _redactor.Redact(input);

        // Assert — no policy number pattern survives
        Assert.DoesNotMatch(PolicyPattern.ToString(), result);
        Assert.Contains("[POLICY-REDACTED]", result);
    }

    [Fact]
    public void Redact_ClaimNumbers_NeverSurviveInOutput()
    {
        // Arrange — claim number variations
        var input = "Primary claim CLM-2024-78901234 denied. Related claim CLM-2023-5678. " +
                    "Subrogation on clm-2024-99887766.";

        // Act
        var result = _redactor.Redact(input);

        // Assert — no claim number pattern survives (case-insensitive)
        Assert.DoesNotMatch(ClaimPattern.ToString(), result);
        Assert.Contains("[CLAIM-REDACTED]", result);
    }

    [Fact]
    public void Redact_EmailAddresses_NeverSurviveInOutput()
    {
        // Arrange — various email formats
        var input = "Contact policyholder at john.doe@insurance.com or adjuster " +
                    "jane_smith+claims@acme-insurance.co.uk for follow-up. " +
                    "CC: compliance@department.gov.";

        // Act
        var result = _redactor.Redact(input);

        // Assert — no email pattern survives
        Assert.DoesNotMatch(EmailPattern.ToString(), result);
        Assert.Contains("[EMAIL-REDACTED]", result);
    }

    [Fact]
    public void Redact_PhoneNumbers_NeverSurviveInOutput()
    {
        // Arrange — various phone formats
        var input = "Call policyholder at 555-123-4567. Alt: (555) 987-6543. " +
                    "International: +1-555-111-2222. Mobile: 5559998888.";

        // Act
        var result = _redactor.Redact(input);

        // Assert — no phone pattern survives
        Assert.DoesNotMatch(PhonePattern.ToString(), result);
        Assert.Contains("[PHONE-REDACTED]", result);
    }

    [Fact]
    public void Redact_MixedPII_AllPatternsRedactedSimultaneously()
    {
        // Arrange — realistic insurance text with ALL PII types
        var input = "Claimant Jane Doe (SSN 234-56-7890) filed claim CLM-2024-44556677 " +
                    "under policy HO-2024-123456. Contact: jane.doe@email.com, 555-444-3333. " +
                    "Adjuster: bob@claims.net, (800) 555-1234.";

        // Act
        var result = _redactor.Redact(input);

        // Assert — ALL patterns eliminated
        Assert.DoesNotMatch(SsnPattern.ToString(), result);
        Assert.DoesNotMatch(ClaimPattern.ToString(), result);
        Assert.DoesNotMatch(PolicyPattern.ToString(), result);
        Assert.DoesNotMatch(EmailPattern.ToString(), result);
        Assert.DoesNotMatch(PhonePattern.ToString(), result);

        // Verify non-PII text preserved
        Assert.Contains("Claimant Jane Doe", result);
        Assert.Contains("filed claim", result);
        Assert.Contains("under policy", result);
    }

    [Fact]
    public void Redact_CleanText_PassesThroughUnchanged()
    {
        // Arrange — no PII present
        var input = "The insured reported minor hail damage to the roof on January 15th. " +
                    "Coverage limits appear adequate for the estimated repair cost.";

        // Act
        var result = _redactor.Redact(input);

        // Assert — text unchanged
        Assert.Equal(input, result);
    }

    [Fact]
    public void Redact_EmptyOrNull_HandlesGracefully()
    {
        Assert.Equal("", _redactor.Redact(""));
        Assert.Null(_redactor.Redact(null!));
    }
}
