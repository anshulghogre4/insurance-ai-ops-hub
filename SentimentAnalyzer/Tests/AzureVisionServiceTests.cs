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
/// Tests for AzureVisionService.
/// Uses mocked HttpMessageHandler to simulate Azure Vision API responses.
/// </summary>
public class AzureVisionServiceTests
{
    private readonly Mock<ILogger<AzureVisionService>> _loggerMock = new();

    private AzureVisionService CreateService(string apiKey, string endpoint, HttpMessageHandler handler)
    {
        var settings = new AgentSystemSettings
        {
            AzureVision = new AzureVisionSettings { ApiKey = apiKey, Endpoint = endpoint }
        };
        var optionsMock = new Mock<IOptions<AgentSystemSettings>>();
        optionsMock.Setup(o => o.Value).Returns(settings);

        var httpClient = new HttpClient(handler);
        return new AzureVisionService(httpClient, optionsMock.Object, _loggerMock.Object);
    }

    private static Mock<HttpMessageHandler> CreateMockHandler(HttpStatusCode statusCode, string responseBody)
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
    public async Task AnalyzeImageAsync_WithValidImage_ReturnsLabels()
    {
        var responseJson = """
        {
            "captionResult": {
                "text": "A flooded basement with visible water damage on walls",
                "confidence": 0.92
            },
            "tagsResult": {
                "values": [
                    { "name": "water damage", "confidence": 0.95 },
                    { "name": "basement", "confidence": 0.88 },
                    { "name": "flood", "confidence": 0.82 },
                    { "name": "indoor", "confidence": 0.75 }
                ]
            }
        }
        """;

        var handler = CreateMockHandler(HttpStatusCode.OK, responseJson);
        var service = CreateService("test-azure-key", "https://test.cognitiveservices.azure.com", handler.Object);

        var result = await service.AnalyzeImageAsync(new byte[] { 1, 2, 3 });

        Assert.True(result.IsSuccess);
        Assert.Equal("AzureVision", result.Provider);
        Assert.Contains("flooded basement", result.Description);
        Assert.Equal(4, result.Labels.Count);
        Assert.Equal(0.92, result.ConfidenceScore);
    }

    [Fact]
    public async Task AnalyzeImageAsync_DetectsDamageIndicators()
    {
        var responseJson = """
        {
            "captionResult": { "text": "Damaged vehicle", "confidence": 0.9 },
            "tagsResult": {
                "values": [
                    { "name": "water damage", "confidence": 0.95 },
                    { "name": "flood", "confidence": 0.82 },
                    { "name": "mold growth", "confidence": 0.7 }
                ]
            }
        }
        """;

        var handler = CreateMockHandler(HttpStatusCode.OK, responseJson);
        var service = CreateService("test-key", "https://test.cognitiveservices.azure.com", handler.Object);

        var result = await service.AnalyzeImageAsync(new byte[] { 1, 2, 3 });

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.DamageIndicators);
        Assert.Contains("water damage", result.DamageIndicators);
        Assert.Contains("flood", result.DamageIndicators);
    }

    [Fact]
    public async Task AnalyzeImageAsync_WithMissingConfig_ReturnsError()
    {
        var handler = CreateMockHandler(HttpStatusCode.OK, "{}");
        var service = CreateService("", "", handler.Object);

        var result = await service.AnalyzeImageAsync(new byte[] { 1, 2, 3 });

        Assert.False(result.IsSuccess);
        Assert.Contains("not configured", result.ErrorMessage);
    }

    [Fact]
    public async Task AnalyzeImageAsync_With429RateLimit_ReturnsError()
    {
        var handler = CreateMockHandler(HttpStatusCode.TooManyRequests, """{"error":"Rate limit exceeded"}""");
        var service = CreateService("test-key", "https://test.cognitiveservices.azure.com", handler.Object);

        var result = await service.AnalyzeImageAsync(new byte[] { 1, 2, 3 });

        Assert.False(result.IsSuccess);
        Assert.Contains("TooManyRequests", result.ErrorMessage);
    }

    [Fact]
    public async Task AnalyzeImageAsync_WithMissingEndpoint_ReturnsError()
    {
        var handler = CreateMockHandler(HttpStatusCode.OK, "{}");
        var service = CreateService("test-key", "", handler.Object);

        var result = await service.AnalyzeImageAsync(new byte[] { 1, 2, 3 });

        Assert.False(result.IsSuccess);
        Assert.Contains("not configured", result.ErrorMessage);
    }
}
