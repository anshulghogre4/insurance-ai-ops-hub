using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using SentimentAnalyzer.API.Controllers;
using SentimentAnalyzer.API.Models;
using SentimentAnalyzer.API.Services;
using Xunit;

namespace SentimentAnalyzer.Tests;

public class SentimentControllerTests
{
    private readonly Mock<ISentimentService> _mockService;
    private readonly Mock<ILogger<SentimentController>> _mockLogger;
    private readonly SentimentController _controller;

    public SentimentControllerTests()
    {
        _mockService = new Mock<ISentimentService>();
        _mockLogger = new Mock<ILogger<SentimentController>>();
        _controller = new SentimentController(_mockService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task AnalyzeSentiment_WithValidText_ReturnsOkResult()
    {
        // Arrange
        var request = new SentimentRequest { Text = "I love this product!" };
        var expectedResponse = new SentimentResponse
        {
            Sentiment = "Positive",
            ConfidenceScore = 0.95,
            Explanation = "The text expresses strong positive emotions",
            EmotionBreakdown = new Dictionary<string, double>
            {
                { "joy", 0.9 },
                { "satisfaction", 0.85 }
            }
        };

        _mockService.Setup(s => s.AnalyzeSentimentAsync(request.Text))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.AnalyzeSentiment(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<SentimentResponse>(okResult.Value);
        Assert.Equal("Positive", response.Sentiment);
        Assert.Equal(0.95, response.ConfidenceScore);
    }

    [Fact]
    public async Task AnalyzeSentiment_WithEmptyText_ReturnsBadRequest()
    {
        // Arrange
        var request = new SentimentRequest { Text = "" };

        // Act
        var result = await _controller.AnalyzeSentiment(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task AnalyzeSentiment_WithWhitespaceText_ReturnsBadRequest()
    {
        // Arrange
        var request = new SentimentRequest { Text = "   " };

        // Act
        var result = await _controller.AnalyzeSentiment(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task AnalyzeSentiment_WithTextExceeding5000Chars_ReturnsBadRequest()
    {
        // Arrange
        var request = new SentimentRequest { Text = new string('a', 5001) };

        // Act
        var result = await _controller.AnalyzeSentiment(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task AnalyzeSentiment_WhenServiceThrows_ReturnsInternalServerError()
    {
        // Arrange
        var request = new SentimentRequest { Text = "Test text" };
        _mockService.Setup(s => s.AnalyzeSentimentAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("Service error"));

        // Act
        var result = await _controller.AnalyzeSentiment(request);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, statusCodeResult.StatusCode);
    }

    [Fact]
    public void Health_ReturnsOkWithStatus()
    {
        // Act
        var result = _controller.Health();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Theory]
    [InlineData("I love this!")]
    [InlineData("This is terrible")]
    [InlineData("The meeting is at 3 PM")]
    public async Task AnalyzeSentiment_WithVariousTexts_CallsServiceCorrectly(string text)
    {
        // Arrange
        var request = new SentimentRequest { Text = text };
        _mockService.Setup(s => s.AnalyzeSentimentAsync(text))
            .ReturnsAsync(new SentimentResponse
            {
                Sentiment = "Neutral",
                ConfidenceScore = 0.5,
                Explanation = "Test"
            });

        // Act
        await _controller.AnalyzeSentiment(request);

        // Assert
        _mockService.Verify(s => s.AnalyzeSentimentAsync(text), Times.Once);
    }
}
