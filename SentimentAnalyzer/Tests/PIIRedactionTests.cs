using SentimentAnalyzer.API.Services;
using Xunit;

namespace SentimentAnalyzer.Tests;

public class PIIRedactionTests
{
    private readonly PIIRedactionService _service = new();

    [Fact]
    public void Redact_SSN_IsRedacted()
    {
        var text = "My SSN is 123-45-6789 and I need coverage.";
        var result = _service.Redact(text);
        Assert.Contains("[SSN-REDACTED]", result);
        Assert.DoesNotContain("123-45-6789", result);
    }

    [Fact]
    public void Redact_PolicyNumber_IsRedacted()
    {
        var text = "I reported water damage on Jan 15. Policy HO-2024-789456.";
        var result = _service.Redact(text);
        Assert.Contains("[POLICY-REDACTED]", result);
        Assert.DoesNotContain("HO-2024-789456", result);
    }

    [Fact]
    public void Redact_ClaimNumber_IsRedacted()
    {
        var text = "Please check claim CLM-2024-12345678 for my auto incident.";
        var result = _service.Redact(text);
        Assert.Contains("[CLAIM-REDACTED]", result);
        Assert.DoesNotContain("CLM-2024-12345678", result);
    }

    [Fact]
    public void Redact_EmailAddress_IsRedacted()
    {
        var text = "Contact me at john.doe@insurance.com for updates.";
        var result = _service.Redact(text);
        Assert.Contains("[EMAIL-REDACTED]", result);
        Assert.DoesNotContain("john.doe@insurance.com", result);
    }

    [Fact]
    public void Redact_PhoneNumber_IsRedacted()
    {
        var text = "Call me at 555-123-4567 regarding my claim.";
        var result = _service.Redact(text);
        Assert.Contains("[PHONE-REDACTED]", result);
        Assert.DoesNotContain("555-123-4567", result);
    }

    [Fact]
    public void Redact_PhoneWithParentheses_IsRedacted()
    {
        var text = "My number is (555) 123-4567.";
        var result = _service.Redact(text);
        Assert.Contains("[PHONE-REDACTED]", result);
        Assert.DoesNotContain("(555) 123-4567", result);
    }

    [Fact]
    public void Redact_MultiplePIIInOneText_AllRedacted()
    {
        var text = "Name: Jane, SSN: 987-65-4321, Policy: AUTO-55667788, Email: jane@test.com, Phone: 555-999-1234, Claim: CLM-2025-0001";
        var result = _service.Redact(text);

        Assert.Contains("[SSN-REDACTED]", result);
        Assert.Contains("[POLICY-REDACTED]", result);
        Assert.Contains("[EMAIL-REDACTED]", result);
        Assert.Contains("[PHONE-REDACTED]", result);
        Assert.Contains("[CLAIM-REDACTED]", result);
        Assert.DoesNotContain("987-65-4321", result);
        Assert.DoesNotContain("AUTO-55667788", result);
        Assert.DoesNotContain("jane@test.com", result);
    }

    [Fact]
    public void Redact_TextWithNoPII_ReturnedUnchanged()
    {
        var text = "I am very happy with my insurance coverage and want to renew.";
        var result = _service.Redact(text);
        Assert.Equal(text, result);
    }

    [Fact]
    public void Redact_EmptyString_ReturnsEmpty()
    {
        Assert.Equal("", _service.Redact(""));
    }

    [Fact]
    public void Redact_NullString_ReturnsNull()
    {
        Assert.Null(_service.Redact(null!));
    }

    [Fact]
    public void Redact_PreservesNonPIIContent()
    {
        var text = "I submitted claim CLM-2024-5678 and my policy is HO-1234-5678. Please help.";
        var result = _service.Redact(text);
        Assert.Contains("I submitted claim", result);
        Assert.Contains("and my policy is", result);
        Assert.Contains("Please help.", result);
    }
}
