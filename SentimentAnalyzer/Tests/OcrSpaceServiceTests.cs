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
/// Tests for OcrSpaceService.
/// Uses mocked HttpMessageHandler to simulate OCR.space API responses.
/// </summary>
public class OcrSpaceServiceTests
{
    private readonly Mock<ILogger<OcrSpaceService>> _loggerMock = new();

    private OcrSpaceService CreateService(string apiKey, HttpMessageHandler handler)
    {
        var settings = new AgentSystemSettings
        {
            OcrSpace = new OcrSpaceSettings { ApiKey = apiKey }
        };
        var optionsMock = new Mock<IOptions<AgentSystemSettings>>();
        optionsMock.Setup(o => o.Value).Returns(settings);

        var httpClient = new HttpClient(handler);
        return new OcrSpaceService(httpClient, optionsMock.Object, _loggerMock.Object);
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
    public async Task ExtractTextAsync_WithValidPdf_ReturnsText()
    {
        var responseJson = """
        {
            "ParsedResults": [
                { "ParsedText": "INSURANCE POLICY DOCUMENT\nPolicy Number: HO-2024-789456\nEffective Date: January 1, 2024" },
                { "ParsedText": "Coverage Details\nDwelling: $250,000\nPersonal Property: $125,000" }
            ],
            "IsErroredOnProcessing": false
        }
        """;

        var handler = CreateMockHandler(HttpStatusCode.OK, responseJson);
        var service = CreateService("ocr_test_key_123", handler.Object);

        var result = await service.ExtractTextAsync(new byte[] { 1, 2, 3 }, "application/pdf");

        Assert.True(result.IsSuccess);
        Assert.Equal("OcrSpace", result.Provider);
        Assert.Equal(2, result.PageCount);
        Assert.Contains("INSURANCE POLICY DOCUMENT", result.ExtractedText);
        Assert.Contains("HO-2024-789456", result.ExtractedText);
    }

    [Fact]
    public async Task ExtractTextAsync_WithMissingApiKey_ReturnsError()
    {
        var handler = CreateMockHandler(HttpStatusCode.OK, "{}");
        var service = CreateService("", handler.Object);

        var result = await service.ExtractTextAsync(new byte[] { 1, 2, 3 });

        Assert.False(result.IsSuccess);
        Assert.Contains("not configured", result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractTextAsync_WithOcrError_ReturnsError()
    {
        var responseJson = """
        {
            "ParsedResults": [],
            "IsErroredOnProcessing": true,
            "ErrorMessage": ["File could not be processed"]
        }
        """;

        var handler = CreateMockHandler(HttpStatusCode.OK, responseJson);
        var service = CreateService("ocr_test_key_123", handler.Object);

        var result = await service.ExtractTextAsync(new byte[] { 1, 2, 3 });

        Assert.False(result.IsSuccess);
        Assert.Contains("could not be processed", result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractTextAsync_WithApiError_ReturnsError()
    {
        var handler = CreateMockHandler(HttpStatusCode.InternalServerError, """{"error":"Server error"}""");
        var service = CreateService("ocr_test_key_123", handler.Object);

        var result = await service.ExtractTextAsync(new byte[] { 1, 2, 3 });

        Assert.False(result.IsSuccess);
        Assert.Contains("InternalServerError", result.ErrorMessage);
    }
}
