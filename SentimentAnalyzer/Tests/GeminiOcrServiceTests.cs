using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using SentimentAnalyzer.Agents.Configuration;
using SentimentAnalyzer.Agents.Orchestration;
using SentimentAnalyzer.API.Services.Multimodal;
using Xunit;

namespace SentimentAnalyzer.Tests;

/// <summary>
/// Tests for GeminiOcrService (Tier 3 OCR — Gemini Vision API).
/// Uses mocked HttpMessageHandler to simulate Gemini API responses with insurance-realistic content.
/// </summary>
public class GeminiOcrServiceTests
{
    private readonly Mock<ILogger<GeminiOcrService>> _loggerMock = new();
    private readonly Mock<IPIIRedactor> _piiRedactorMock = new();

    private GeminiOcrService CreateService(string apiKey, HttpMessageHandler handler, bool withRedactor = false)
    {
        var settings = new AgentSystemSettings
        {
            Gemini = new GeminiSettings { ApiKey = apiKey }
        };
        var optionsMock = new Mock<IOptions<AgentSystemSettings>>();
        optionsMock.Setup(o => o.Value).Returns(settings);

        var httpClient = new HttpClient(handler);

        if (withRedactor)
        {
            _piiRedactorMock.Setup(p => p.Redact(It.IsAny<string>())).Returns<string>(s => s);
            return new GeminiOcrService(httpClient, optionsMock.Object, _loggerMock.Object, _piiRedactorMock.Object);
        }

        return new GeminiOcrService(httpClient, optionsMock.Object, _loggerMock.Object);
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
            "candidates": [{
                "content": {
                    "parts": [{
                        "text": "CLAIM FORM - PROPERTY DAMAGE\nClaim Number: CLM-2024-001234\nDate of Loss: March 15, 2024\nInsured: Midwest Property Holdings\nType: Water Damage\nEstimated Loss: $45,000"
                    }]
                }
            }]
        }
        """;

        var handler = CreateMockHandler(HttpStatusCode.OK, responseJson);
        var service = CreateService("gemini_api_key_ins_456", handler.Object);

        var result = await service.ExtractTextAsync(new byte[] { 1, 2, 3 }, "application/pdf");

        Assert.True(result.IsSuccess);
        Assert.Equal("GeminiVision", result.Provider);
        Assert.Equal(0.75, result.Confidence);
        Assert.Contains("CLAIM FORM - PROPERTY DAMAGE", result.ExtractedText);
        Assert.Contains("CLM-2024-001234", result.ExtractedText);
        Assert.Contains("Midwest Property Holdings", result.ExtractedText);
    }

    [Fact]
    public async Task ExtractTextAsync_WithMissingApiKey_ReturnsError()
    {
        var handler = CreateMockHandler(HttpStatusCode.OK, "{}");
        var service = CreateService("", handler.Object);

        var result = await service.ExtractTextAsync(new byte[] { 1, 2, 3 });

        Assert.False(result.IsSuccess);
        Assert.Equal("GeminiVision", result.Provider);
        Assert.Contains("not configured", result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractTextAsync_WithApiError_ReturnsError()
    {
        var handler = CreateMockHandler(HttpStatusCode.InternalServerError, """{"error":"Gemini service unavailable"}""");
        var service = CreateService("gemini_api_key_ins_456", handler.Object);

        var result = await service.ExtractTextAsync(new byte[] { 1, 2, 3 });

        Assert.False(result.IsSuccess);
        Assert.Equal("GeminiVision", result.Provider);
        Assert.Contains("InternalServerError", result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractTextAsync_StripsConversationalPreamble()
    {
        var responseJson = """
        {
            "candidates": [{
                "content": {
                    "parts": [{
                        "text": "Here is the extracted text:\nACCIDENT REPORT\nVehicle: 2023 Toyota Camry\nPolicy: AUTO-2024-556789\nDriver: Licensed Insured\nDate of Accident: February 10, 2024\nLocation: Interstate 85, mile marker 142\nDamage Estimate: $8,750"
                    }]
                }
            }]
        }
        """;

        var handler = CreateMockHandler(HttpStatusCode.OK, responseJson);
        var service = CreateService("gemini_api_key_ins_456", handler.Object);

        var result = await service.ExtractTextAsync(new byte[] { 1, 2, 3 });

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain("Here is the extracted text:", result.ExtractedText);
        Assert.Contains("ACCIDENT REPORT", result.ExtractedText);
        Assert.Contains("AUTO-2024-556789", result.ExtractedText);
    }

    [Fact]
    public async Task ExtractTextAsync_CountsPageBreakMarkers()
    {
        var responseJson = """
        {
            "candidates": [{
                "content": {
                    "parts": [{
                        "text": "HOMEOWNERS POLICY DECLARATIONS\nPolicy Number: HO-2024-112233\nInsured: Lakeside Residential Trust\n---PAGE BREAK---\nCOVERAGE SCHEDULE\nDwelling: $350,000\nPersonal Property: $175,000\n---PAGE BREAK---\nENDORSEMENTS\nScheduled Personal Property Rider\nWater Backup Coverage: $25,000"
                    }]
                }
            }]
        }
        """;

        var handler = CreateMockHandler(HttpStatusCode.OK, responseJson);
        var service = CreateService("gemini_api_key_ins_456", handler.Object);

        var result = await service.ExtractTextAsync(new byte[] { 1, 2, 3 });

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.PageCount);
        Assert.Contains("HOMEOWNERS POLICY DECLARATIONS", result.ExtractedText);
        Assert.Contains("ENDORSEMENTS", result.ExtractedText);
    }

    [Fact]
    public async Task ExtractTextAsync_AppliesPiiRedaction()
    {
        var responseJson = """
        {
            "candidates": [{
                "content": {
                    "parts": [{
                        "text": "WORKERS COMPENSATION CLAIM\nClaimant: Robert Martinez\nSSN: 456-78-9012\nEmployer: Pacific Northwest Manufacturing Inc\nInjury Date: April 3, 2024\nClaim Amount: $32,500"
                    }]
                }
            }]
        }
        """;

        var handler = CreateMockHandler(HttpStatusCode.OK, responseJson);
        var service = CreateService("gemini_api_key_ins_456", handler.Object, withRedactor: true);

        // Override the default pass-through: redact SSN
        _piiRedactorMock.Setup(p => p.Redact(It.IsAny<string>()))
            .Returns<string>(s => s.Replace("456-78-9012", "[SSN-REDACTED]"));

        var result = await service.ExtractTextAsync(new byte[] { 1, 2, 3 });

        Assert.True(result.IsSuccess);
        Assert.Contains("[SSN-REDACTED]", result.ExtractedText);
        Assert.DoesNotContain("456-78-9012", result.ExtractedText);
        _piiRedactorMock.Verify(p => p.Redact(It.IsAny<string>()), Times.Once);
    }
}
