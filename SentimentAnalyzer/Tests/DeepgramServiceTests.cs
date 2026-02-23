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
/// Tests for DeepgramSpeechToTextService.
/// Uses mocked HttpMessageHandler to simulate Deepgram API responses.
/// </summary>
public class DeepgramServiceTests
{
    private readonly Mock<ILogger<DeepgramSpeechToTextService>> _loggerMock = new();

    private DeepgramSpeechToTextService CreateService(string apiKey, HttpMessageHandler handler)
    {
        var settings = new AgentSystemSettings
        {
            Deepgram = new DeepgramSettings { ApiKey = apiKey }
        };
        var optionsMock = new Mock<IOptions<AgentSystemSettings>>();
        optionsMock.Setup(o => o.Value).Returns(settings);

        var httpClient = new HttpClient(handler);
        return new DeepgramSpeechToTextService(httpClient, optionsMock.Object, _loggerMock.Object);
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
    public async Task TranscribeAsync_WithValidAudio_ReturnsTranscription()
    {
        var responseJson = """
        {
            "metadata": { "duration": 5.2 },
            "results": {
                "channels": [{
                    "alternatives": [{
                        "transcript": "I need to file a claim for water damage to my basement.",
                        "confidence": 0.95
                    }]
                }]
            }
        }
        """;

        var handler = CreateMockHandler(HttpStatusCode.OK, responseJson);
        var service = CreateService("dgp_test_key_123", handler.Object);

        var result = await service.TranscribeAsync(new byte[] { 1, 2, 3 }, "audio/wav");

        Assert.True(result.IsSuccess);
        Assert.Equal("Deepgram", result.Provider);
        Assert.Contains("water damage", result.Text);
        Assert.Equal(0.95, result.Confidence);
        Assert.Equal(5.2, result.DurationSeconds);
    }

    [Fact]
    public async Task TranscribeAsync_WithMissingApiKey_ReturnsError()
    {
        var handler = CreateMockHandler(HttpStatusCode.OK, "{}");
        var service = CreateService("", handler.Object);

        var result = await service.TranscribeAsync(new byte[] { 1, 2, 3 });

        Assert.False(result.IsSuccess);
        Assert.Contains("not configured", result.ErrorMessage);
    }

    [Fact]
    public async Task TranscribeAsync_WithApiError_ReturnsError()
    {
        var handler = CreateMockHandler(HttpStatusCode.TooManyRequests, """{"error":"Rate limit exceeded"}""");
        var service = CreateService("dgp_test_key_123", handler.Object);

        var result = await service.TranscribeAsync(new byte[] { 1, 2, 3 });

        Assert.False(result.IsSuccess);
        Assert.Contains("TooManyRequests", result.ErrorMessage);
    }

    [Fact]
    public async Task TranscribeAsync_WithNetworkError_ReturnsError()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var service = CreateService("dgp_test_key_123", handlerMock.Object);
        var result = await service.TranscribeAsync(new byte[] { 1, 2, 3 });

        Assert.False(result.IsSuccess);
        Assert.Contains("Connection refused", result.ErrorMessage);
    }
}
