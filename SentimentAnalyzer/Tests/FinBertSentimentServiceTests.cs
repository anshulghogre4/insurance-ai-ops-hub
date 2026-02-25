using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using SentimentAnalyzer.Agents.Configuration;
using SentimentAnalyzer.API.Services.Multimodal;
using Xunit;

namespace SentimentAnalyzer.Tests;

/// <summary>
/// Tests for FinBertSentimentService.
/// Uses mocked HttpMessageHandler to simulate HuggingFace Inference API responses.
/// </summary>
public class FinBertSentimentServiceTests
{
    private readonly Mock<ILogger<FinBertSentimentService>> _loggerMock = new();

    private FinBertSentimentService CreateService(
        string apiKey,
        HttpMessageHandler handler,
        double confidenceThreshold = 0.85)
    {
        var settings = new AgentSystemSettings
        {
            HuggingFace = new HuggingFaceSettings
            {
                ApiKey = apiKey,
                SentimentModel = "ProsusAI/finbert",
                PreScreenConfidenceThreshold = confidenceThreshold
            }
        };
        var optionsMock = new Mock<IOptions<AgentSystemSettings>>();
        optionsMock.Setup(o => o.Value).Returns(settings);

        var httpClient = new HttpClient(handler);
        return new FinBertSentimentService(httpClient, optionsMock.Object, _loggerMock.Object);
    }

    private static Mock<HttpMessageHandler> CreateMockHandler(
        HttpStatusCode statusCode, string responseBody)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            });
        return handlerMock;
    }

    [Fact]
    public async Task PreScreenAsync_WithHighConfidenceNegative_ReturnsHighConfidence()
    {
        var responseJson = """
        [[
            {"label": "negative", "score": 0.94},
            {"label": "neutral", "score": 0.04},
            {"label": "positive", "score": 0.02}
        ]]
        """;

        var handler = CreateMockHandler(HttpStatusCode.OK, responseJson);
        var service = CreateService("hf_test_key_123", handler.Object);

        var result = await service.PreScreenAsync(
            "I am extremely dissatisfied with how my claim was handled. Terrible service.");

        Assert.True(result.IsSuccess);
        Assert.Equal("negative", result.Sentiment);
        Assert.Equal(0.94, result.TopScore);
        Assert.True(result.IsHighConfidence);
        Assert.Equal(3, result.Scores.Count);
        Assert.Equal("HuggingFace/FinBERT", result.Provider);
    }

    [Fact]
    public async Task PreScreenAsync_WithHighConfidencePositive_ReturnsHighConfidence()
    {
        var responseJson = """
        [[
            {"label": "positive", "score": 0.91},
            {"label": "neutral", "score": 0.06},
            {"label": "negative", "score": 0.03}
        ]]
        """;

        var handler = CreateMockHandler(HttpStatusCode.OK, responseJson);
        var service = CreateService("hf_test_key_123", handler.Object);

        var result = await service.PreScreenAsync(
            "My agent was wonderful and got my claim approved quickly. Great experience!");

        Assert.True(result.IsSuccess);
        Assert.Equal("positive", result.Sentiment);
        Assert.Equal(0.91, result.TopScore);
        Assert.True(result.IsHighConfidence);
    }

    [Fact]
    public async Task PreScreenAsync_WithLowConfidence_ReturnsNotHighConfidence()
    {
        var responseJson = """
        [[
            {"label": "neutral", "score": 0.45},
            {"label": "negative", "score": 0.35},
            {"label": "positive", "score": 0.20}
        ]]
        """;

        var handler = CreateMockHandler(HttpStatusCode.OK, responseJson);
        var service = CreateService("hf_test_key_123", handler.Object);

        var result = await service.PreScreenAsync(
            "I received a letter about my policy renewal terms and conditions.");

        Assert.True(result.IsSuccess);
        Assert.Equal("neutral", result.Sentiment);
        Assert.Equal(0.45, result.TopScore);
        Assert.False(result.IsHighConfidence);
    }

    [Fact]
    public async Task PreScreenAsync_WithMissingApiKey_ReturnsError()
    {
        var handler = CreateMockHandler(HttpStatusCode.OK, "[[]]");
        var service = CreateService("", handler.Object);

        var result = await service.PreScreenAsync("Some insurance claim text.");

        Assert.False(result.IsSuccess);
        Assert.Contains("not configured", result.ErrorMessage);
    }

    [Fact]
    public async Task PreScreenAsync_WithColdStart503_ReturnsModelLoading()
    {
        var responseJson = """{"error":"Model is currently loading","estimated_time":25.0}""";
        var handler = CreateMockHandler(HttpStatusCode.ServiceUnavailable, responseJson);
        var service = CreateService("hf_test_key_123", handler.Object);

        var result = await service.PreScreenAsync(
            "Filed a water damage claim under my homeowner's policy.");

        Assert.False(result.IsSuccess);
        Assert.Contains("loading", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PreScreenAsync_WithNetworkError_ReturnsError()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection timeout"));

        var service = CreateService("hf_test_key_123", handlerMock.Object);
        var result = await service.PreScreenAsync("Insurance claim text for analysis.");

        Assert.False(result.IsSuccess);
        Assert.Contains("Connection timeout", result.ErrorMessage);
    }

    [Fact]
    public async Task PreScreenAsync_CustomThreshold_AffectsHighConfidenceFlag()
    {
        var responseJson = """
        [[
            {"label": "negative", "score": 0.80},
            {"label": "neutral", "score": 0.15},
            {"label": "positive", "score": 0.05}
        ]]
        """;

        // With default 0.85 threshold, 0.80 is NOT high confidence
        var handler85 = CreateMockHandler(HttpStatusCode.OK, responseJson);
        var service85 = CreateService("hf_test_key_123", handler85.Object, confidenceThreshold: 0.85);
        var result85 = await service85.PreScreenAsync("My claim was denied without explanation.");
        Assert.False(result85.IsHighConfidence);

        // With 0.75 threshold, 0.80 IS high confidence
        var handler75 = CreateMockHandler(HttpStatusCode.OK, responseJson);
        var service75 = CreateService("hf_test_key_123", handler75.Object, confidenceThreshold: 0.75);
        var result75 = await service75.PreScreenAsync("My claim was denied without explanation.");
        Assert.True(result75.IsHighConfidence);
    }

    [Fact]
    public async Task PreScreenAsync_RecordsElapsedTime()
    {
        var responseJson = """
        [[
            {"label": "neutral", "score": 0.90},
            {"label": "negative", "score": 0.05},
            {"label": "positive", "score": 0.05}
        ]]
        """;

        var handler = CreateMockHandler(HttpStatusCode.OK, responseJson);
        var service = CreateService("hf_test_key_123", handler.Object);

        var result = await service.PreScreenAsync("Policy renewal notice received for annual review.");

        Assert.True(result.IsSuccess);
        Assert.True(result.ElapsedMilliseconds >= 0);
    }
}
