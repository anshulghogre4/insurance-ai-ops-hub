using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SentimentAnalyzer.Agents.Configuration;
using SentimentAnalyzer.API.Services.Multimodal;
using Xunit;

namespace SentimentAnalyzer.Tests;

/// <summary>
/// Tests for AzureContentSafetyService (text and image moderation).
/// Tests validation paths (API key, endpoint, empty data) that don't require a live Azure connection.
/// </summary>
public class AzureContentSafetyServiceTests
{
    private readonly Mock<ILogger<AzureContentSafetyService>> _loggerMock = new();

    private AzureContentSafetyService CreateService(string apiKey = "", string endpoint = "")
    {
        var settings = new AgentSystemSettings
        {
            AzureContentSafety = new AzureContentSafetySettings
            {
                ApiKey = apiKey,
                Endpoint = endpoint
            }
        };
        var options = Options.Create(settings);
        return new AzureContentSafetyService(options, _loggerMock.Object);
    }

    [Fact]
    public async Task AnalyzeTextAsync_MissingApiKey_ReturnsFailure()
    {
        var service = CreateService(
            apiKey: "",
            endpoint: "https://insurance-safety.cognitiveservices.azure.com/");

        var result = await service.AnalyzeTextAsync(
            "Water damage claim for residential property at 123 Oak Street.");

        Assert.False(result.IsSuccess);
        Assert.Equal("AzureContentSafety", result.Provider);
        Assert.Contains("API key not configured", result.ErrorMessage);
    }

    [Fact]
    public async Task AnalyzeTextAsync_MissingEndpoint_ReturnsFailure()
    {
        var service = CreateService(
            apiKey: "azure_content_safety_key_abc123",
            endpoint: "");

        var result = await service.AnalyzeTextAsync(
            "Policyholder reports flooding from burst pipe affecting basement and first floor.");

        Assert.False(result.IsSuccess);
        Assert.Equal("AzureContentSafety", result.Provider);
        Assert.Contains("endpoint not configured", result.ErrorMessage);
    }

    [Fact]
    public async Task AnalyzeImageAsync_EmptyImageData_ReturnsFailure()
    {
        var service = CreateService(
            apiKey: "azure_content_safety_key_abc123",
            endpoint: "https://insurance-safety.cognitiveservices.azure.com/");

        var result = await service.AnalyzeImageAsync([]);

        Assert.False(result.IsSuccess);
        Assert.Equal("AzureContentSafety", result.Provider);
        Assert.Contains("empty or null", result.ErrorMessage);
    }

    [Fact]
    public async Task Provider_ReturnsAzureContentSafety()
    {
        // Even on validation failure, the provider name must be "AzureContentSafety"
        var service = CreateService(apiKey: "", endpoint: "");

        var result = await service.AnalyzeTextAsync(
            "Claim adjuster reviewed the property damage assessment for hail damage to roofing.");

        Assert.Equal("AzureContentSafety", result.Provider);
    }

    [Fact]
    public async Task AnalyzeTextAsync_WithInsuranceClaimText_ReturnsResult()
    {
        // With configured credentials but no real Azure connection,
        // the service should return a failure result (not throw) due to connection error
        var service = CreateService(
            apiKey: "azure_content_safety_key_test_only",
            endpoint: "https://insurance-safety-test.cognitiveservices.azure.com/");

        var result = await service.AnalyzeTextAsync(
            "Water damage claim for residential property at 123 Oak Street. " +
            "Policyholder reports flooding from burst pipe affecting basement and first floor. " +
            "Estimated repair cost: $45,000. Policy coverage limit: $500,000. " +
            "Adjuster site visit scheduled for February 28, 2026.");

        // No real API key, so the SDK call will fail — but the service should catch gracefully
        Assert.False(result.IsSuccess);
        Assert.Equal("AzureContentSafety", result.Provider);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task AnalyzeImageAsync_MissingApiKey_ReturnsFailure()
    {
        var service = CreateService(
            apiKey: "",
            endpoint: "https://insurance-safety.cognitiveservices.azure.com/");

        // Simulate a small image payload (claim photo of vehicle damage)
        var sampleImageData = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        var result = await service.AnalyzeImageAsync(sampleImageData);

        Assert.False(result.IsSuccess);
        Assert.Equal("AzureContentSafety", result.Provider);
        Assert.Contains("API key not configured", result.ErrorMessage);
    }
}
