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
/// Tests for OllamaEmbeddingService.
/// Uses mocked HttpMessageHandler to simulate Ollama /api/embed responses.
/// </summary>
public class OllamaEmbeddingServiceTests
{
    private readonly Mock<ILogger<OllamaEmbeddingService>> _loggerMock = new();

    private OllamaEmbeddingService CreateService(
        HttpMessageHandler handler,
        string endpoint = "http://localhost:11434")
    {
        var settings = new AgentSystemSettings
        {
            Ollama = new OllamaSettings
            {
                Endpoint = endpoint,
                Model = "llama3.2"
            }
        };
        var optionsMock = new Mock<IOptions<AgentSystemSettings>>();
        optionsMock.Setup(o => o.Value).Returns(settings);

        var httpClient = new HttpClient(handler);
        return new OllamaEmbeddingService(httpClient, optionsMock.Object, _loggerMock.Object);
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
            "model": "mxbai-embed-large",
            "embeddings": [[0.15, -0.23, 0.87, 0.42, -0.11]]
        }
        """;

        var handler = CreateMockHandler(HttpStatusCode.OK, responseJson);
        var service = CreateService(handler.Object);

        var result = await service.GenerateEmbeddingAsync(
            "Adjuster inspected property damage from recent flooding event. Estimated repair cost: $25,000.");

        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Dimension);
        Assert.Equal(0.15f, result.Embedding[0]);
        Assert.Equal(-0.23f, result.Embedding[1]);
        Assert.Equal(0, result.TokensUsed); // Ollama does not report tokens
        Assert.Equal("Ollama", result.Provider);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WithEmptyText_ReturnsError()
    {
        var handler = CreateMockHandler(HttpStatusCode.OK, "{}");
        var service = CreateService(handler.Object);

        var result = await service.GenerateEmbeddingAsync("");

        Assert.False(result.IsSuccess);
        Assert.Contains("empty", result.ErrorMessage);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_ModelNotFound_ReturnsHelpfulError()
    {
        var handler = CreateMockHandler(HttpStatusCode.NotFound,
            """{"error":"model 'mxbai-embed-large' not found, try pulling it first"}""");
        var service = CreateService(handler.Object);

        var result = await service.GenerateEmbeddingAsync(
            "Policyholder reported vehicle collision on Highway 101.");

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.ErrorMessage);
        Assert.Contains("ollama pull", result.ErrorMessage);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_ServerError_ReturnsError()
    {
        var handler = CreateMockHandler(HttpStatusCode.InternalServerError,
            """{"error":"internal server error"}""");
        var service = CreateService(handler.Object);

        var result = await service.GenerateEmbeddingAsync(
            "Workers compensation claim for on-site injury at construction facility.");

        Assert.False(result.IsSuccess);
        Assert.Contains("InternalServerError", result.ErrorMessage);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_SendsCorrectRequestFormat()
    {
        var responseJson = """
        {
            "model": "mxbai-embed-large",
            "embeddings": [[0.1]]
        }
        """;

        string? capturedBody = null;
        Uri? capturedUri = null;
        HttpMethod? capturedMethod = null;
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>(async (req, _) =>
            {
                capturedBody = await req.Content!.ReadAsStringAsync();
                capturedUri = req.RequestUri;
                capturedMethod = req.Method;
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
                };
            });

        var service = CreateService(handlerMock.Object);
        await service.GenerateEmbeddingAsync("Professional liability coverage endorsement.");

        Assert.Equal(HttpMethod.Post, capturedMethod);
        Assert.Contains("/api/embed", capturedUri!.ToString());
        Assert.NotNull(capturedBody);
        Assert.Contains("mxbai-embed-large", capturedBody);
        Assert.Contains("truncate", capturedBody);
    }

    [Fact]
    public async Task GenerateBatchEmbeddingsAsync_WithValidResponse_ReturnsBatch()
    {
        var responseJson = """
        {
            "model": "mxbai-embed-large",
            "embeddings": [
                [0.1, 0.2, 0.3],
                [0.4, 0.5, 0.6]
            ]
        }
        """;

        var handler = CreateMockHandler(HttpStatusCode.OK, responseJson);
        var service = CreateService(handler.Object);

        var result = await service.GenerateBatchEmbeddingsAsync(new[]
        {
            "Named peril homeowner's insurance policy - fire, theft, vandalism coverage.",
            "Commercial auto fleet insurance with hired and non-owned vehicle endorsement."
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Count);
        Assert.Equal(3, result.Dimension);
        Assert.Equal(0.4f, result.Embeddings[1][0]);
    }

    [Fact]
    public async Task GenerateBatchEmbeddingsAsync_WithEmptyArray_ReturnsError()
    {
        var handler = CreateMockHandler(HttpStatusCode.OK, "{}");
        var service = CreateService(handler.Object);

        var result = await service.GenerateBatchEmbeddingsAsync([]);

        Assert.False(result.IsSuccess);
        Assert.Contains("empty", result.ErrorMessage);
    }

    [Fact]
    public async Task GenerateBatchEmbeddingsAsync_ExceedsBatchLimit_ReturnsError()
    {
        var handler = CreateMockHandler(HttpStatusCode.OK, "{}");
        var service = CreateService(handler.Object);

        var texts = Enumerable.Range(0, 200).Select(i => $"Claim description #{i}").ToArray();
        var result = await service.GenerateBatchEmbeddingsAsync(texts);

        Assert.False(result.IsSuccess);
        Assert.Contains("exceeds maximum", result.ErrorMessage);
    }

    [Fact]
    public void EmbeddingDimension_Returns1024()
    {
        var handler = CreateMockHandler(HttpStatusCode.OK, "{}");
        var service = CreateService(handler.Object);

        Assert.Equal(1024, service.EmbeddingDimension);
    }

    [Fact]
    public void ProviderName_ReturnsOllama()
    {
        var handler = CreateMockHandler(HttpStatusCode.OK, "{}");
        var service = CreateService(handler.Object);

        Assert.Equal("Ollama", service.ProviderName);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_RecordsElapsedTime()
    {
        var responseJson = """
        {
            "model": "mxbai-embed-large",
            "embeddings": [[0.1, 0.2]]
        }
        """;

        var handler = CreateMockHandler(HttpStatusCode.OK, responseJson);
        var service = CreateService(handler.Object);

        var result = await service.GenerateEmbeddingAsync(
            "Umbrella liability policy providing excess coverage over primary CGL and auto policies.");

        Assert.True(result.IsSuccess);
        Assert.True(result.ElapsedMilliseconds >= 0);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WithEmptyEmbeddingsArray_ReturnsError()
    {
        var responseJson = """
        {
            "model": "mxbai-embed-large",
            "embeddings": []
        }
        """;

        var handler = CreateMockHandler(HttpStatusCode.OK, responseJson);
        var service = CreateService(handler.Object);

        var result = await service.GenerateEmbeddingAsync(
            "Subrogation rights after collision with uninsured motorist.");

        Assert.False(result.IsSuccess);
        Assert.Contains("empty", result.ErrorMessage);
    }
}
