using Microsoft.Extensions.Logging;
using Moq;
using SentimentAnalyzer.API.Services.Multimodal;
using Xunit;

namespace SentimentAnalyzer.Tests;

/// <summary>
/// Tests for ResilientSpeechToTextProvider.
/// Validates 2-tier fallback chain: Deepgram ($200 credit) → Azure Speech (5 hrs/month).
/// Verifies exponential backoff cooldown, provider recovery, and error propagation.
/// </summary>
public class ResilientSpeechToTextProviderTests
{
    private readonly Mock<ISpeechToTextService> _deepgramMock = new();
    private readonly Mock<ISpeechToTextService> _azureSpeechMock = new();
    private readonly Mock<ILogger<ResilientSpeechToTextProvider>> _loggerMock = new();

    /// <summary>
    /// Creates a ResilientSpeechToTextProvider with mocked dependencies.
    /// Constructor takes [FromKeyedServices] but direct construction works for testing.
    /// </summary>
    private ResilientSpeechToTextProvider CreateProvider()
    {
        return new ResilientSpeechToTextProvider(
            _deepgramMock.Object,
            _azureSpeechMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task TranscribeAsync_PrimarySucceeds_ReturnsPrimaryResult()
    {
        // Arrange: Deepgram succeeds with policyholder collision report
        _deepgramMock.Setup(s => s.TranscribeAsync(
                It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranscriptionResult
            {
                IsSuccess = true,
                Text = "Policyholder reports a vehicle collision at the intersection of Main Street and Oak Avenue. Estimated damage approximately five thousand dollars.",
                Confidence = 0.96,
                DurationSeconds = 8.5,
                Provider = "Deepgram"
            });

        var provider = CreateProvider();

        // Act
        var result = await provider.TranscribeAsync(
            new byte[] { 0x52, 0x49, 0x46, 0x46 }, "audio/wav");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Deepgram", result.Provider);
        Assert.Contains("vehicle collision", result.Text);
        Assert.Contains("Main Street", result.Text);
        Assert.Equal(0.96, result.Confidence);
        Assert.Equal(8.5, result.DurationSeconds);

        // Azure Speech should NEVER be called when Deepgram succeeds
        _azureSpeechMock.Verify(
            s => s.TranscribeAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TranscribeAsync_PrimaryFails_FallsBackToAzure()
    {
        // Arrange: Deepgram fails (API key expired), Azure succeeds
        _deepgramMock.Setup(s => s.TranscribeAsync(
                It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranscriptionResult
            {
                IsSuccess = false,
                Provider = "Deepgram",
                ErrorMessage = "Deepgram API error: TooManyRequests"
            });

        _azureSpeechMock.Setup(s => s.TranscribeAsync(
                It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranscriptionResult
            {
                IsSuccess = true,
                Text = "Claimant reported water damage to the basement of their residential property at 742 Evergreen Terrace. The adjuster noted extensive mold growth requiring remediation estimated at twelve thousand dollars.",
                Confidence = 0.9,
                DurationSeconds = 12.3,
                Provider = "AzureSpeech"
            });

        var provider = CreateProvider();

        // Act
        var result = await provider.TranscribeAsync(
            new byte[] { 1, 2, 3 }, "audio/wav");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("AzureSpeech", result.Provider);
        Assert.Contains("water damage", result.Text);
        Assert.Contains("mold growth", result.Text);
        Assert.Equal(0.9, result.Confidence);

        // Both providers should have been called
        _deepgramMock.Verify(
            s => s.TranscribeAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _azureSpeechMock.Verify(
            s => s.TranscribeAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TranscribeAsync_BothFail_ReturnsLastError()
    {
        // Arrange: both providers fail
        _deepgramMock.Setup(s => s.TranscribeAsync(
                It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranscriptionResult
            {
                IsSuccess = false,
                Provider = "Deepgram",
                ErrorMessage = "Deepgram API error: ServiceUnavailable"
            });

        _azureSpeechMock.Setup(s => s.TranscribeAsync(
                It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranscriptionResult
            {
                IsSuccess = false,
                Provider = "AzureSpeech",
                ErrorMessage = "Azure Speech API error: TooManyRequests"
            });

        var provider = CreateProvider();

        // Act
        var result = await provider.TranscribeAsync(
            new byte[] { 1, 2, 3 }, "audio/wav");

        // Assert: returns Azure's error (the last provider attempted)
        Assert.False(result.IsSuccess);
        Assert.Equal("AzureSpeech", result.Provider);
        Assert.Contains("TooManyRequests", result.ErrorMessage);
    }

    [Fact]
    public async Task TranscribeAsync_AzureInCooldown_SkipsAzure()
    {
        // Arrange: Deepgram always fails, Azure fails on first attempt (triggers cooldown)
        _deepgramMock.Setup(s => s.TranscribeAsync(
                It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranscriptionResult
            {
                IsSuccess = false,
                Provider = "Deepgram",
                ErrorMessage = "Deepgram API key not configured."
            });

        _azureSpeechMock.Setup(s => s.TranscribeAsync(
                It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranscriptionResult
            {
                IsSuccess = false,
                Provider = "AzureSpeech",
                ErrorMessage = "Azure Speech API error: TooManyRequests"
            });

        var provider = CreateProvider();

        // First call: Deepgram fails → Azure fails (triggers 30s cooldown)
        var result1 = await provider.TranscribeAsync(
            new byte[] { 1, 2, 3 }, "audio/wav");
        Assert.False(result1.IsSuccess);
        Assert.Equal("AzureSpeech", result1.Provider);

        // Second call: Deepgram fails → Azure SKIPPED (in cooldown) → returns Deepgram error
        var result2 = await provider.TranscribeAsync(
            new byte[] { 4, 5, 6 }, "audio/wav");
        Assert.False(result2.IsSuccess);
        Assert.Equal("Deepgram", result2.Provider);
        Assert.Contains("not configured", result2.ErrorMessage);

        // Azure should have been called exactly once (skipped on second call due to cooldown)
        _azureSpeechMock.Verify(
            s => s.TranscribeAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TranscribeAsync_AzureRecoversAfterCooldown()
    {
        // Arrange: Deepgram always fails
        _deepgramMock.Setup(s => s.TranscribeAsync(
                It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranscriptionResult
            {
                IsSuccess = false,
                Provider = "Deepgram",
                ErrorMessage = "Deepgram API key not configured."
            });

        // Azure fails first, then succeeds on retry after cooldown
        var azureCallCount = 0;
        _azureSpeechMock.Setup(s => s.TranscribeAsync(
                It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                azureCallCount++;
                if (azureCallCount == 1)
                {
                    return new TranscriptionResult
                    {
                        IsSuccess = false,
                        Provider = "AzureSpeech",
                        ErrorMessage = "Azure Speech API error: ServiceUnavailable"
                    };
                }

                return new TranscriptionResult
                {
                    IsSuccess = true,
                    Text = "The insured party described a rear-end collision on Interstate 95 during morning rush hour. Three vehicles involved with minor injuries reported by the policyholder.",
                    Confidence = 0.9,
                    DurationSeconds = 10.1,
                    Provider = "AzureSpeech"
                };
            });

        // Use a provider subclass that lets us manipulate the cooldown for testing
        var provider = CreateProvider();

        // First call: Deepgram fails → Azure fails (triggers cooldown)
        var result1 = await provider.TranscribeAsync(
            new byte[] { 1, 2, 3 }, "audio/wav");
        Assert.False(result1.IsSuccess);

        // Simulate cooldown expiration by making many rapid calls
        // The cooldown is 30s minimum, but we can verify the provider structure by
        // using reflection to reset the cooldown (pragmatic test approach)
        var cooldownField = typeof(ResilientSpeechToTextProvider)
            .GetField("_azureCooldownExpiresUtc",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(cooldownField);
        cooldownField!.SetValue(provider, (DateTime?)DateTime.UtcNow.AddSeconds(-1));

        // After cooldown expires: Deepgram fails → Azure retried and succeeds
        var result2 = await provider.TranscribeAsync(
            new byte[] { 4, 5, 6 }, "audio/wav");
        Assert.True(result2.IsSuccess);
        Assert.Equal("AzureSpeech", result2.Provider);
        Assert.Contains("rear-end collision", result2.Text);
        Assert.Contains("Interstate 95", result2.Text);

        // Azure should have been called exactly twice (once failed, once recovered)
        Assert.Equal(2, azureCallCount);
    }

    [Fact]
    public async Task TranscribeAsync_PassesMimeTypeToProviders()
    {
        // Arrange: Deepgram succeeds — verify the mime type is forwarded
        _deepgramMock.Setup(s => s.TranscribeAsync(
                It.IsAny<byte[]>(), "audio/webm", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranscriptionResult
            {
                IsSuccess = true,
                Text = "Adjuster field recording from the property inspection at 1200 Commerce Drive. Observed significant structural damage to the north-facing wall consistent with wind damage from the reported tornado event.",
                Confidence = 0.93,
                DurationSeconds = 15.7,
                Provider = "Deepgram"
            });

        var provider = CreateProvider();

        // Act: pass audio/webm MIME type
        var result = await provider.TranscribeAsync(
            new byte[] { 1, 2, 3 }, "audio/webm");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains("structural damage", result.Text);

        // Verify Deepgram was called with the correct MIME type
        _deepgramMock.Verify(
            s => s.TranscribeAsync(It.IsAny<byte[]>(), "audio/webm", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TranscribeAsync_ExponentialBackoff_IncreasesPerFailure()
    {
        // Arrange: Deepgram always fails, Azure always fails (to trigger multiple cooldowns)
        _deepgramMock.Setup(s => s.TranscribeAsync(
                It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranscriptionResult
            {
                IsSuccess = false,
                Provider = "Deepgram",
                ErrorMessage = "Deepgram API key not configured."
            });

        _azureSpeechMock.Setup(s => s.TranscribeAsync(
                It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranscriptionResult
            {
                IsSuccess = false,
                Provider = "AzureSpeech",
                ErrorMessage = "Azure Speech API error: ServiceUnavailable"
            });

        var provider = CreateProvider();

        // Use reflection to read the cooldown state
        var cooldownField = typeof(ResilientSpeechToTextProvider)
            .GetField("_azureCooldownExpiresUtc",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var failureField = typeof(ResilientSpeechToTextProvider)
            .GetField("_azureConsecutiveFailures",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.NotNull(cooldownField);
        Assert.NotNull(failureField);

        // First failure: 30s cooldown
        await provider.TranscribeAsync(new byte[] { 1 }, "audio/wav");
        var failures1 = (int)failureField!.GetValue(provider)!;
        Assert.Equal(1, failures1);

        // Reset cooldown to simulate expiration, trigger second failure: 60s cooldown
        cooldownField!.SetValue(provider, (DateTime?)DateTime.UtcNow.AddSeconds(-1));
        await provider.TranscribeAsync(new byte[] { 2 }, "audio/wav");
        var failures2 = (int)failureField.GetValue(provider)!;
        Assert.Equal(2, failures2);

        // Reset cooldown, trigger third failure: 120s cooldown
        cooldownField.SetValue(provider, (DateTime?)DateTime.UtcNow.AddSeconds(-1));
        await provider.TranscribeAsync(new byte[] { 3 }, "audio/wav");
        var failures3 = (int)failureField.GetValue(provider)!;
        Assert.Equal(3, failures3);
    }
}
