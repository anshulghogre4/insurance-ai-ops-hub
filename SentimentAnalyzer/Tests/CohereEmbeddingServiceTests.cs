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
/// Tests for CohereEmbeddingService.
/// Uses mocked HttpMessageHandler to simulate Cohere Embed API v2 responses.
/// Cohere embed-multilingual-v3.0: 1024-dim, 100 req/min free trial, no credit card required.
/// </summary>
public class CohereEmbeddingServiceTests
{
    private readonly Mock<ILogger<CohereEmbeddingService>> _loggerMock = new();

    private CohereEmbeddingService CreateService(
        string apiKey,
        HttpMessageHandler handler,
        string model = "embed-multilingual-v3.0",
        string endpoint = "https://api.cohere.com/v2")
    {
        var settings = new AgentSystemSettings
        {
            CohereEmbedding = new CohereEmbeddingSettings
            {
                ApiKey = apiKey,
                Model = model,
                Endpoint = endpoint
            }
        };
        var optionsMock = new Mock<IOptions<AgentSystemSettings>>();
        optionsMock.Setup(o => o.Value).Returns(settings);

        var httpClient = new HttpClient(handler);
        return new CohereEmbeddingService(httpClient, optionsMock.Object, _loggerMock.Object);
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
            "Workers compensation claim filed for repetitive stress injury sustained during warehouse operations.");

        Assert.False(result.IsSuccess);
        Assert.Contains("not configured", result.ErrorMessage);
        Assert.Equal("Cohere", result.Provider);
    }

    [Fact]
    public async Task GenerateEmbedding_Parses_Cohere_Format()
    {
        var responseJson = """
        {
            "id": "emb-abc123",
            "embeddings": {
                "float": [[0.23, -0.45, 0.67, 0.12, -0.89]]
            },
            "texts": ["Workers compensation claim filed for repetitive stress injury sustained during warehouse operations."],
            "meta": {
                "api_version": { "version": "2" },
                "billed_units": { "input_tokens": 18 }
            }
        }
        """;

        var handler = CreateMockHandler(HttpStatusCode.OK, responseJson);
        var service = CreateService("cohere-test-key-xyz789", handler.Object);

        var result = await service.GenerateEmbeddingAsync(
            "Workers compensation claim filed for repetitive stress injury sustained during warehouse operations.");

        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Dimension);
        Assert.Equal(0.23f, result.Embedding[0]);
        Assert.Equal(-0.45f, result.Embedding[1]);
        Assert.Equal(-0.89f, result.Embedding[4]);
        Assert.Equal(18, result.TokensUsed);
        Assert.Equal("Cohere", result.Provider);
    }

    [Fact]
    public async Task GenerateEmbeddings_Batch_Parses()
    {
        var responseJson = """
        {
            "id": "emb-batch123",
            "embeddings": {
                "float": [
                    [0.1, 0.2, 0.3],
                    [0.4, 0.5, 0.6],
                    [0.7, 0.8, 0.9]
                ]
            },
            "texts": [
                "Workers compensation premium calculation for manufacturing sector.",
                "Occupational disease coverage under state workers compensation statute.",
                "Return-to-work program assessment for injured warehouse employee."
            ],
            "meta": {
                "api_version": { "version": "2" },
                "billed_units": { "input_tokens": 42 }
            }
        }
        """;

        var handler = CreateMockHandler(HttpStatusCode.OK, responseJson);
        var service = CreateService("cohere-test-key-xyz789", handler.Object);

        var result = await service.GenerateBatchEmbeddingsAsync(new[]
        {
            "Workers compensation premium calculation for manufacturing sector.",
            "Occupational disease coverage under state workers compensation statute.",
            "Return-to-work program assessment for injured warehouse employee."
        }, "document");

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Count);
        Assert.Equal(3, result.Dimension);
        Assert.Equal(42, result.TotalTokensUsed);
        Assert.Equal(0.4f, result.Embeddings[1][0]);
        Assert.Equal(0.9f, result.Embeddings[2][2]);
    }

    [Fact]
    public async Task Returns_Empty_On_429_HttpError()
    {
        var handler = CreateMockHandler(HttpStatusCode.TooManyRequests,
            """{"message":"rate limit exceeded"}""");
        var service = CreateService("cohere-test-key-xyz789", handler.Object);

        var result = await service.GenerateEmbeddingAsync(
            "Workers compensation claim filed for repetitive stress injury sustained during warehouse operations.");

        Assert.False(result.IsSuccess);
        Assert.Contains("TooManyRequests", result.ErrorMessage);
        Assert.Equal("Cohere", result.Provider);
    }

    [Fact]
    public async Task Returns_Empty_On_500_HttpError()
    {
        var handler = CreateMockHandler(HttpStatusCode.InternalServerError,
            """{"message":"internal server error"}""");
        var service = CreateService("cohere-test-key-xyz789", handler.Object);

        var result = await service.GenerateEmbeddingAsync(
            "Employer liability coverage for workplace safety violations resulting in employee injury.");

        Assert.False(result.IsSuccess);
        Assert.Contains("InternalServerError", result.ErrorMessage);
        Assert.Equal("Cohere", result.Provider);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WithEmptyText_ReturnsError()
    {
        var handler = CreateMockHandler(HttpStatusCode.OK, "{}");
        var service = CreateService("cohere-test-key-xyz789", handler.Object);

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

        var service = CreateService("cohere-test-key-xyz789", handlerMock.Object);
        var result = await service.GenerateEmbeddingAsync(
            "Cumulative injury claim under workers compensation for repetitive motion disorder.");

        Assert.False(result.IsSuccess);
        Assert.Contains("Connection refused", result.ErrorMessage);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_SendsAuthorizationHeader()
    {
        var responseJson = """
        {
            "id": "emb-auth123",
            "embeddings": {
                "float": [[0.1]]
            },
            "texts": ["test"],
            "meta": {
                "api_version": { "version": "2" },
                "billed_units": { "input_tokens": 3 }
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

        var service = CreateService("cohere-secret-api-key", handlerMock.Object);
        await service.GenerateEmbeddingAsync("Independent medical examination report for workers compensation dispute.");

        Assert.NotNull(capturedRequest);
        Assert.Equal("Bearer", capturedRequest!.Headers.Authorization?.Scheme);
        Assert.Equal("cohere-secret-api-key", capturedRequest.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_RecordsElapsedTime()
    {
        var responseJson = """
        {
            "id": "emb-time123",
            "embeddings": {
                "float": [[0.1]]
            },
            "texts": ["test"],
            "meta": {
                "api_version": { "version": "2" },
                "billed_units": { "input_tokens": 5 }
            }
        }
        """;

        var handler = CreateMockHandler(HttpStatusCode.OK, responseJson);
        var service = CreateService("cohere-test-key-xyz789", handler.Object);

        var result = await service.GenerateEmbeddingAsync(
            "Permanent partial disability rating for shoulder injury from workplace accident.");

        Assert.True(result.IsSuccess);
        Assert.True(result.ElapsedMilliseconds >= 0);
    }

    [Fact]
    public void ProviderName_ReturnsCohere()
    {
        var handler = CreateMockHandler(HttpStatusCode.OK, "{}");
        var service = CreateService("cohere-test-key-xyz789", handler.Object);

        Assert.Equal("Cohere", service.ProviderName);
    }

    [Fact]
    public void EmbeddingDimension_Returns1024()
    {
        var handler = CreateMockHandler(HttpStatusCode.OK, "{}");
        var service = CreateService("cohere-test-key-xyz789", handler.Object);

        Assert.Equal(1024, service.EmbeddingDimension);
    }

    [Fact]
    public async Task GenerateBatchEmbeddingsAsync_WithEmptyArray_ReturnsError()
    {
        var handler = CreateMockHandler(HttpStatusCode.OK, "{}");
        var service = CreateService("cohere-test-key-xyz789", handler.Object);

        var result = await service.GenerateBatchEmbeddingsAsync([]);

        Assert.False(result.IsSuccess);
        Assert.Contains("empty", result.ErrorMessage);
    }
}
