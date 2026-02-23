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
/// Tests for HuggingFaceNerService.
/// Uses mocked HttpMessageHandler to simulate HuggingFace Inference API responses.
/// </summary>
public class HuggingFaceNerServiceTests
{
    private readonly Mock<ILogger<HuggingFaceNerService>> _loggerMock = new();

    private HuggingFaceNerService CreateService(string apiKey, HttpMessageHandler handler)
    {
        var settings = new AgentSystemSettings
        {
            HuggingFace = new HuggingFaceSettings { ApiKey = apiKey }
        };
        var optionsMock = new Mock<IOptions<AgentSystemSettings>>();
        optionsMock.Setup(o => o.Value).Returns(settings);

        var httpClient = new HttpClient(handler);
        return new HuggingFaceNerService(httpClient, optionsMock.Object, _loggerMock.Object);
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
    public async Task ExtractEntitiesAsync_WithValidText_ReturnsEntities()
    {
        var responseJson = """
        [
            { "entity_group": "PER", "word": "Jane Smith", "score": 0.98, "start": 0, "end": 10 },
            { "entity_group": "ORG", "word": "Acme Insurance", "score": 0.95, "start": 25, "end": 39 },
            { "entity_group": "LOC", "word": "Springfield", "score": 0.92, "start": 50, "end": 61 }
        ]
        """;

        var handler = CreateMockHandler(HttpStatusCode.OK, responseJson);
        var service = CreateService("hf_test_key_123", handler.Object);

        var result = await service.ExtractEntitiesAsync("Jane Smith filed a claim with Acme Insurance in Springfield.");

        Assert.True(result.IsSuccess);
        Assert.Equal("HuggingFace", result.Provider);
        Assert.Equal(3, result.Entities.Count);
    }

    [Fact]
    public async Task ExtractEntitiesAsync_MapsEntityTypes_Correctly()
    {
        var responseJson = """
        [
            { "entity_group": "PER", "word": "John Doe", "score": 0.98, "start": 0, "end": 8 },
            { "entity_group": "ORG", "word": "State Farm", "score": 0.95, "start": 20, "end": 30 },
            { "entity_group": "LOC", "word": "Chicago", "score": 0.92, "start": 40, "end": 47 },
            { "entity_group": "MISC", "word": "Policy HO-2024", "score": 0.88, "start": 55, "end": 69 }
        ]
        """;

        var handler = CreateMockHandler(HttpStatusCode.OK, responseJson);
        var service = CreateService("hf_test_key_123", handler.Object);

        var result = await service.ExtractEntitiesAsync("John Doe insured with State Farm, lives in Chicago, Policy HO-2024.");

        Assert.Equal("PERSON", result.Entities[0].Type);
        Assert.Equal("ORGANIZATION", result.Entities[1].Type);
        Assert.Equal("LOCATION", result.Entities[2].Type);
        Assert.Equal("MISCELLANEOUS", result.Entities[3].Type);
    }

    [Fact]
    public async Task ExtractEntitiesAsync_WithMissingApiKey_ReturnsError()
    {
        var handler = CreateMockHandler(HttpStatusCode.OK, "[]");
        var service = CreateService("", handler.Object);

        var result = await service.ExtractEntitiesAsync("Some insurance text.");

        Assert.False(result.IsSuccess);
        Assert.Contains("not configured", result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractEntitiesAsync_WithColdStart503_ReturnsModelLoading()
    {
        var responseJson = """{"error":"Model is currently loading","estimated_time":25.0}""";
        var handler = CreateMockHandler(HttpStatusCode.ServiceUnavailable, responseJson);
        var service = CreateService("hf_test_key_123", handler.Object);

        var result = await service.ExtractEntitiesAsync("Test text for entity extraction.");

        Assert.False(result.IsSuccess);
        Assert.Contains("loading", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExtractEntitiesAsync_PreservesPositionInfo()
    {
        var responseJson = """
        [
            { "entity_group": "PER", "word": "Jane", "score": 0.97, "start": 0, "end": 4 }
        ]
        """;

        var handler = CreateMockHandler(HttpStatusCode.OK, responseJson);
        var service = CreateService("hf_test_key_123", handler.Object);

        var result = await service.ExtractEntitiesAsync("Jane reported a claim.");

        Assert.Single(result.Entities);
        Assert.Equal(0, result.Entities[0].StartIndex);
        Assert.Equal(4, result.Entities[0].EndIndex);
        Assert.Equal(0.97, result.Entities[0].Confidence);
    }

    [Fact]
    public async Task ExtractEntitiesAsync_WithNetworkError_ReturnsError()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection timeout"));

        var service = CreateService("hf_test_key_123", handlerMock.Object);
        var result = await service.ExtractEntitiesAsync("Test text.");

        Assert.False(result.IsSuccess);
        Assert.Contains("Connection timeout", result.ErrorMessage);
    }
}
