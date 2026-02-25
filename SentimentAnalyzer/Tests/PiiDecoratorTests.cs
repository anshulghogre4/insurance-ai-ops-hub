using Microsoft.Extensions.Logging;
using Moq;
using SentimentAnalyzer.Agents.Orchestration;
using SentimentAnalyzer.API.Models;
using SentimentAnalyzer.API.Services;
using Xunit;

namespace SentimentAnalyzer.Tests;

/// <summary>
/// Tests for PiiRedactingSentimentService — the decorator wrapping ISentimentService.
/// Verifies PII is redacted before reaching the external AI provider.
/// </summary>
public class PiiDecoratorTests
{
    private readonly Mock<ISentimentService> _mockInner;
    private readonly Mock<IPIIRedactor> _mockRedactor;
    private readonly Mock<ILogger<PiiRedactingSentimentService>> _mockLogger;
    private readonly PiiRedactingSentimentService _decorator;

    public PiiDecoratorTests()
    {
        _mockInner = new Mock<ISentimentService>();
        _mockRedactor = new Mock<IPIIRedactor>();
        _mockLogger = new Mock<ILogger<PiiRedactingSentimentService>>();
        _decorator = new PiiRedactingSentimentService(
            _mockInner.Object,
            _mockRedactor.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task AnalyzeSentimentAsync_RedactsPIIBeforeCallingInner()
    {
        // Arrange
        var rawText = "My SSN is 123-45-6789. Policy HO-2024-789456. Contact at john@test.com.";
        var redactedText = "My SSN is [SSN-REDACTED]. Policy [POLICY-REDACTED]. Contact at [EMAIL-REDACTED].";
        var expectedResponse = new SentimentResponse { Sentiment = "Negative", ConfidenceScore = 0.85 };

        _mockRedactor.Setup(r => r.Redact(rawText)).Returns(redactedText);
        _mockInner.Setup(s => s.AnalyzeSentimentAsync(redactedText)).ReturnsAsync(expectedResponse);

        // Act
        var result = await _decorator.AnalyzeSentimentAsync(rawText);

        // Assert — inner service receives REDACTED text, not raw PII
        _mockRedactor.Verify(r => r.Redact(rawText), Times.Once);
        _mockInner.Verify(s => s.AnalyzeSentimentAsync(redactedText), Times.Once);
        _mockInner.Verify(s => s.AnalyzeSentimentAsync(rawText), Times.Never);
        Assert.Equal("Negative", result.Sentiment);
    }

    [Fact]
    public async Task AnalyzeSentimentAsync_WithNoPII_PassesThroughUnchanged()
    {
        // Arrange
        var cleanText = "I am happy with my insurance coverage and service.";
        var expectedResponse = new SentimentResponse { Sentiment = "Positive", ConfidenceScore = 0.95 };

        _mockRedactor.Setup(r => r.Redact(cleanText)).Returns(cleanText); // No change
        _mockInner.Setup(s => s.AnalyzeSentimentAsync(cleanText)).ReturnsAsync(expectedResponse);

        // Act
        var result = await _decorator.AnalyzeSentimentAsync(cleanText);

        // Assert
        _mockInner.Verify(s => s.AnalyzeSentimentAsync(cleanText), Times.Once);
        Assert.Equal("Positive", result.Sentiment);
    }

    [Fact]
    public async Task AnalyzeSentimentAsync_RedactsSSN()
    {
        // Arrange
        var text = "Policyholder SSN 456-78-9012 requests claim review.";
        var redacted = "Policyholder SSN [SSN-REDACTED] requests claim review.";

        _mockRedactor.Setup(r => r.Redact(text)).Returns(redacted);
        _mockInner.Setup(s => s.AnalyzeSentimentAsync(It.IsAny<string>()))
            .ReturnsAsync(new SentimentResponse { Sentiment = "Neutral" });

        // Act
        await _decorator.AnalyzeSentimentAsync(text);

        // Assert
        _mockInner.Verify(s => s.AnalyzeSentimentAsync(redacted), Times.Once);
    }

    [Fact]
    public async Task AnalyzeSentimentAsync_RedactsClaimNumber()
    {
        // Arrange
        var text = "Claim CLM-2024-78901234 was denied unfairly.";
        var redacted = "Claim [CLAIM-REDACTED] was denied unfairly.";

        _mockRedactor.Setup(r => r.Redact(text)).Returns(redacted);
        _mockInner.Setup(s => s.AnalyzeSentimentAsync(It.IsAny<string>()))
            .ReturnsAsync(new SentimentResponse { Sentiment = "Negative" });

        // Act
        await _decorator.AnalyzeSentimentAsync(text);

        // Assert
        _mockInner.Verify(s => s.AnalyzeSentimentAsync(redacted), Times.Once);
    }

    [Fact]
    public async Task AnalyzeSentimentAsync_RedactsPhoneAndEmail()
    {
        // Arrange
        var text = "Call me at 555-123-4567 or email john@insurance.com about my policy.";
        var redacted = "Call me at [PHONE-REDACTED] or email [EMAIL-REDACTED] about my policy.";

        _mockRedactor.Setup(r => r.Redact(text)).Returns(redacted);
        _mockInner.Setup(s => s.AnalyzeSentimentAsync(It.IsAny<string>()))
            .ReturnsAsync(new SentimentResponse { Sentiment = "Neutral" });

        // Act
        await _decorator.AnalyzeSentimentAsync(text);

        // Assert
        _mockInner.Verify(s => s.AnalyzeSentimentAsync(redacted), Times.Once);
    }

    [Fact]
    public async Task AnalyzeSentimentAsync_PropagatesInnerServiceResponse()
    {
        // Arrange
        var expectedResponse = new SentimentResponse
        {
            Sentiment = "Negative",
            ConfidenceScore = 0.92,
            Explanation = "Strong negative sentiment detected about claim denial",
            EmotionBreakdown = new Dictionary<string, double> { { "anger", 0.8 }, { "frustration", 0.9 } }
        };

        _mockRedactor.Setup(r => r.Redact(It.IsAny<string>())).Returns((string s) => s);
        _mockInner.Setup(s => s.AnalyzeSentimentAsync(It.IsAny<string>())).ReturnsAsync(expectedResponse);

        // Act
        var result = await _decorator.AnalyzeSentimentAsync("Claim denied unfairly");

        // Assert
        Assert.Equal("Negative", result.Sentiment);
        Assert.Equal(0.92, result.ConfidenceScore);
        Assert.Equal("Strong negative sentiment detected about claim denial", result.Explanation);
        Assert.Equal(0.8, result.EmotionBreakdown["anger"]);
    }

    [Fact]
    public async Task AnalyzeSentimentAsync_PropagatesInnerServiceException()
    {
        // Arrange
        _mockRedactor.Setup(r => r.Redact(It.IsAny<string>())).Returns((string s) => s);
        _mockInner.Setup(s => s.AnalyzeSentimentAsync(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("OpenAI API unavailable"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _decorator.AnalyzeSentimentAsync("Test claim text"));
    }

    [Fact]
    public void Constructor_ThrowsOnNullInner()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new PiiRedactingSentimentService(null!, _mockRedactor.Object, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_ThrowsOnNullRedactor()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new PiiRedactingSentimentService(_mockInner.Object, null!, _mockLogger.Object));
    }
}
