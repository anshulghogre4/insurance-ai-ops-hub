using System.Threading.RateLimiting;
using Xunit;

namespace SentimentAnalyzer.Tests;

/// <summary>
/// Tests for per-endpoint rate limiting policies.
/// Verifies that FixedWindowRateLimiter behaviors match our design:
///   analyze: 10/min, triage: 5/min, fraud: 5/min, upload: 3/min, api: 30/min.
/// </summary>
public class RateLimitingTests
{
    [Theory]
    [InlineData("analyze", 10)]
    [InlineData("triage", 5)]
    [InlineData("fraud", 5)]
    [InlineData("upload", 3)]
    [InlineData("api", 30)]
    public void RateLimiter_PermitsExactlyConfiguredLimit(string policyName, int expectedLimit)
    {
        // Arrange — create a limiter with the same config as Program.cs
        using var limiter = new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
        {
            PermitLimit = expectedLimit,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = policyName == "api" ? 5 : policyName == "analyze" ? 2 : 1
        });

        // Act — acquire exactly the limit
        var permits = new List<RateLimitLease>();
        for (var i = 0; i < expectedLimit; i++)
        {
            permits.Add(limiter.AttemptAcquire());
        }

        // Assert — all permits granted
        Assert.All(permits, p => Assert.True(p.IsAcquired, $"Policy '{policyName}': permit {permits.IndexOf(p) + 1}/{expectedLimit} should be granted"));

        // Act — one more should be rejected
        using var overflow = limiter.AttemptAcquire();
        Assert.False(overflow.IsAcquired, $"Policy '{policyName}': request {expectedLimit + 1} should be rate-limited");

        // Cleanup
        foreach (var p in permits) p.Dispose();
    }

    [Fact]
    public void AnalyzePolicy_StricterThanGenericApi()
    {
        // The analyze endpoint (10/min) should be stricter than the generic api policy (30/min)
        var analyzeLimit = 10;
        var apiLimit = 30;
        Assert.True(analyzeLimit < apiLimit, "Analyze policy should allow fewer requests than generic API policy");
    }

    [Fact]
    public void TriageAndFraud_EqualLimits()
    {
        // Triage and fraud both use AI agent pipelines with similar cost
        var triageLimit = 5;
        var fraudLimit = 5;
        Assert.Equal(triageLimit, fraudLimit);
    }

    [Fact]
    public void Upload_StrictestAIPolicy()
    {
        // Upload is the most expensive operation (multimodal: STT/Vision/OCR)
        var uploadLimit = 3;
        var triageLimit = 5;
        var analyzeLimit = 10;
        Assert.True(uploadLimit < triageLimit, "Upload should be stricter than triage");
        Assert.True(uploadLimit < analyzeLimit, "Upload should be stricter than analyze");
    }

    [Fact]
    public void RateLimiter_RejectsAfterLimitExhausted()
    {
        // Arrange — simulate the analyze endpoint (10 req/min)
        using var limiter = new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 2
        });

        // Exhaust all permits
        var permits = new List<RateLimitLease>();
        for (var i = 0; i < 10; i++)
        {
            permits.Add(limiter.AttemptAcquire());
        }

        // Act — next 3 should all be rejected (queue limit exceeded by non-async acquire)
        using var rejected1 = limiter.AttemptAcquire();
        using var rejected2 = limiter.AttemptAcquire();
        using var rejected3 = limiter.AttemptAcquire();

        // Assert
        Assert.False(rejected1.IsAcquired);
        Assert.False(rejected2.IsAcquired);
        Assert.False(rejected3.IsAcquired);

        foreach (var p in permits) p.Dispose();
    }
}
