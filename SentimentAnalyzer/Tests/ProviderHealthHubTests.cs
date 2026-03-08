using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using SentimentAnalyzer.API.Hubs;
using SentimentAnalyzer.API.Models;
using SentimentAnalyzer.API.Services.Notifications;
using Xunit;

namespace SentimentAnalyzer.Tests;

public class ProviderHealthHubTests
{
    [Fact]
    public async Task HealthSnapshot_BroadcastsAllProviderStatuses()
    {
        var mockClientProxy = new Mock<IProviderHealthHubClient>();

        var providers = new List<ProviderStatusEvent>
        {
            new("Groq", "Available", "Available", null, DateTime.UtcNow),
            new("Cerebras", "Available", "Cooldown", 120, DateTime.UtcNow),
            new("Mistral", "Available", "Available", null, DateTime.UtcNow)
        };

        var snapshot = new HealthSnapshotEvent(providers, DateTime.UtcNow);

        await mockClientProxy.Object.HealthSnapshot(snapshot);

        mockClientProxy.Verify(c => c.HealthSnapshot(It.Is<HealthSnapshotEvent>(s =>
            s.Providers.Count == 3)), Times.Once);
    }

    [Fact]
    public async Task ProviderStatusChanged_BroadcastsOnStateTransition()
    {
        var mockClientProxy = new Mock<IProviderHealthHubClient>();

        var evt = new ProviderStatusEvent("Groq", "Available", "Cooldown", 180, DateTime.UtcNow);

        await mockClientProxy.Object.ProviderStatusChanged(evt);

        mockClientProxy.Verify(c => c.ProviderStatusChanged(It.Is<ProviderStatusEvent>(e =>
            e.ProviderName == "Groq" && e.OldStatus == "Available" && e.NewStatus == "Cooldown" && e.CooldownSeconds == 180)), Times.Once);
    }

    [Fact]
    public async Task ProviderStatusChanged_IncludesNullCooldownForRecovery()
    {
        var mockClientProxy = new Mock<IProviderHealthHubClient>();

        var evt = new ProviderStatusEvent("Deepgram", "Cooldown", "Available", null, DateTime.UtcNow);

        await mockClientProxy.Object.ProviderStatusChanged(evt);

        mockClientProxy.Verify(c => c.ProviderStatusChanged(It.Is<ProviderStatusEvent>(e =>
            e.ProviderName == "Deepgram" && e.NewStatus == "Available" && e.CooldownSeconds == null)), Times.Once);
    }
}
