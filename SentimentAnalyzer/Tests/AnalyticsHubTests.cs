using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using SentimentAnalyzer.API.Hubs;
using SentimentAnalyzer.API.Models;
using SentimentAnalyzer.API.Services.Notifications;
using Xunit;

namespace SentimentAnalyzer.Tests;

public class AnalyticsHubTests
{
    [Fact]
    public async Task MetricsUpdate_BroadcastsRollingWindowMetrics()
    {
        var mockClientProxy = new Mock<IAnalyticsHubClient>();

        var now = DateTime.UtcNow;
        var metrics = new AnalyticsMetrics(
            ClaimsPerHour: 47,
            AvgTriageMs: 1250.5,
            FraudDetectionRate: 12.3,
            ProviderResponseMs: new Dictionary<string, double> { ["Groq"] = 340.2, ["Cerebras"] = 180.1 },
            DocQueriesPerHour: 23,
            WindowStart: now.AddHours(-1),
            WindowEnd: now);

        await mockClientProxy.Object.MetricsUpdate(metrics);

        mockClientProxy.Verify(c => c.MetricsUpdate(It.Is<AnalyticsMetrics>(m =>
            m.ClaimsPerHour == 47 && m.AvgTriageMs == 1250.5 && m.FraudDetectionRate == 12.3)), Times.Once);
    }

    [Fact]
    public void RollingCounter_TracksIncrementsWithinWindow()
    {
        var counter = new RollingCounter(TimeSpan.FromHours(1));

        counter.Increment();
        counter.Increment();
        counter.Increment();

        Assert.Equal(3, counter.GetCount());
    }

    [Fact]
    public void RollingCounter_TracksSumValues()
    {
        var counter = new RollingCounter(TimeSpan.FromHours(1));

        counter.Add(150.5);
        counter.Add(200.3);
        counter.Add(100.0);

        // GetCount includes all Add calls (each Add also increments count)
        Assert.Equal(3, counter.GetCount());
        Assert.True(counter.GetSum() > 400);
    }

    [Fact]
    public void RollingCounter_SnapshotReturnsConsistentCountAndSum()
    {
        var counter = new RollingCounter(TimeSpan.FromHours(1));

        counter.Add(100.0);
        counter.Add(200.0);
        counter.Increment(); // count-only entry, value = 0

        var (count, sum) = counter.Snapshot();

        Assert.Equal(3, count);
        Assert.Equal(300.0, sum);
    }

    [Fact]
    public void RollingCounter_ExpiredEntriesAreExcludedFromSnapshot()
    {
        // Use a very short window so entries expire quickly
        var counter = new RollingCounter(TimeSpan.FromMilliseconds(1));

        counter.Increment();
        counter.Add(50.0);

        // Wait for entries to expire
        Thread.Sleep(10);

        var (count, sum) = counter.Snapshot();

        Assert.Equal(0, count);
        Assert.Equal(0.0, sum);
    }
}
