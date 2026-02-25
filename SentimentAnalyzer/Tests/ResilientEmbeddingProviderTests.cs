using Microsoft.Extensions.Logging;
using Moq;
using SentimentAnalyzer.Agents.Orchestration;
using SentimentAnalyzer.API.Services.Embeddings;
using Xunit;

namespace SentimentAnalyzer.Tests;

/// <summary>
/// Tests for ResilientEmbeddingProvider.
/// Validates fallback chain (Voyage AI -> Ollama), PII redaction, and cooldown behavior.
/// </summary>
public class ResilientEmbeddingProviderTests
{
    private readonly Mock<IEmbeddingService> _voyageMock = new();
    private readonly Mock<IEmbeddingService> _ollamaMock = new();
    private readonly Mock<IPIIRedactor> _piiRedactorMock = new();
    private readonly Mock<ILogger<ResilientEmbeddingProvider>> _loggerMock = new();

    private ResilientEmbeddingProvider CreateProvider()
    {
        _voyageMock.Setup(v => v.EmbeddingDimension).Returns(1024);
        _voyageMock.Setup(v => v.ProviderName).Returns("VoyageAI");
        _ollamaMock.Setup(o => o.EmbeddingDimension).Returns(1024);
        _ollamaMock.Setup(o => o.ProviderName).Returns("Ollama");

        // Default PII redaction: pass-through (no PII detected)
        _piiRedactorMock.Setup(p => p.Redact(It.IsAny<string>())).Returns<string>(s => s);

        return new ResilientEmbeddingProvider(
            _voyageMock.Object,
            _ollamaMock.Object,
            _piiRedactorMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_VoyageSucceeds_ReturnsVoyageResult()
    {
        var expectedResult = new EmbeddingResult
        {
            IsSuccess = true,
            Embedding = new float[] { 0.1f, 0.2f, 0.3f },
            Provider = "VoyageAI",
            TokensUsed = 10
        };

        _voyageMock.Setup(v => v.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var provider = CreateProvider();
        var result = await provider.GenerateEmbeddingAsync(
            "Water damage claim filed for commercial property. Estimated loss: $45,000.");

        Assert.True(result.IsSuccess);
        Assert.Equal("VoyageAI", result.Provider);
        Assert.Equal(3, result.Dimension);

        // Ollama should NOT be called
        _ollamaMock.Verify(o => o.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_VoyageFails_FallsBackToOllama()
    {
        _voyageMock.Setup(v => v.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                IsSuccess = false,
                Provider = "VoyageAI",
                ErrorMessage = "Rate limit exceeded (429)"
            });

        _ollamaMock.Setup(o => o.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                IsSuccess = true,
                Embedding = new float[] { 0.4f, 0.5f },
                Provider = "Ollama"
            });

        var provider = CreateProvider();
        var result = await provider.GenerateEmbeddingAsync(
            "Auto insurance claim for rear-end collision at intersection.");

        Assert.True(result.IsSuccess);
        Assert.Equal("Ollama", result.Provider);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_RedactsPiiBeforeSendingToProvider()
    {
        var originalText = "Policyholder John Smith (SSN 123-45-6789, policy HO-2024-789456) filed a claim.";
        var redactedText = "Policyholder [SSN-REDACTED] (SSN [SSN-REDACTED], policy [POLICY-REDACTED]) filed a claim.";

        _voyageMock.Setup(v => v.GenerateEmbeddingAsync(redactedText, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                IsSuccess = true,
                Embedding = new float[] { 0.1f },
                Provider = "VoyageAI"
            });

        var provider = CreateProvider();

        // Override the default pass-through PII redactor AFTER CreateProvider()
        // (CreateProvider sets up a generic pass-through; this specific setup takes precedence in Moq)
        _piiRedactorMock.Setup(p => p.Redact(originalText)).Returns(redactedText);

        await provider.GenerateEmbeddingAsync(originalText);

        // Verify PII was redacted BEFORE sending to Voyage AI
        _piiRedactorMock.Verify(p => p.Redact(originalText), Times.Once);
        _voyageMock.Verify(v => v.GenerateEmbeddingAsync(redactedText, It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_CooldownPreventsDuplicateVoyageCalls()
    {
        // First call: Voyage fails
        _voyageMock.SetupSequence(v => v.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { IsSuccess = false, Provider = "VoyageAI", ErrorMessage = "429" })
            .ReturnsAsync(new EmbeddingResult { IsSuccess = true, Embedding = new float[] { 0.1f }, Provider = "VoyageAI" });

        _ollamaMock.Setup(o => o.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                IsSuccess = true,
                Embedding = new float[] { 0.2f },
                Provider = "Ollama"
            });

        var provider = CreateProvider();

        // First call triggers Voyage failure -> cooldown -> Ollama fallback
        var result1 = await provider.GenerateEmbeddingAsync("First claim analysis text.");
        Assert.Equal("Ollama", result1.Provider);

        // Second call should skip Voyage (cooldown) and go directly to Ollama
        var result2 = await provider.GenerateEmbeddingAsync("Second claim analysis text.");
        Assert.Equal("Ollama", result2.Provider);

        // Voyage should have been called exactly once (second call skipped due to cooldown)
        _voyageMock.Verify(v => v.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateBatchEmbeddingsAsync_VoyageSucceeds_ReturnsBatch()
    {
        _voyageMock.Setup(v => v.GenerateBatchEmbeddingsAsync(It.IsAny<string[]>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BatchEmbeddingResult
            {
                IsSuccess = true,
                Embeddings = new float[][] { new[] { 0.1f, 0.2f }, new[] { 0.3f, 0.4f } },
                Provider = "VoyageAI",
                TotalTokensUsed = 25
            });

        var provider = CreateProvider();
        var result = await provider.GenerateBatchEmbeddingsAsync(new[]
        {
            "Commercial general liability coverage section.",
            "Professional errors and omissions coverage terms."
        }, "document");

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Count);
        Assert.Equal("VoyageAI", result.Provider);
    }

    [Fact]
    public async Task GenerateBatchEmbeddingsAsync_VoyageFails_FallsBackToOllama()
    {
        _voyageMock.Setup(v => v.GenerateBatchEmbeddingsAsync(It.IsAny<string[]>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BatchEmbeddingResult { IsSuccess = false, Provider = "VoyageAI", ErrorMessage = "API error" });

        _ollamaMock.Setup(o => o.GenerateBatchEmbeddingsAsync(It.IsAny<string[]>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BatchEmbeddingResult
            {
                IsSuccess = true,
                Embeddings = new float[][] { new[] { 0.5f }, new[] { 0.6f } },
                Provider = "Ollama"
            });

        var provider = CreateProvider();
        var result = await provider.GenerateBatchEmbeddingsAsync(new[]
        {
            "Policy declarations page content.",
            "Coverage exclusions summary."
        });

        Assert.True(result.IsSuccess);
        Assert.Equal("Ollama", result.Provider);
    }

    [Fact]
    public async Task GenerateBatchEmbeddingsAsync_RedactsPiiInAllTexts()
    {
        var texts = new[]
        {
            "John Smith policy HO-2024-001 claim filed.",
            "Jane Doe SSN 987-65-4321 premium payment."
        };

        var redactedTexts = new[]
        {
            "John Smith policy [POLICY-REDACTED] claim filed.",
            "Jane Doe SSN [SSN-REDACTED] premium payment."
        };

        _voyageMock.Setup(v => v.GenerateBatchEmbeddingsAsync(
                It.Is<string[]>(t => t[0] == redactedTexts[0] && t[1] == redactedTexts[1]),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BatchEmbeddingResult
            {
                IsSuccess = true,
                Embeddings = new float[][] { new[] { 0.1f }, new[] { 0.2f } },
                Provider = "VoyageAI"
            });

        var provider = CreateProvider();

        // Override default pass-through PII redactor AFTER CreateProvider()
        _piiRedactorMock.Setup(p => p.Redact(texts[0])).Returns(redactedTexts[0]);
        _piiRedactorMock.Setup(p => p.Redact(texts[1])).Returns(redactedTexts[1]);

        await provider.GenerateBatchEmbeddingsAsync(texts);

        _piiRedactorMock.Verify(p => p.Redact(texts[0]), Times.Once);
        _piiRedactorMock.Verify(p => p.Redact(texts[1]), Times.Once);
    }

    [Fact]
    public void ProviderName_ReflectsActiveProvider()
    {
        var provider = CreateProvider();

        // Initially Voyage AI is active
        Assert.Contains("VoyageAI", provider.ProviderName);
    }

    [Fact]
    public void EmbeddingDimension_ReflectsActiveProvider()
    {
        var provider = CreateProvider();

        Assert.Equal(1024, provider.EmbeddingDimension);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_BothProvidersFail_ReturnsOllamaError()
    {
        _voyageMock.Setup(v => v.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { IsSuccess = false, Provider = "VoyageAI", ErrorMessage = "Voyage down" });

        _ollamaMock.Setup(o => o.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { IsSuccess = false, Provider = "Ollama", ErrorMessage = "Ollama not running" });

        var provider = CreateProvider();
        var result = await provider.GenerateEmbeddingAsync("Claim under investigation for potential fraud indicators.");

        Assert.False(result.IsSuccess);
        Assert.Equal("Ollama", result.Provider);
        Assert.Contains("not running", result.ErrorMessage);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_InputTypePassedToProvider()
    {
        _voyageMock.Setup(v => v.GenerateEmbeddingAsync(It.IsAny<string>(), "query", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                IsSuccess = true,
                Embedding = new float[] { 0.1f },
                Provider = "VoyageAI"
            });

        var provider = CreateProvider();
        await provider.GenerateEmbeddingAsync("What is the deductible for water damage?", "query");

        _voyageMock.Verify(v => v.GenerateEmbeddingAsync(It.IsAny<string>(), "query", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_NoPiiDetected_StillCallsRedactor()
    {
        var cleanText = "General insurance market overview and trends.";
        _piiRedactorMock.Setup(p => p.Redact(cleanText)).Returns(cleanText);

        _voyageMock.Setup(v => v.GenerateEmbeddingAsync(cleanText, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                IsSuccess = true,
                Embedding = new float[] { 0.1f },
                Provider = "VoyageAI"
            });

        var provider = CreateProvider();
        await provider.GenerateEmbeddingAsync(cleanText);

        // PII redaction is ALWAYS called, even if no PII is found (defense in depth)
        _piiRedactorMock.Verify(p => p.Redact(cleanText), Times.Once);
    }
}
