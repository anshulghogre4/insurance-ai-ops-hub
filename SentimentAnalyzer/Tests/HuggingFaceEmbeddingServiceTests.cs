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
/// Tests for HuggingFaceEmbeddingService.
/// Uses mocked HttpMessageHandler to simulate HuggingFace Inference API responses.
/// HuggingFace BAAI/bge-large-en-v1.5: 1024-dim, 300 req/hr free tier, shares quota with NER.
/// Response format is a direct nested array: [[0.1, 0.2, ...]]
/// </summary>
public class HuggingFaceEmbeddingServiceTests
{
    private readonly Mock<ILogger<HuggingFaceEmbeddingService>> _loggerMock = new();

    private HuggingFaceEmbeddingService CreateService(
        string apiKey,
        HttpMessageHandler handler,
        string model = "BAAI/bge-large-en-v1.5")
    {
        var settings = new AgentSystemSettings
        {
            HuggingFaceEmbedding = new HuggingFaceEmbeddingSettings
            {
                ApiKey = apiKey,
                Model = model
            }
        };
        var optionsMock = new Mock<IOptions<AgentSystemSettings>>();
        optionsMock.Setup(o => o.Value).Returns(settings);

        var httpClient = new HttpClient(handler);
        return new HuggingFaceEmbeddingService(httpClient, optionsMock.Object, _loggerMock.Object);
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
            "Automobile collision claim with suspected fraud indicators including inconsistent damage patterns.");

        Assert.False(result.IsSuccess);
        Assert.Contains("not configured", result.ErrorMessage);
        Assert.Equal("HuggingFaceEmbed", result.Provider);
    }

    [Fact]
    public async Task GenerateEmbedding_Parses_Direct_Array()
    {
        // HuggingFace Inference API returns a direct nested array: [[float, float, ...]]
        var responseJson = "[[0.56, -0.34, 0.78, 0.11, -0.92]]";

        var handler = CreateMockHandler(HttpStatusCode.OK, responseJson);
        var service = CreateService("hf-test-key-ghi012", handler.Object);

        var result = await service.GenerateEmbeddingAsync(
            "Automobile collision claim with suspected fraud indicators including inconsistent damage patterns.");

        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Dimension);
        Assert.Equal(0.56f, result.Embedding[0]);
        Assert.Equal(-0.34f, result.Embedding[1]);
        Assert.Equal(-0.92f, result.Embedding[4]);
        Assert.Equal("HuggingFaceEmbed", result.Provider);
    }

    [Fact]
    public async Task GenerateEmbeddings_Batch_Parses()
    {
        // HuggingFace batch returns a nested array with one embedding per input
        var responseJson = """
        [
            [0.1, 0.2, 0.3],
            [0.4, 0.5, 0.6],
            [0.7, 0.8, 0.9]
        ]
        """;

        var handler = CreateMockHandler(HttpStatusCode.OK, responseJson);
        var service = CreateService("hf-test-key-ghi012", handler.Object);

        var result = await service.GenerateBatchEmbeddingsAsync(new[]
        {
            "Staged automobile accident investigation with multiple claimants at same intersection.",
            "Vehicle damage inconsistency analysis comparing reported impact versus physical evidence.",
            "Prior claim history review showing pattern of repeated soft tissue injury claims."
        }, "document");

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Count);
        Assert.Equal(3, result.Dimension);
        Assert.Equal(0.4f, result.Embeddings[1][0]);
        Assert.Equal(0.9f, result.Embeddings[2][2]);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_2DResponse_MeanPoolsToSingleEmbedding()
    {
        // BGE model returns per-token embeddings as 2D array [[token1_emb], [token2_emb], ...]
        // Mean pooling averages across tokens: (token1 + token2 + token3) / 3
        // Token 1: [0.3, 0.6, 0.9]
        // Token 2: [0.6, 0.3, 0.0]
        // Token 3: [0.9, 0.9, 0.3]
        // Mean:    [0.6, 0.6, 0.4]
        var responseJson = """
        [
            [0.3, 0.6, 0.9],
            [0.6, 0.3, 0.0],
            [0.9, 0.9, 0.3]
        ]
        """;

        var handler = CreateMockHandler(HttpStatusCode.OK, responseJson);
        var service = CreateService("hf-test-key-ghi012", handler.Object);

        var result = await service.GenerateEmbeddingAsync(
            "Workers compensation claim for repetitive strain injury in warehouse loading dock operations.");

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Dimension);
        Assert.Equal("HuggingFaceEmbed", result.Provider);

        // Verify mean pooling: (0.3+0.6+0.9)/3 = 0.6, (0.6+0.3+0.9)/3 = 0.6, (0.9+0.0+0.3)/3 = 0.4
        Assert.Equal(0.6f, result.Embedding[0], 0.01f);
        Assert.Equal(0.6f, result.Embedding[1], 0.01f);
        Assert.Equal(0.4f, result.Embedding[2], 0.01f);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_1DResponse_ReturnsDirectly()
    {
        // Sentence-transformers models return a flat 1D array: [0.1, 0.2, ...]
        var responseJson = "[0.45, -0.67, 0.23, 0.81]";

        var handler = CreateMockHandler(HttpStatusCode.OK, responseJson);
        var service = CreateService("hf-test-key-ghi012", handler.Object);

        var result = await service.GenerateEmbeddingAsync(
            "Liability coverage limits analysis for commercial fleet auto insurance policy.");

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Dimension);
        Assert.Equal(0.45f, result.Embedding[0]);
        Assert.Equal(-0.67f, result.Embedding[1]);
        Assert.Equal(0.23f, result.Embedding[2]);
        Assert.Equal(0.81f, result.Embedding[3]);
        Assert.Equal("HuggingFaceEmbed", result.Provider);
    }

    [Fact]
    public async Task GenerateBatchEmbeddingsAsync_3DResponse_MeanPoolsEachText()
    {
        // Batch with BGE model: 3D array [batch][tokens][dim]
        // Text 1 has 2 tokens: [[0.2, 0.4], [0.6, 0.8]] -> mean: [0.4, 0.6]
        // Text 2 has 2 tokens: [[0.1, 0.3], [0.5, 0.7]] -> mean: [0.3, 0.5]
        var responseJson = """
        [
            [[0.2, 0.4], [0.6, 0.8]],
            [[0.1, 0.3], [0.5, 0.7]]
        ]
        """;

        var handler = CreateMockHandler(HttpStatusCode.OK, responseJson);
        var service = CreateService("hf-test-key-ghi012", handler.Object);

        var result = await service.GenerateBatchEmbeddingsAsync(new[]
        {
            "Subrogation recovery analysis for at-fault driver liability in multi-vehicle collision.",
            "Personal injury protection benefit calculation for policyholder medical expenses."
        }, "document");

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Count);
        Assert.Equal(2, result.Dimension);
        Assert.Equal("HuggingFaceEmbed", result.Provider);

        // Text 1 mean pooling: (0.2+0.6)/2=0.4, (0.4+0.8)/2=0.6
        Assert.Equal(0.4f, result.Embeddings[0][0], 0.01f);
        Assert.Equal(0.6f, result.Embeddings[0][1], 0.01f);

        // Text 2 mean pooling: (0.1+0.5)/2=0.3, (0.3+0.7)/2=0.5
        Assert.Equal(0.3f, result.Embeddings[1][0], 0.01f);
        Assert.Equal(0.5f, result.Embeddings[1][1], 0.01f);
    }

    [Fact]
    public async Task Returns_Empty_On_429_HttpError()
    {
        var handler = CreateMockHandler(HttpStatusCode.TooManyRequests,
            """{"error":"Rate limit reached. Free tier: 300 requests/hour."}""");
        var service = CreateService("hf-test-key-ghi012", handler.Object);

        var result = await service.GenerateEmbeddingAsync(
            "Automobile collision claim with suspected fraud indicators including inconsistent damage patterns.");

        Assert.False(result.IsSuccess);
        Assert.Contains("TooManyRequests", result.ErrorMessage);
        Assert.Equal("HuggingFaceEmbed", result.Provider);
    }

    [Fact]
    public async Task Returns_Empty_On_500_HttpError()
    {
        var handler = CreateMockHandler(HttpStatusCode.InternalServerError,
            """{"error":"Model BAAI/bge-large-en-v1.5 is currently loading"}""");
        var service = CreateService("hf-test-key-ghi012", handler.Object);

        var result = await service.GenerateEmbeddingAsync(
            "Total loss determination for vehicle with salvage value calculation and GAP coverage analysis.");

        Assert.False(result.IsSuccess);
        Assert.Contains("InternalServerError", result.ErrorMessage);
        Assert.Equal("HuggingFaceEmbed", result.Provider);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WithEmptyText_ReturnsError()
    {
        var handler = CreateMockHandler(HttpStatusCode.OK, "{}");
        var service = CreateService("hf-test-key-ghi012", handler.Object);

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

        var service = CreateService("hf-test-key-ghi012", handlerMock.Object);
        var result = await service.GenerateEmbeddingAsync(
            "Uninsured motorist claim with hit-and-run investigation report from law enforcement.");

        Assert.False(result.IsSuccess);
        Assert.Contains("Connection refused", result.ErrorMessage);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_SendsAuthorizationHeader()
    {
        var responseJson = "[[0.1]]";

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

        var service = CreateService("hf-secret-api-key", handlerMock.Object);
        await service.GenerateEmbeddingAsync("Diminished value claim for post-accident vehicle depreciation.");

        Assert.NotNull(capturedRequest);
        Assert.Equal("Bearer", capturedRequest!.Headers.Authorization?.Scheme);
        Assert.Equal("hf-secret-api-key", capturedRequest.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_RecordsElapsedTime()
    {
        var responseJson = "[[0.1]]";

        var handler = CreateMockHandler(HttpStatusCode.OK, responseJson);
        var service = CreateService("hf-test-key-ghi012", handler.Object);

        var result = await service.GenerateEmbeddingAsync(
            "Comparative negligence assessment for multi-vehicle intersection collision.");

        Assert.True(result.IsSuccess);
        Assert.True(result.ElapsedMilliseconds >= 0);
    }

    [Fact]
    public void ProviderName_ReturnsHuggingFace()
    {
        var handler = CreateMockHandler(HttpStatusCode.OK, "{}");
        var service = CreateService("hf-test-key-ghi012", handler.Object);

        Assert.Equal("HuggingFaceEmbed", service.ProviderName);
    }

    [Fact]
    public void EmbeddingDimension_Returns1024()
    {
        var handler = CreateMockHandler(HttpStatusCode.OK, "{}");
        var service = CreateService("hf-test-key-ghi012", handler.Object);

        Assert.Equal(1024, service.EmbeddingDimension);
    }

    [Fact]
    public async Task GenerateBatchEmbeddingsAsync_WithEmptyArray_ReturnsError()
    {
        var handler = CreateMockHandler(HttpStatusCode.OK, "{}");
        var service = CreateService("hf-test-key-ghi012", handler.Object);

        var result = await service.GenerateBatchEmbeddingsAsync([]);

        Assert.False(result.IsSuccess);
        Assert.Contains("empty", result.ErrorMessage);
    }

    [Fact]
    public async Task Returns_Empty_On_503_ModelLoading()
    {
        var handler = CreateMockHandler(HttpStatusCode.ServiceUnavailable,
            """{"error":"Model BAAI/bge-large-en-v1.5 is currently loading","estimated_time":20}""");
        var service = CreateService("hf-test-key-ghi012", handler.Object);

        var result = await service.GenerateEmbeddingAsync(
            "Medical payments coverage for passenger injuries in rear-end automobile collision.");

        Assert.False(result.IsSuccess);
        Assert.Contains("ServiceUnavailable", result.ErrorMessage);
        Assert.Equal("HuggingFaceEmbed", result.Provider);
    }
}
