using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using SentimentAnalyzer.Agents.Configuration;
using SentimentAnalyzer.API.Services.Embeddings;
using Xunit;

namespace SentimentAnalyzer.Tests;

/// <summary>
/// Tests for JinaEmbeddingService.
/// Uses mocked HttpMessageHandler to simulate Jina AI API responses (OpenAI-compatible format).
/// Jina AI jina-embeddings-v3: 1024-dim, 1M tokens free, no credit card required.
/// </summary>
public class JinaEmbeddingServiceTests
{
    private readonly Mock<ILogger<JinaEmbeddingService>> _loggerMock = new();

    private JinaEmbeddingService CreateService(
        string apiKey,
        HttpMessageHandler handler,
        string model = "jina-embeddings-v3",
        string endpoint = "https://api.jina.ai/v1")
    {
        var settings = new AgentSystemSettings
        {
            Jina = new JinaSettings
            {
                ApiKey = apiKey,
                Model = model,
                Endpoint = endpoint
            }
        };
        var optionsMock = new Mock<IOptions<AgentSystemSettings>>();
        optionsMock.Setup(o => o.Value).Returns(settings);

        var httpClient = new HttpClient(handler);
        return new JinaEmbeddingService(httpClient, optionsMock.Object, _loggerMock.Object);
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
    public async Task Returns_Empty_When_ApiKey_Missing()
    {
        var handler = CreateMockHandler(HttpStatusCode.OK, "{}");
        var service = CreateService("", handler.Object);

        var result = await service.GenerateEmbeddingAsync(
            "The comprehensive general liability policy covers bodily injury and property damage claims arising from business operations.");

        Assert.False(result.IsSuccess);
        Assert.Contains("not configured", result.ErrorMessage);
        Assert.Equal("Jina", result.Provider);
    }

    [Fact]
    public async Task GenerateEmbedding_Parses_OpenAI_Format_Response()
    {
        var responseJson = """
        {
            "object": "list",
            "data": [
                {
                    "object": "embedding",
                    "embedding": [0.15, -0.28, 0.73, 0.41, -0.62],
                    "index": 0
                }
            ],
            "model": "jina-embeddings-v3",
            "usage": { "total_tokens": 22 }
        }
        """;

        var handler = CreateMockHandler(HttpStatusCode.OK, responseJson);
        var service = CreateService("jina-test-key-abc123", handler.Object);

        var result = await service.GenerateEmbeddingAsync(
            "The comprehensive general liability policy covers bodily injury and property damage claims arising from business operations.");

        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Dimension);
        Assert.Equal(0.15f, result.Embedding[0]);
        Assert.Equal(-0.28f, result.Embedding[1]);
        Assert.Equal(-0.62f, result.Embedding[4]);
        Assert.Equal(22, result.TokensUsed);
        Assert.Equal("Jina", result.Provider);
    }

    [Fact]
    public async Task GenerateEmbeddings_Batch_Parses_Multiple()
    {
        var responseJson = """
        {
            "object": "list",
            "data": [
                { "object": "embedding", "embedding": [0.1, 0.2, 0.3], "index": 0 },
                { "object": "embedding", "embedding": [0.4, 0.5, 0.6], "index": 1 },
                { "object": "embedding", "embedding": [0.7, 0.8, 0.9], "index": 2 }
            ],
            "model": "jina-embeddings-v3",
            "usage": { "total_tokens": 58 }
        }
        """;

        var handler = CreateMockHandler(HttpStatusCode.OK, responseJson);
        var service = CreateService("jina-test-key-abc123", handler.Object);

        var result = await service.GenerateBatchEmbeddingsAsync(new[]
        {
            "Commercial general liability declarations page with named insured and coverage limits.",
            "Property damage exclusion clause for earthquake and flood perils.",
            "Workers compensation supplementary payments and medical expense coverage terms."
        }, "document");

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Count);
        Assert.Equal(3, result.Dimension);
        Assert.Equal(58, result.TotalTokensUsed);
        Assert.Equal(0.4f, result.Embeddings[1][0]);
        Assert.Equal(0.9f, result.Embeddings[2][2]);
    }

    [Fact]
    public async Task Returns_Empty_On_429_HttpError()
    {
        var handler = CreateMockHandler(HttpStatusCode.TooManyRequests,
            """{"detail":"Rate limit exceeded. Free tier: 1M tokens for jina-embeddings-v3."}""");
        var service = CreateService("jina-test-key-abc123", handler.Object);

        var result = await service.GenerateEmbeddingAsync(
            "The comprehensive general liability policy covers bodily injury and property damage claims arising from business operations.");

        Assert.False(result.IsSuccess);
        Assert.Contains("TooManyRequests", result.ErrorMessage);
        Assert.Equal("Jina", result.Provider);
    }

    [Fact]
    public async Task Returns_Empty_On_500_HttpError()
    {
        var handler = CreateMockHandler(HttpStatusCode.InternalServerError,
            """{"detail":"Internal server error"}""");
        var service = CreateService("jina-test-key-abc123", handler.Object);

        var result = await service.GenerateEmbeddingAsync(
            "Umbrella liability policy providing excess coverage above primary commercial general liability limits.");

        Assert.False(result.IsSuccess);
        Assert.Contains("InternalServerError", result.ErrorMessage);
        Assert.Equal("Jina", result.Provider);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WithEmptyText_ReturnsError()
    {
        var handler = CreateMockHandler(HttpStatusCode.OK, "{}");
        var service = CreateService("jina-test-key-abc123", handler.Object);

        var result = await service.GenerateEmbeddingAsync("");

        Assert.False(result.IsSuccess);
        Assert.Contains("empty", result.ErrorMessage);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WithNetworkError_ReturnsError()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var service = CreateService("jina-test-key-abc123", handlerMock.Object);
        var result = await service.GenerateEmbeddingAsync(
            "Professional liability endorsement for insurance agents and brokers errors and omissions.");

        Assert.False(result.IsSuccess);
        Assert.Contains("Connection refused", result.ErrorMessage);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_SendsAuthorizationHeader()
    {
        var responseJson = """
        {
            "object": "list",
            "data": [{ "object": "embedding", "embedding": [0.1], "index": 0 }],
            "model": "jina-embeddings-v3",
            "usage": { "total_tokens": 5 }
        }
        """;

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
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });

        var service = CreateService("jina-secret-api-key", handlerMock.Object);
        await service.GenerateEmbeddingAsync("Subrogation rights under commercial property insurance policy.");

        Assert.NotNull(capturedRequest);
        Assert.Equal("Bearer", capturedRequest!.Headers.Authorization?.Scheme);
        Assert.Equal("jina-secret-api-key", capturedRequest.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_RecordsElapsedTime()
    {
        var responseJson = """
        {
            "object": "list",
            "data": [{ "object": "embedding", "embedding": [0.1], "index": 0 }],
            "model": "jina-embeddings-v3",
            "usage": { "total_tokens": 3 }
        }
        """;

        var handler = CreateMockHandler(HttpStatusCode.OK, responseJson);
        var service = CreateService("jina-test-key-abc123", handler.Object);

        var result = await service.GenerateEmbeddingAsync(
            "Business interruption coverage following covered property damage event.");

        Assert.True(result.IsSuccess);
        Assert.True(result.ElapsedMilliseconds >= 0);
    }

    [Fact]
    public void ProviderName_ReturnsJina()
    {
        var handler = CreateMockHandler(HttpStatusCode.OK, "{}");
        var service = CreateService("jina-test-key-abc123", handler.Object);

        Assert.Equal("Jina", service.ProviderName);
    }

    [Fact]
    public void EmbeddingDimension_Returns1024()
    {
        var handler = CreateMockHandler(HttpStatusCode.OK, "{}");
        var service = CreateService("jina-test-key-abc123", handler.Object);

        Assert.Equal(1024, service.EmbeddingDimension);
    }

    [Fact]
    public async Task GenerateBatchEmbeddingsAsync_WithEmptyArray_ReturnsError()
    {
        var handler = CreateMockHandler(HttpStatusCode.OK, "{}");
        var service = CreateService("jina-test-key-abc123", handler.Object);

        var result = await service.GenerateBatchEmbeddingsAsync([]);

        Assert.False(result.IsSuccess);
        Assert.Contains("empty", result.ErrorMessage);
    }
}
