using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SentimentAnalyzer.Agents.Configuration;
using SentimentAnalyzer.API.Services.Multimodal;
using Xunit;

namespace SentimentAnalyzer.Tests;

/// <summary>
/// Tests for AzureLanguageNerService.
/// Validates configuration checks, provider name, and graceful handling
/// when no real Azure Language API key is available.
/// </summary>
public class AzureLanguageNerServiceTests
{
    private static AzureLanguageNerService CreateService(string apiKey = "", string endpoint = "")
    {
        var settings = new AgentSystemSettings
        {
            AzureLanguage = new AzureLanguageSettings { ApiKey = apiKey, Endpoint = endpoint }
        };
        return new AzureLanguageNerService(
            Options.Create(settings),
            new Mock<ILogger<AzureLanguageNerService>>().Object);
    }

    [Fact]
    public async Task ExtractEntitiesAsync_MissingApiKey_ReturnsFailure()
    {
        // Arrange — no API key configured
        var service = CreateService(apiKey: "", endpoint: "https://test-language.cognitiveservices.azure.com/");

        // Act
        var result = await service.ExtractEntitiesAsync(
            "Policyholder Maria Garcia filed claim CLM-2024002 for fire damage at 789 Oak Street, Denver CO.");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("AzureLanguage", result.Provider);
        Assert.Contains("API key not configured", result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractEntitiesAsync_MissingEndpoint_ReturnsFailure()
    {
        // Arrange — API key present but no endpoint
        var service = CreateService(apiKey: "test-azure-language-key-abc123", endpoint: "");

        // Act
        var result = await service.ExtractEntitiesAsync(
            "Adjuster Robert Chen reviewed property damage claim CLM-2024003 for hail damage in Oklahoma City.");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("AzureLanguage", result.Provider);
        Assert.Contains("endpoint not configured", result.ErrorMessage);
    }

    [Fact]
    public void Provider_ReturnsAzureLanguage()
    {
        // Arrange — create service with missing config so we can test provider name
        var service = CreateService(apiKey: "", endpoint: "");

        // Act
        var result = service.ExtractEntitiesAsync(
            "Policy review for Great Lakes Manufacturing Corp.").Result;

        // Assert — the provider field should always be "AzureLanguage"
        Assert.Equal("AzureLanguage", result.Provider);
    }

    [Fact]
    public async Task ExtractEntitiesAsync_WithInsuranceText_ReturnsResultWithoutException()
    {
        // Arrange — real endpoint format but invalid credentials (no real API key in tests)
        var service = CreateService(
            apiKey: "test-azure-language-key-xyz789",
            endpoint: "https://test-resource.cognitiveservices.azure.com/");

        // Act — should NOT throw, even with invalid credentials
        var result = await service.ExtractEntitiesAsync(
            "Policyholder John Smith filed claim CLM-2024001 for water damage at 456 Elm Drive, Springfield IL.");

        // Assert — IsSuccess is false because the API key is invalid (no real Azure resource)
        // but no unhandled exception should be thrown
        Assert.False(result.IsSuccess);
        Assert.Equal("AzureLanguage", result.Provider);
        Assert.NotNull(result.ErrorMessage);
        Assert.NotEmpty(result.ErrorMessage);
    }
}
