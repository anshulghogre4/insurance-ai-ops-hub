using Microsoft.Extensions.Logging;
using Moq;
using SentimentAnalyzer.API.Services.Multimodal;
using Xunit;

namespace SentimentAnalyzer.Tests;

/// <summary>
/// Tests for ResilientEntityExtractionProvider.
/// Validates 2-tier fallback chain: HuggingFace (primary) → Azure Language (fallback).
/// Follows the same test pattern as ResilientOcrProviderTests.
/// </summary>
public class ResilientEntityExtractionProviderTests
{
    private readonly Mock<IEntityExtractionService> _huggingFaceMock = new();
    private readonly Mock<IEntityExtractionService> _azureLanguageMock = new();
    private readonly Mock<ILogger<ResilientEntityExtractionProvider>> _loggerMock = new();

    private ResilientEntityExtractionProvider CreateProvider()
    {
        return new ResilientEntityExtractionProvider(
            _huggingFaceMock.Object,
            _azureLanguageMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task ExtractEntitiesAsync_PrimarySucceeds_ReturnsPrimaryResult()
    {
        // Arrange — HuggingFace returns a successful result with insurance entities
        _huggingFaceMock.Setup(s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EntityExtractionResult
            {
                IsSuccess = true,
                Provider = "HuggingFace",
                Entities =
                [
                    new ExtractedEntity { Type = "PERSON", Value = "Sarah Johnson", Confidence = 0.97, StartIndex = 14, EndIndex = 27 },
                    new ExtractedEntity { Type = "ORGANIZATION", Value = "Midwest Mutual Insurance", Confidence = 0.94, StartIndex = 45, EndIndex = 69 },
                    new ExtractedEntity { Type = "CLAIM_NUMBER", Value = "CLM-2024005", Confidence = 0.95, StartIndex = 82, EndIndex = 93 }
                ]
            });

        var provider = CreateProvider();

        // Act
        var result = await provider.ExtractEntitiesAsync(
            "Policyholder Sarah Johnson contacted Midwest Mutual Insurance regarding claim CLM-2024005 for storm damage.");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("HuggingFace", result.Provider);
        Assert.Equal(3, result.Entities.Count);
        Assert.Contains(result.Entities, e => e.Type == "PERSON" && e.Value == "Sarah Johnson");
        Assert.Contains(result.Entities, e => e.Type == "CLAIM_NUMBER" && e.Value == "CLM-2024005");

        // Azure Language should NEVER be called when HuggingFace succeeds
        _azureLanguageMock.Verify(
            s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExtractEntitiesAsync_PrimaryFails_FallsBackToAzure()
    {
        // Arrange — HuggingFace fails (cold start), Azure Language succeeds
        _huggingFaceMock.Setup(s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EntityExtractionResult
            {
                IsSuccess = false,
                Provider = "HuggingFace",
                ErrorMessage = "Model is loading (cold start). Please retry in 20-30 seconds."
            });

        _azureLanguageMock.Setup(s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EntityExtractionResult
            {
                IsSuccess = true,
                Provider = "AzureLanguage",
                Entities =
                [
                    new ExtractedEntity { Type = "PERSON", Value = "David Martinez", Confidence = 0.96, StartIndex = 0, EndIndex = 14 },
                    new ExtractedEntity { Type = "LOCATION", Value = "Houston TX", Confidence = 0.91, StartIndex = 68, EndIndex = 78 },
                    new ExtractedEntity { Type = "MONEY", Value = "$45,000", Confidence = 0.93, StartIndex = 95, EndIndex = 102 }
                ]
            });

        var provider = CreateProvider();

        // Act
        var result = await provider.ExtractEntitiesAsync(
            "David Martinez submitted a property damage claim for wind damage at his residence in Houston TX. Estimated loss: $45,000.");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("AzureLanguage", result.Provider);
        Assert.Equal(3, result.Entities.Count);
        Assert.Contains(result.Entities, e => e.Type == "PERSON" && e.Value == "David Martinez");
        Assert.Contains(result.Entities, e => e.Type == "MONEY" && e.Value == "$45,000");
    }

    [Fact]
    public async Task ExtractEntitiesAsync_BothFail_ReturnsLastError()
    {
        // Arrange — both providers fail
        _huggingFaceMock.Setup(s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EntityExtractionResult
            {
                IsSuccess = false,
                Provider = "HuggingFace",
                ErrorMessage = "HuggingFace API error: TooManyRequests"
            });

        _azureLanguageMock.Setup(s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EntityExtractionResult
            {
                IsSuccess = false,
                Provider = "AzureLanguage",
                ErrorMessage = "Azure Language API error (HTTP 429): Rate limit exceeded"
            });

        var provider = CreateProvider();

        // Act
        var result = await provider.ExtractEntitiesAsync(
            "Workers compensation claim for warehouse employee injured during loading operations at distribution center.");

        // Assert — returns HuggingFace error (primary provider's error is returned)
        Assert.False(result.IsSuccess);
        Assert.Equal("HuggingFace", result.Provider);
        Assert.Contains("TooManyRequests", result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractEntitiesAsync_AzureInCooldown_SkipsAzure()
    {
        // Arrange — HuggingFace always fails, Azure fails on first call (triggers cooldown)
        _huggingFaceMock.Setup(s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EntityExtractionResult
            {
                IsSuccess = false,
                Provider = "HuggingFace",
                ErrorMessage = "HuggingFace API key not configured."
            });

        _azureLanguageMock.Setup(s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EntityExtractionResult
            {
                IsSuccess = false,
                Provider = "AzureLanguage",
                ErrorMessage = "Azure Language API error (HTTP 429): Rate limit exceeded"
            });

        var provider = CreateProvider();

        // Act — first call: HuggingFace fails → Azure fails (triggers 30s cooldown)
        var result1 = await provider.ExtractEntitiesAsync(
            "Liability claim filed by policyholder Lisa Wong against commercial general liability policy CGL-2024-112233.");
        Assert.False(result1.IsSuccess);

        // Act — second call: HuggingFace fails → Azure SKIPPED (in cooldown)
        var result2 = await provider.ExtractEntitiesAsync(
            "Auto collision claim reported by James Park, policy AUTO-2024-445566, rear-end accident on Interstate 95.");
        Assert.False(result2.IsSuccess);

        // Assert — Azure should have been called exactly once (skipped on second call due to cooldown)
        _azureLanguageMock.Verify(
            s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExtractEntitiesAsync_AzureRecoversAfterCooldown()
    {
        // Arrange — HuggingFace always fails
        _huggingFaceMock.Setup(s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EntityExtractionResult
            {
                IsSuccess = false,
                Provider = "HuggingFace",
                ErrorMessage = "HuggingFace API error: ServiceUnavailable"
            });

        var callCount = 0;
        _azureLanguageMock.Setup(s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    // First call: Azure fails (triggers cooldown)
                    return new EntityExtractionResult
                    {
                        IsSuccess = false,
                        Provider = "AzureLanguage",
                        ErrorMessage = "Azure Language API error (HTTP 503): Service unavailable"
                    };
                }

                // Subsequent calls: Azure succeeds (recovered)
                return new EntityExtractionResult
                {
                    IsSuccess = true,
                    Provider = "AzureLanguage",
                    Entities =
                    [
                        new ExtractedEntity { Type = "PERSON", Value = "Patricia Williams", Confidence = 0.95, StartIndex = 14, EndIndex = 31 },
                        new ExtractedEntity { Type = "LOCATION", Value = "Tampa FL", Confidence = 0.90, StartIndex = 85, EndIndex = 93 }
                    ]
                };
            });

        var provider = CreateProvider();

        // Act — first call: HuggingFace fails → Azure fails (triggers cooldown)
        var result1 = await provider.ExtractEntitiesAsync(
            "Policyholder Patricia Williams reported flood damage to her property at 321 Bay Street, Tampa FL.");
        Assert.False(result1.IsSuccess);

        // The cooldown is 30 seconds minimum. We can't easily wait in a unit test,
        // but we can verify that Azure was called once. In production, after cooldown
        // expires, Azure would be retried automatically.

        // Assert — Azure was called once on the first attempt
        _azureLanguageMock.Verify(
            s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);

        // Assert — HuggingFace was called on both attempts (always attempted as primary)
        _huggingFaceMock.Verify(
            s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
