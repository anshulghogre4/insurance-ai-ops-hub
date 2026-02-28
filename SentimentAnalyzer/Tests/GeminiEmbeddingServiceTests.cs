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
/// Tests for GeminiEmbeddingService.
/// Uses mocked HttpMessageHandler to simulate Google Gemini embedding API responses.
/// Gemini text-embedding-004: 1024-dim (via outputDimensionality), 1,500 req/day free.
/// </summary>
public class GeminiEmbeddingServiceTests
{
    private readonly Mock<ILogger<GeminiEmbeddingService>> _loggerMock = new();

    private GeminiEmbeddingService CreateService(
        string apiKey,
        HttpMessageHandler handler,
        string model = "text-embedding-004")
    {
        var settings = new AgentSystemSettings
        {
            GeminiEmbedding = new GeminiEmbeddingSettings
            {
                ApiKey = apiKey,
                Model = model
            }
        };
        var optionsMock = new Mock<IOptions<AgentSystemSettings>>();
        optionsMock.Setup(o => o.Value).Returns(settings);

        var httpClient = new HttpClient(handler);
        return new GeminiEmbeddingService(httpClient, optionsMock.Object, _loggerMock.Object);
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
            "Property damage assessment for commercial building following Category 3 hurricane impact.");

        Assert.False(result.IsSuccess);
        Assert.Contains("not configured", result.ErrorMessage);
        Assert.Equal("GeminiEmbed", result.Provider);
    }

    [Fact]
    public async Task GenerateEmbedding_Parses_Gemini_Format()
    {
        var responseJson = """
        {
            "embedding": {
                "values": [0.34, -0.67, 0.12, 0.89, -0.23]
            }
        }
        """;

        var handler = CreateMockHandler(HttpStatusCode.OK, responseJson);
        var service = CreateService("gemini-test-key-def456", handler.Object);

        var result = await service.GenerateEmbeddingAsync(
            "Property damage assessment for commercial building following Category 3 hurricane impact.");

        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Dimension);
        Assert.Equal(0.34f, result.Embedding[0]);
        Assert.Equal(-0.67f, result.Embedding[1]);
        Assert.Equal(-0.23f, result.Embedding[4]);
        Assert.Equal("GeminiEmbed", result.Provider);
    }

    [Fact]
    public async Task GenerateEmbeddings_Batch_Parses()
    {
        // Gemini batch embedContent uses a different response format
        var responseJson = """
        {
            "embeddings": [
                { "values": [0.1, 0.2, 0.3] },
                { "values": [0.4, 0.5, 0.6] },
                { "values": [0.7, 0.8, 0.9] }
            ]
        }
        """;

        var handler = CreateMockHandler(HttpStatusCode.OK, responseJson);
        var service = CreateService("gemini-test-key-def456", handler.Object);

        var result = await service.GenerateBatchEmbeddingsAsync(new[]
        {
            "Roof replacement cost estimate following hail damage inspection by certified adjuster.",
            "Business income loss calculation for commercial property during restoration period.",
            "Flood zone determination and National Flood Insurance Program eligibility assessment."
        }, "document");

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Count);
        Assert.Equal(3, result.Dimension);
        Assert.Equal(0.4f, result.Embeddings[1][0]);
        Assert.Equal(0.9f, result.Embeddings[2][2]);
    }

    [Fact]
    public async Task Returns_Empty_On_429_HttpError()
    {
        var handler = CreateMockHandler(HttpStatusCode.TooManyRequests,
            """{"error":{"code":429,"message":"Resource has been exhausted (e.g. check quota)."}}""");
        var service = CreateService("gemini-test-key-def456", handler.Object);

        var result = await service.GenerateEmbeddingAsync(
            "Property damage assessment for commercial building following Category 3 hurricane impact.");

        Assert.False(result.IsSuccess);
        Assert.Contains("TooManyRequests", result.ErrorMessage);
        Assert.Equal("GeminiEmbed", result.Provider);
    }

    [Fact]
    public async Task Returns_Empty_On_500_HttpError()
    {
        var handler = CreateMockHandler(HttpStatusCode.InternalServerError,
            """{"error":{"code":500,"message":"An internal error has occurred."}}""");
        var service = CreateService("gemini-test-key-def456", handler.Object);

        var result = await service.GenerateEmbeddingAsync(
            "Structural engineering report for wind damage to commercial warehouse roof system.");

        Assert.False(result.IsSuccess);
        Assert.Contains("InternalServerError", result.ErrorMessage);
        Assert.Equal("GeminiEmbed", result.Provider);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WithEmptyText_ReturnsError()
    {
        var handler = CreateMockHandler(HttpStatusCode.OK, "{}");
        var service = CreateService("gemini-test-key-def456", handler.Object);

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

        var service = CreateService("gemini-test-key-def456", handlerMock.Object);
        var result = await service.GenerateEmbeddingAsync(
            "Fire damage restoration estimate for multi-story commercial office building.");

        Assert.False(result.IsSuccess);
        Assert.Contains("Connection refused", result.ErrorMessage);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_RecordsElapsedTime()
    {
        var responseJson = """
        {
            "embedding": {
                "values": [0.1]
            }
        }
        """;

        var handler = CreateMockHandler(HttpStatusCode.OK, responseJson);
        var service = CreateService("gemini-test-key-def456", handler.Object);

        var result = await service.GenerateEmbeddingAsync(
            "Catastrophe modeling for hurricane wind speed and property damage correlation.");

        Assert.True(result.IsSuccess);
        Assert.True(result.ElapsedMilliseconds >= 0);
    }

    [Fact]
    public void ProviderName_ReturnsGemini()
    {
        var handler = CreateMockHandler(HttpStatusCode.OK, "{}");
        var service = CreateService("gemini-test-key-def456", handler.Object);

        Assert.Equal("GeminiEmbed", service.ProviderName);
    }

    [Fact]
    public void EmbeddingDimension_Returns768()
    {
        var handler = CreateMockHandler(HttpStatusCode.OK, "{}");
        var service = CreateService("gemini-test-key-def456", handler.Object);

        Assert.Equal(768, service.EmbeddingDimension);
    }

    [Fact]
    public async Task GenerateBatchEmbeddingsAsync_WithEmptyArray_ReturnsError()
    {
        var handler = CreateMockHandler(HttpStatusCode.OK, "{}");
        var service = CreateService("gemini-test-key-def456", handler.Object);

        var result = await service.GenerateBatchEmbeddingsAsync([]);

        Assert.False(result.IsSuccess);
        Assert.Contains("empty", result.ErrorMessage);
    }

    [Fact]
    public async Task GenerateBatchEmbeddingsAsync_ExceedsMaxBatchSize_ReturnsError()
    {
        var handler = CreateMockHandler(HttpStatusCode.OK, "{}");
        var service = CreateService("gemini-test-key-def456", handler.Object);

        // Generate 101 texts to exceed MaxBatchSize of 100
        var texts = Enumerable.Range(1, 101)
            .Select(i => $"Commercial property claim #{i}: water damage assessment for insured premises at policy location.")
            .ToArray();

        var result = await service.GenerateBatchEmbeddingsAsync(texts);

        Assert.False(result.IsSuccess);
        Assert.Contains("exceeds maximum", result.ErrorMessage);
        Assert.Equal("GeminiEmbed", result.Provider);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_IncludesApiKeyInUrl()
    {
        var responseJson = """
        {
            "embedding": {
                "values": [0.1]
            }
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

        var service = CreateService("gemini-secret-key", handlerMock.Object);
        await service.GenerateEmbeddingAsync("Insurance property valuation report.");

        Assert.NotNull(capturedRequest);
        // Gemini uses API key in query string, not Authorization header
        Assert.Contains("key=gemini-secret-key", capturedRequest!.RequestUri?.ToString() ?? "");
    }
}
