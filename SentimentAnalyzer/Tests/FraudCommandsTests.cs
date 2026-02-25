using Microsoft.Extensions.Logging;
using Moq;
using SentimentAnalyzer.API.Features.Fraud.Commands;
using SentimentAnalyzer.API.Features.Fraud.Queries;
using SentimentAnalyzer.API.Models;
using SentimentAnalyzer.API.Services.Fraud;
using Xunit;

namespace SentimentAnalyzer.Tests;

/// <summary>
/// Tests for fraud MediatR command and query handlers.
/// </summary>
public class FraudCommandsTests
{
    [Fact]
    public async Task AnalyzeFraudHandler_ValidClaim_ReturnsFraudResponse()
    {
        // Arrange
        var mockService = new Mock<IFraudAnalysisService>();
        var mockLogger = new Mock<ILogger<AnalyzeFraudHandler>>();
        mockService.Setup(s => s.AnalyzeFraudAsync(1))
            .ReturnsAsync(new FraudAnalysisResponse
            {
                ClaimId = 1,
                FraudScore = 72,
                RiskLevel = "High"
            });

        var handler = new AnalyzeFraudHandler(mockService.Object, mockLogger.Object);
        var command = new AnalyzeFraudCommand(1);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(72, result.FraudScore);
        Assert.Equal("High", result.RiskLevel);
    }

    [Fact]
    public async Task GetFraudScoreHandler_ReturnsScore()
    {
        // Arrange
        var mockService = new Mock<IFraudAnalysisService>();
        mockService.Setup(s => s.GetFraudScoreAsync(5))
            .ReturnsAsync(new FraudAnalysisResponse { ClaimId = 5, FraudScore = 35, RiskLevel = "Low" });

        var handler = new GetFraudScoreHandler(mockService.Object);

        // Act
        var result = await handler.Handle(new GetFraudScoreQuery(5), CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(35, result.FraudScore);
    }

    [Fact]
    public async Task GetFraudAlertsHandler_ReturnsFilteredAlerts()
    {
        // Arrange
        var mockService = new Mock<IFraudAnalysisService>();
        mockService.Setup(s => s.GetFraudAlertsAsync(60, 25))
            .ReturnsAsync(
            [
                new FraudAnalysisResponse { ClaimId = 1, FraudScore = 78, RiskLevel = "High" },
                new FraudAnalysisResponse { ClaimId = 2, FraudScore = 65, RiskLevel = "Medium" }
            ]);

        var handler = new GetFraudAlertsHandler(mockService.Object);

        // Act
        var results = await handler.Handle(new GetFraudAlertsQuery(60, 25), CancellationToken.None);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.True(r.FraudScore >= 60));
    }

    [Fact]
    public async Task GetFraudScoreHandler_NonExistentClaim_ReturnsNull()
    {
        // Arrange
        var mockService = new Mock<IFraudAnalysisService>();
        mockService.Setup(s => s.GetFraudScoreAsync(999)).ReturnsAsync((FraudAnalysisResponse?)null);

        var handler = new GetFraudScoreHandler(mockService.Object);

        // Act
        var result = await handler.Handle(new GetFraudScoreQuery(999), CancellationToken.None);

        // Assert
        Assert.Null(result);
    }
}
