using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SentimentAnalyzer.Agents.Configuration;
using SentimentAnalyzer.API.Services.Multimodal;
using Xunit;

namespace SentimentAnalyzer.Tests;

/// <summary>
/// Tests for AzureDocumentIntelligenceOcrService (Tier 2 OCR).
/// Tests validation paths (API key, endpoint, file size) that don't require a live Azure connection.
/// </summary>
public class AzureDocumentIntelligenceOcrServiceTests
{
    private readonly Mock<ILogger<AzureDocumentIntelligenceOcrService>> _loggerMock = new();

    private AzureDocumentIntelligenceOcrService CreateService(string apiKey = "", string endpoint = "")
    {
        var settings = new AgentSystemSettings
        {
            AzureDocumentIntelligence = new AzureDocumentIntelligenceSettings
            {
                ApiKey = apiKey,
                Endpoint = endpoint,
                Model = "prebuilt-read"
            }
        };
        var optionsMock = new Mock<IOptions<AgentSystemSettings>>();
        optionsMock.Setup(o => o.Value).Returns(settings);
        return new AzureDocumentIntelligenceOcrService(optionsMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task ExtractTextAsync_WithMissingApiKey_ReturnsError()
    {
        var service = CreateService(apiKey: "", endpoint: "https://insurance-ocr.cognitiveservices.azure.com/");

        var result = await service.ExtractTextAsync(new byte[] { 1, 2, 3 });

        Assert.False(result.IsSuccess);
        Assert.Equal("AzureDocIntel", result.Provider);
        Assert.Contains("not configured", result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractTextAsync_WithMissingEndpoint_ReturnsError()
    {
        var service = CreateService(apiKey: "azure_doc_intel_key_abc123", endpoint: "");

        var result = await service.ExtractTextAsync(new byte[] { 1, 2, 3 });

        Assert.False(result.IsSuccess);
        Assert.Equal("AzureDocIntel", result.Provider);
        Assert.Contains("not configured", result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractTextAsync_FileSizeExceedsLimit_ReturnsError()
    {
        var service = CreateService(
            apiKey: "azure_doc_intel_key_abc123",
            endpoint: "https://insurance-ocr.cognitiveservices.azure.com/");

        // 5MB exceeds the 4MB F0 tier limit
        var oversizedDocument = new byte[5 * 1024 * 1024];

        var result = await service.ExtractTextAsync(oversizedDocument);

        Assert.False(result.IsSuccess);
        Assert.Equal("AzureDocIntel", result.Provider);
        Assert.Contains("4MB", result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractTextAsync_ProviderNameIsAzureDocIntel()
    {
        // Even on validation failure, the provider name must be "AzureDocIntel"
        var service = CreateService(apiKey: "", endpoint: "");

        var result = await service.ExtractTextAsync(new byte[] { 1, 2, 3 });

        Assert.Equal("AzureDocIntel", result.Provider);
    }
}
