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
/// Tests for AzureSpeechToTextService (Tier 2 STT).
/// Tests validation paths (API key, region, empty audio) that don't require a live Azure connection,
/// plus HTTP-mocked paths for success and error responses.
/// </summary>
public class AzureSpeechToTextServiceTests
{
    private readonly Mock<ILogger<AzureSpeechToTextService>> _loggerMock = new();

    /// <summary>
    /// Creates an AzureSpeechToTextService with the given configuration.
    /// </summary>
    private AzureSpeechToTextService CreateService(
        string apiKey = "",
        string region = "",
        HttpClient? httpClient = null)
    {
        var settings = new AgentSystemSettings
        {
            AzureSpeech = new AzureSpeechSettings
            {
                ApiKey = apiKey,
                Region = region
            }
        };
        var optionsMock = new Mock<IOptions<AgentSystemSettings>>();
        optionsMock.Setup(o => o.Value).Returns(settings);

        return new AzureSpeechToTextService(
            httpClient ?? new HttpClient(),
            optionsMock.Object,
            _loggerMock.Object);
    }

    /// <summary>
    /// Creates a mocked HttpMessageHandler that returns the specified status code and response body.
    /// </summary>
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
    public async Task TranscribeAsync_MissingApiKey_ReturnsFailure()
    {
        // Arrange: no API key configured
        var service = CreateService(apiKey: "", region: "eastus");

        // Act
        var result = await service.TranscribeAsync(new byte[] { 1, 2, 3 });

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("AzureSpeech", result.Provider);
        Assert.Contains("API key not configured", result.ErrorMessage);
    }

    [Fact]
    public async Task TranscribeAsync_MissingRegion_ReturnsFailure()
    {
        // Arrange: no region configured
        var service = CreateService(apiKey: "azure_speech_key_abc123", region: "");

        // Act
        var result = await service.TranscribeAsync(new byte[] { 1, 2, 3 });

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("AzureSpeech", result.Provider);
        Assert.Contains("region not configured", result.ErrorMessage);
    }

    [Fact]
    public async Task TranscribeAsync_EmptyAudioData_ReturnsFailure()
    {
        // Arrange: valid config but empty audio
        var handler = CreateMockHandler(HttpStatusCode.OK, "{}");
        var service = CreateService(
            apiKey: "azure_speech_key_abc123",
            region: "eastus",
            httpClient: new HttpClient(handler.Object));

        // Act
        var result = await service.TranscribeAsync(Array.Empty<byte>());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("AzureSpeech", result.Provider);
        Assert.Contains("empty", result.ErrorMessage);
    }

    [Fact]
    public async Task Provider_ReturnsAzureSpeech()
    {
        // Arrange: even on validation failure, provider name must be "AzureSpeech"
        var service = CreateService(apiKey: "", region: "");

        // Act
        var result = await service.TranscribeAsync(new byte[] { 1, 2, 3 });

        // Assert
        Assert.Equal("AzureSpeech", result.Provider);
    }

    [Fact]
    public async Task TranscribeAsync_SuccessfulResponse_ReturnsTranscription()
    {
        // Arrange: mock a successful Azure Speech response
        var responseJson = """
        {
            "RecognitionStatus": "Success",
            "DisplayText": "Policyholder reports a vehicle collision at the intersection of Main Street and Oak Avenue. Estimated damage approximately five thousand dollars.",
            "Offset": 0,
            "Duration": 52000000
        }
        """;

        var handler = CreateMockHandler(HttpStatusCode.OK, responseJson);
        var service = CreateService(
            apiKey: "azure_speech_key_abc123",
            region: "eastus",
            httpClient: new HttpClient(handler.Object));

        // Act
        var result = await service.TranscribeAsync(
            new byte[] { 0x52, 0x49, 0x46, 0x46 }, // RIFF header bytes
            "audio/wav");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("AzureSpeech", result.Provider);
        Assert.Contains("vehicle collision", result.Text);
        Assert.Contains("Main Street", result.Text);
        Assert.Equal(5.2, result.DurationSeconds, 1);
        Assert.Equal(0.9, result.Confidence);
    }

    [Fact]
    public async Task TranscribeAsync_NoMatchStatus_ReturnsFailure()
    {
        // Arrange: Azure returns NoMatch when it can't recognize speech
        var responseJson = """
        {
            "RecognitionStatus": "NoMatch",
            "DisplayText": "",
            "Offset": 0,
            "Duration": 0
        }
        """;

        var handler = CreateMockHandler(HttpStatusCode.OK, responseJson);
        var service = CreateService(
            apiKey: "azure_speech_key_abc123",
            region: "eastus",
            httpClient: new HttpClient(handler.Object));

        // Act
        var result = await service.TranscribeAsync(new byte[] { 1, 2, 3 }, "audio/wav");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("AzureSpeech", result.Provider);
        Assert.Contains("NoMatch", result.ErrorMessage);
    }

    [Fact]
    public async Task TranscribeAsync_ApiReturns429_ReturnsError()
    {
        // Arrange: Azure returns 429 when F0 tier 5-hour limit is exceeded
        var handler = CreateMockHandler(
            HttpStatusCode.TooManyRequests,
            """{"error":"Rate limit exceeded"}""");
        var service = CreateService(
            apiKey: "azure_speech_key_abc123",
            region: "eastus",
            httpClient: new HttpClient(handler.Object));

        // Act
        var result = await service.TranscribeAsync(new byte[] { 1, 2, 3 });

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("AzureSpeech", result.Provider);
        Assert.Contains("TooManyRequests", result.ErrorMessage);
    }

    [Fact]
    public async Task TranscribeAsync_NetworkError_ReturnsError()
    {
        // Arrange: simulate network connectivity failure
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var service = CreateService(
            apiKey: "azure_speech_key_abc123",
            region: "eastus",
            httpClient: new HttpClient(handlerMock.Object));

        // Act
        var result = await service.TranscribeAsync(new byte[] { 1, 2, 3 });

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("AzureSpeech", result.Provider);
        Assert.Contains("Connection refused", result.ErrorMessage);
    }

    [Fact]
    public async Task TranscribeAsync_SendsCorrectHeaders()
    {
        // Arrange: capture the request to verify headers
        HttpRequestMessage? capturedRequest = null;
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(
                    """{"RecognitionStatus":"Success","DisplayText":"Test claim report.","Offset":0,"Duration":10000000}""",
                    Encoding.UTF8, "application/json")
            });

        var service = CreateService(
            apiKey: "azure_speech_key_test_456",
            region: "westus2",
            httpClient: new HttpClient(handlerMock.Object));

        // Act
        await service.TranscribeAsync(new byte[] { 1, 2, 3 }, "audio/mpeg");

        // Assert: verify the request was constructed correctly
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Contains("westus2.stt.speech.microsoft.com", capturedRequest.RequestUri!.Host);
        Assert.Contains("language=en-US", capturedRequest.RequestUri.Query);
        Assert.True(capturedRequest.Headers.Contains("Ocp-Apim-Subscription-Key"));
        Assert.Equal("audio/mpeg", capturedRequest.Content!.Headers.ContentType!.MediaType);
    }
}
