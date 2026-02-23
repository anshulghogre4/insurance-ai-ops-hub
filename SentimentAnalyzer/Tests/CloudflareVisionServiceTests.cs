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
/// Tests for CloudflareVisionService.
/// Uses mocked HttpMessageHandler to simulate Cloudflare Workers AI responses.
/// </summary>
public class CloudflareVisionServiceTests
{
    private readonly Mock<ILogger<CloudflareVisionService>> _loggerMock = new();

    private CloudflareVisionService CreateService(string apiKey, string accountId, HttpMessageHandler handler)
    {
        var settings = new AgentSystemSettings
        {
            Cloudflare = new CloudflareSettings { ApiKey = apiKey, AccountId = accountId }
        };
        var optionsMock = new Mock<IOptions<AgentSystemSettings>>();
        optionsMock.Setup(o => o.Value).Returns(settings);

        var httpClient = new HttpClient(handler);
        return new CloudflareVisionService(httpClient, optionsMock.Object, _loggerMock.Object);
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
    public async Task AnalyzeImageAsync_WithValidImage_ReturnsDescription()
    {
        var responseJson = """
        {
            "result": {
                "response": "The image shows significant water damage to a residential basement. There are visible flood marks on the walls, with water staining reaching approximately 3 feet high. Mold growth is visible in the corners."
            },
            "success": true
        }
        """;

        var handler = CreateMockHandler(HttpStatusCode.OK, responseJson);
        var service = CreateService("cf_test_key_123", "test_account_id", handler.Object);

        var result = await service.AnalyzeImageAsync(new byte[] { 1, 2, 3 });

        Assert.True(result.IsSuccess);
        Assert.Equal("CloudflareVision", result.Provider);
        Assert.Contains("water damage", result.Description);
    }

    [Fact]
    public async Task AnalyzeImageAsync_ExtractsDamageIndicators()
    {
        var responseJson = """
        {
            "result": {
                "response": "The image shows fire damage with visible smoke damage on the ceiling. There is structural damage to the walls and evidence of a water leak from firefighting efforts."
            },
            "success": true
        }
        """;

        var handler = CreateMockHandler(HttpStatusCode.OK, responseJson);
        var service = CreateService("cf_test_key_123", "test_account_id", handler.Object);

        var result = await service.AnalyzeImageAsync(new byte[] { 1, 2, 3 });

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.DamageIndicators);
        Assert.Contains("Fire Damage", result.DamageIndicators);
        Assert.Contains("Smoke Damage", result.DamageIndicators);
        Assert.Contains("Structural Damage", result.DamageIndicators);
        Assert.Contains("Water Leak", result.DamageIndicators);
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
    public async Task AnalyzeImageAsync_WithMissingAccountId_ReturnsError()
    {
        var handler = CreateMockHandler(HttpStatusCode.OK, "{}");
        var service = CreateService("cf_test_key_123", "", handler.Object);

        var result = await service.AnalyzeImageAsync(new byte[] { 1, 2, 3 });

        Assert.False(result.IsSuccess);
        Assert.Contains("not configured", result.ErrorMessage);
    }

    [Fact]
    public async Task AnalyzeImageAsync_WithApiError_ReturnsError()
    {
        var handler = CreateMockHandler(HttpStatusCode.InternalServerError, """{"error":"Model overloaded"}""");
        var service = CreateService("cf_test_key_123", "test_account_id", handler.Object);

        var result = await service.AnalyzeImageAsync(new byte[] { 1, 2, 3 });

        Assert.False(result.IsSuccess);
        Assert.Contains("InternalServerError", result.ErrorMessage);
    }

    [Fact]
    public async Task AnalyzeImageAsync_WithNoDescription_ReturnsEmptyDescription()
    {
        var responseJson = """
        {
            "result": {},
            "success": true
        }
        """;

        var handler = CreateMockHandler(HttpStatusCode.OK, responseJson);
        var service = CreateService("cf_test_key_123", "test_account_id", handler.Object);

        var result = await service.AnalyzeImageAsync(new byte[] { 1, 2, 3 });

        Assert.True(result.IsSuccess);
        Assert.Equal(string.Empty, result.Description);
        Assert.Empty(result.DamageIndicators);
    }
}
