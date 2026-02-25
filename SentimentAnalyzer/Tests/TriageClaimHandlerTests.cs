using Microsoft.Extensions.Logging;
using Moq;
using SentimentAnalyzer.API.Features.Claims.Commands;
using SentimentAnalyzer.API.Models;
using SentimentAnalyzer.API.Services.Claims;
using SentimentAnalyzer.Domain.Enums;
using Xunit;

namespace SentimentAnalyzer.Tests;

/// <summary>
/// Tests for TriageClaimHandler — MediatR command handler for claim triage.
/// </summary>
public class TriageClaimHandlerTests
{
    private readonly Mock<IClaimsOrchestrationService> _mockService;
    private readonly Mock<ILogger<TriageClaimHandler>> _mockLogger;
    private readonly TriageClaimHandler _handler;

    public TriageClaimHandlerTests()
    {
        _mockService = new Mock<IClaimsOrchestrationService>();
        _mockLogger = new Mock<ILogger<TriageClaimHandler>>();
        _handler = new TriageClaimHandler(_mockService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsTriageResponse()
    {
        // Arrange
        var command = new TriageClaimCommand("Water damage reported in basement. Policy HO-2024-555666.");
        _mockService.Setup(s => s.TriageClaimAsync(command.Text, InteractionType.Complaint))
            .ReturnsAsync(new ClaimTriageResponse
            {
                ClaimId = 1,
                Severity = "High",
                Urgency = "Urgent",
                ClaimType = "Property",
                Status = "Triaged"
            });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(1, result.ClaimId);
        Assert.Equal("High", result.Severity);
        Assert.Equal("Triaged", result.Status);
    }

    [Fact]
    public async Task Handle_ParsesInteractionType()
    {
        // Arrange
        var command = new TriageClaimCommand("Accident claim", "Call");
        _mockService.Setup(s => s.TriageClaimAsync(command.Text, InteractionType.Call))
            .ReturnsAsync(new ClaimTriageResponse { ClaimId = 2 });

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockService.Verify(s => s.TriageClaimAsync(command.Text, InteractionType.Call), Times.Once);
    }

    [Fact]
    public async Task Handle_InvalidInteractionType_DefaultsToComplaint()
    {
        // Arrange
        var command = new TriageClaimCommand("Roof damage", "InvalidType");
        _mockService.Setup(s => s.TriageClaimAsync(command.Text, InteractionType.Complaint))
            .ReturnsAsync(new ClaimTriageResponse { ClaimId = 3 });

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockService.Verify(s => s.TriageClaimAsync(command.Text, InteractionType.Complaint), Times.Once);
    }

    [Fact]
    public async Task Handle_ServiceThrows_PropagatesException()
    {
        // Arrange
        var command = new TriageClaimCommand("Fire damage to garage");
        _mockService.Setup(s => s.TriageClaimAsync(It.IsAny<string>(), It.IsAny<InteractionType>()))
            .ThrowsAsync(new InvalidOperationException("Orchestrator failed"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, CancellationToken.None));
    }
}
