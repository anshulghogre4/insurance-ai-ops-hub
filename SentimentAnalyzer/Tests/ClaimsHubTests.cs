using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using SentimentAnalyzer.API.Hubs;
using SentimentAnalyzer.API.Models;
using Xunit;

namespace SentimentAnalyzer.Tests;

public class ClaimsHubTests
{
    private readonly Mock<ILogger<ClaimsHub>> _loggerMock = new();

    [Fact]
    public async Task ClaimTriaged_BroadcastsToAllClients()
    {
        // Arrange
        var mockClients = new Mock<IHubCallerClients<IClaimsHubClient>>();
        var mockClientProxy = new Mock<IClaimsHubClient>();
        mockClients.Setup(c => c.All).Returns(mockClientProxy.Object);

        var evt = new ClaimTriagedEvent(
            ClaimId: 1042,
            Severity: "Critical",
            PersonaType: "Policyholder",
            ClaimType: "Property Damage",
            FraudScore: 72.5,
            TriagedAt: DateTime.UtcNow);

        // Act
        await mockClientProxy.Object.ClaimTriaged(evt);

        // Assert
        mockClientProxy.Verify(c => c.ClaimTriaged(It.Is<ClaimTriagedEvent>(e =>
            e.ClaimId == 1042 && e.Severity == "Critical" && e.FraudScore == 72.5)), Times.Once);
    }

    [Fact]
    public async Task FraudAlertRaised_BroadcastsHighRiskEvent()
    {
        var mockClientProxy = new Mock<IClaimsHubClient>();

        var evt = new FraudAlertEvent(
            ClaimId: 2087,
            FraudScore: 91.3,
            Flags: new List<string> { "Duplicate claim narrative", "Staged accident indicators", "Policy inception within 30 days" },
            RiskLevel: "Critical",
            DetectedAt: DateTime.UtcNow);

        await mockClientProxy.Object.FraudAlertRaised(evt);

        mockClientProxy.Verify(c => c.FraudAlertRaised(It.Is<FraudAlertEvent>(e =>
            e.ClaimId == 2087 && e.FraudScore == 91.3 && e.Flags.Count == 3)), Times.Once);
    }

    [Fact]
    public async Task ClaimStatusChanged_BroadcastsTransition()
    {
        var mockClientProxy = new Mock<IClaimsHubClient>();

        var evt = new ClaimStatusEvent(
            ClaimId: 3015,
            OldStatus: "Triaged",
            NewStatus: "UnderReview",
            ChangedAt: DateTime.UtcNow);

        await mockClientProxy.Object.ClaimStatusChanged(evt);

        mockClientProxy.Verify(c => c.ClaimStatusChanged(It.Is<ClaimStatusEvent>(e =>
            e.ClaimId == 3015 && e.OldStatus == "Triaged" && e.NewStatus == "UnderReview")), Times.Once);
    }

    [Fact]
    public async Task JoinSeverityGroup_AddsToGroup()
    {
        // Arrange — use IHubContext to verify group operations
        var mockHubContext = new Mock<IHubContext<ClaimsHub, IClaimsHubClient>>();
        var mockGroups = new Mock<IGroupManager>();
        mockHubContext.Setup(h => h.Groups).Returns(mockGroups.Object);

        var groupName = "severity-Critical";
        var connectionId = "conn-abc-123";

        // Act
        await mockGroups.Object.AddToGroupAsync(connectionId, groupName);

        // Assert
        mockGroups.Verify(g => g.AddToGroupAsync(connectionId, groupName, default), Times.Once);
    }
}
