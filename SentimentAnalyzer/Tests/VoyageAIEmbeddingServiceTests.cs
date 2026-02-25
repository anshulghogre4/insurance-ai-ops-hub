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
/// Tests for VoyageAIEmbeddingService.
/// Uses mocked HttpMessageHandler to simulate Voyage AI API responses.
/// </summary>
public class VoyageAIEmbeddingServiceTests
{
    private readonly Mock<ILogger<VoyageAIEmbeddingService>> _loggerMock = new();

    private VoyageAIEmbeddingService CreateService(
        string apiKey,
        HttpMessageHandler handler,
        string model = "voyage-finance-2",
        string endpoint = "https://api.voyageai.com/v1")
    {
        var settings = new AgentSystemSettings
        {
            Voyage = new VoyageSettings
            {
                ApiKey = apiKey,
                Model = model,
                Endpoint = endpoint
            }
        };
        var optionsMock = new Mock<IOptions<AgentSystemSettings>>();
        optionsMock.Setup(o => o.Value).Returns(settings);

        var httpClient = new HttpClient(handler);
        return new VoyageAIEmbeddingService(httpClient, optionsMock.Object, _loggerMock.Object);
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
    public async Task GenerateEmbeddingAsync_WithValidResponse_ReturnsEmbedding()
    {
        var responseJson = """
        {
            "object": "list",
            "data": [
                {
                    "object": "embedding",
                    "embedding": [0.1, 0.2, 0.3, -0.4, 0.5],
                    "index": 0
                }
            ],
            "model": "voyage-finance-2",
            "usage": { "total_tokens": 15 }
        }
        """;

        var handler = CreateMockHandler(HttpStatusCode.OK, responseJson);
        var service = CreateService("voyage-test-key-123", handler.Object);

        var result = await service.GenerateEmbeddingAsync(
            "Filed a water damage claim under homeowner's policy. Adjuster estimated $12,000 in repairs.");

        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Dimension);
        Assert.Equal(0.1f, result.Embedding[0]);
        Assert.Equal(-0.4f, result.Embedding[3]);
        Assert.Equal(15, result.TokensUsed);
        Assert.Equal("VoyageAI", result.Provider);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WithDocumentInputType_SendsInputTypeInRequest()
    {
        var responseJson = """
        {
            "object": "list",
            "data": [{ "object": "embedding", "embedding": [0.1], "index": 0 }],
            "model": "voyage-finance-2",
            "usage": { "total_tokens": 5 }
        }
        """;

        string? capturedBody = null;
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>(async (req, _) =>
            {
                capturedBody = await req.Content!.ReadAsStringAsync();
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
                };
            });

        var service = CreateService("voyage-test-key-123", handlerMock.Object);
        await service.GenerateEmbeddingAsync("Insurance policy DECLARATIONS section text.", "document");

        Assert.NotNull(capturedBody);
        Assert.Contains("\"input_type\":\"document\"", capturedBody);
        Assert.Contains("voyage-finance-2", capturedBody);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WithMissingApiKey_ReturnsError()
    {
        var handler = CreateMockHandler(HttpStatusCode.OK, "{}");
        var service = CreateService("", handler.Object);

        var result = await service.GenerateEmbeddingAsync(
            "Policy renewal notice for commercial general liability coverage.");

        Assert.False(result.IsSuccess);
        Assert.Contains("not configured", result.ErrorMessage);
        Assert.Equal("VoyageAI", result.Provider);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WithEmptyText_ReturnsError()
    {
        var handler = CreateMockHandler(HttpStatusCode.OK, "{}");
        var service = CreateService("voyage-test-key-123", handler.Object);

        var result = await service.GenerateEmbeddingAsync("");

        Assert.False(result.IsSuccess);
        Assert.Contains("empty", result.ErrorMessage);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_With429RateLimit_ReturnsError()
    {
        var handler = CreateMockHandler(HttpStatusCode.TooManyRequests,
            """{"detail":"Rate limit exceeded. Free tier: 50M tokens for voyage-finance-2."}""");
        var service = CreateService("voyage-test-key-123", handler.Object);

        var result = await service.GenerateEmbeddingAsync(
            "Claim denied due to policy exclusion for pre-existing conditions.");

        Assert.False(result.IsSuccess);
        Assert.Contains("TooManyRequests", result.ErrorMessage);
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

        var service = CreateService("voyage-test-key-123", handlerMock.Object);
        var result = await service.GenerateEmbeddingAsync("Insurance claim text for analysis.");

        Assert.False(result.IsSuccess);
        Assert.Contains("Connection refused", result.ErrorMessage);
    }

    [Fact]
    public async Task GenerateBatchEmbeddingsAsync_WithValidResponse_ReturnsBatchEmbeddings()
    {
        var responseJson = """
        {
            "object": "list",
            "data": [
                { "object": "embedding", "embedding": [0.1, 0.2, 0.3], "index": 0 },
                { "object": "embedding", "embedding": [0.4, 0.5, 0.6], "index": 1 },
                { "object": "embedding", "embedding": [0.7, 0.8, 0.9], "index": 2 }
            ],
            "model": "voyage-finance-2",
            "usage": { "total_tokens": 45 }
        }
        """;

        var handler = CreateMockHandler(HttpStatusCode.OK, responseJson);
        var service = CreateService("voyage-test-key-123", handler.Object);

        var result = await service.GenerateBatchEmbeddingsAsync(new[]
        {
            "DECLARATIONS: Named insured, policy period, coverage limits.",
            "EXCLUSIONS: Flood damage, earthquake, acts of war.",
            "CONDITIONS: Duties after loss, subrogation, other insurance."
        }, "document");

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Count);
        Assert.Equal(3, result.Dimension);
        Assert.Equal(45, result.TotalTokensUsed);
        Assert.Equal(0.4f, result.Embeddings[1][0]);
        Assert.Equal(0.9f, result.Embeddings[2][2]);
    }

    [Fact]
    public async Task GenerateBatchEmbeddingsAsync_WithEmptyArray_ReturnsError()
    {
        var handler = CreateMockHandler(HttpStatusCode.OK, "{}");
        var service = CreateService("voyage-test-key-123", handler.Object);

        var result = await service.GenerateBatchEmbeddingsAsync([]);

        Assert.False(result.IsSuccess);
        Assert.Contains("empty", result.ErrorMessage);
    }

    [Fact]
    public async Task GenerateBatchEmbeddingsAsync_ExceedsMaxBatchSize_ReturnsError()
    {
        var handler = CreateMockHandler(HttpStatusCode.OK, "{}");
        var service = CreateService("voyage-test-key-123", handler.Object);

        var texts = Enumerable.Range(0, 200).Select(i => $"Insurance claim text {i}").ToArray();
        var result = await service.GenerateBatchEmbeddingsAsync(texts);

        Assert.False(result.IsSuccess);
        Assert.Contains("exceeds maximum", result.ErrorMessage);
    }

    [Fact]
    public void EmbeddingDimension_Returns1024()
    {
        var handler = CreateMockHandler(HttpStatusCode.OK, "{}");
        var service = CreateService("voyage-test-key-123", handler.Object);

        Assert.Equal(1024, service.EmbeddingDimension);
    }

    [Fact]
    public void ProviderName_ReturnsVoyageAI()
    {
        var handler = CreateMockHandler(HttpStatusCode.OK, "{}");
        var service = CreateService("voyage-test-key-123", handler.Object);

        Assert.Equal("VoyageAI", service.ProviderName);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_RecordsElapsedTime()
    {
        var responseJson = """
        {
            "object": "list",
            "data": [{ "object": "embedding", "embedding": [0.1], "index": 0 }],
            "model": "voyage-finance-2",
            "usage": { "total_tokens": 5 }
        }
        """;

        var handler = CreateMockHandler(HttpStatusCode.OK, responseJson);
        var service = CreateService("voyage-test-key-123", handler.Object);

        var result = await service.GenerateEmbeddingAsync("Policy endorsement for additional living expenses coverage.");

        Assert.True(result.IsSuccess);
        Assert.True(result.ElapsedMilliseconds >= 0);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_With500ServerError_ReturnsError()
    {
        var handler = CreateMockHandler(HttpStatusCode.InternalServerError,
            """{"detail":"Internal server error"}""");
        var service = CreateService("voyage-test-key-123", handler.Object);

        var result = await service.GenerateEmbeddingAsync(
            "Claim adjuster notes: significant structural damage to roof after hailstorm.");

        Assert.False(result.IsSuccess);
        Assert.Contains("InternalServerError", result.ErrorMessage);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_SendsAuthorizationHeader()
    {
        var responseJson = """
        {
            "object": "list",
            "data": [{ "object": "embedding", "embedding": [0.1], "index": 0 }],
            "model": "voyage-finance-2",
            "usage": { "total_tokens": 3 }
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

        var service = CreateService("voyage-secret-api-key", handlerMock.Object);
        await service.GenerateEmbeddingAsync("Insurance premium calculation.");

        Assert.NotNull(capturedRequest);
        Assert.Equal("Bearer", capturedRequest!.Headers.Authorization?.Scheme);
        Assert.Equal("voyage-secret-api-key", capturedRequest.Headers.Authorization?.Parameter);
    }
}
