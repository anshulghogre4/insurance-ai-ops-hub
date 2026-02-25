using Moq;
using SentimentAnalyzer.API.Features.Claims.Queries;
using SentimentAnalyzer.API.Models;
using SentimentAnalyzer.API.Services.Claims;
using Xunit;

namespace SentimentAnalyzer.Tests;

/// <summary>
/// Tests for GetClaimHandler and GetClaimsHistoryHandler — query handlers for claims.
/// </summary>
public class GetClaimHandlerTests
{
    [Fact]
    public async Task GetClaimHandler_ClaimFound_ReturnsResponse()
    {
        // Arrange
        var mockService = new Mock<IClaimsOrchestrationService>();
        mockService.Setup(s => s.GetClaimAsync(1))
            .ReturnsAsync(new ClaimTriageResponse
            {
                ClaimId = 1,
                Severity = "High",
                ClaimType = "Property",
                Status = "Triaged"
            });

        var handler = new GetClaimHandler(mockService.Object);

        // Act
        var result = await handler.Handle(new GetClaimQuery(1), CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.ClaimId);
        Assert.Equal("High", result.Severity);
    }

    [Fact]
    public async Task GetClaimHandler_ClaimNotFound_ReturnsNull()
    {
        // Arrange
        var mockService = new Mock<IClaimsOrchestrationService>();
        mockService.Setup(s => s.GetClaimAsync(999)).ReturnsAsync((ClaimTriageResponse?)null);

        var handler = new GetClaimHandler(mockService.Object);

        // Act
        var result = await handler.Handle(new GetClaimQuery(999), CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetClaimsHistoryHandler_ReturnsFilteredResults()
    {
        // Arrange
        var mockService = new Mock<IClaimsOrchestrationService>();
        mockService.Setup(s => s.GetClaimsHistoryAsync("High", null, null, null, 20, 1))
            .ReturnsAsync(new PaginatedResponse<ClaimTriageResponse>
            {
                Items =
                [
                    new ClaimTriageResponse { ClaimId = 1, Severity = "High" },
                    new ClaimTriageResponse { ClaimId = 2, Severity = "High" }
                ],
                TotalCount = 2,
                Page = 1,
                PageSize = 20
            });

        var handler = new GetClaimsHistoryHandler(mockService.Object);

        // Act
        var result = await handler.Handle(
            new GetClaimsHistoryQuery(Severity: "High"), CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, r => Assert.Equal("High", r.Severity));
    }

    [Fact]
    public async Task GetClaimsHistoryHandler_EmptyResult_ReturnsPaginatedEmpty()
    {
        // Arrange
        var mockService = new Mock<IClaimsOrchestrationService>();
        mockService.Setup(s => s.GetClaimsHistoryAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new PaginatedResponse<ClaimTriageResponse>
            {
                Items = [],
                TotalCount = 0,
                Page = 1,
                PageSize = 20
            });

        var handler = new GetClaimsHistoryHandler(mockService.Object);

        // Act
        var result = await handler.Handle(
            new GetClaimsHistoryQuery(Severity: "Critical"), CancellationToken.None);

        // Assert
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }
}
