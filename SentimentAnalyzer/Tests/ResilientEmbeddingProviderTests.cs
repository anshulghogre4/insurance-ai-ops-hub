using Microsoft.Extensions.Logging;
using Moq;
using SentimentAnalyzer.Agents.Orchestration;
using SentimentAnalyzer.API.Services.Embeddings;
using Xunit;

namespace SentimentAnalyzer.Tests;

/// <summary>
/// Tests for ResilientEmbeddingProvider.
/// Validates 6-provider fallback chain (VoyageAI -> Jina -> Cohere -> Gemini -> HuggingFace -> Ollama),
/// PII redaction, and per-provider cooldown behavior.
/// </summary>
public class ResilientEmbeddingProviderTests
{
    private readonly Mock<IEmbeddingService> _voyageMock = new();
    private readonly Mock<IEmbeddingService> _jinaMock = new();
    private readonly Mock<IEmbeddingService> _cohereMock = new();
    private readonly Mock<IEmbeddingService> _geminiMock = new();
    private readonly Mock<IEmbeddingService> _huggingFaceMock = new();
    private readonly Mock<IEmbeddingService> _ollamaMock = new();
    private readonly Mock<IPIIRedactor> _piiRedactorMock = new();
    private readonly Mock<ILogger<ResilientEmbeddingProvider>> _loggerMock = new();

    private ResilientEmbeddingProvider CreateProvider()
    {
        _voyageMock.Setup(v => v.EmbeddingDimension).Returns(1024);
        _voyageMock.Setup(v => v.ProviderName).Returns("VoyageAI");
        _jinaMock.Setup(j => j.EmbeddingDimension).Returns(1024);
        _jinaMock.Setup(j => j.ProviderName).Returns("Jina");
        _cohereMock.Setup(c => c.EmbeddingDimension).Returns(1024);
        _cohereMock.Setup(c => c.ProviderName).Returns("Cohere");
        _geminiMock.Setup(g => g.EmbeddingDimension).Returns(768);
        _geminiMock.Setup(g => g.ProviderName).Returns("GeminiEmbed");
        _huggingFaceMock.Setup(h => h.EmbeddingDimension).Returns(1024);
        _huggingFaceMock.Setup(h => h.ProviderName).Returns("HuggingFaceEmbed");
        _ollamaMock.Setup(o => o.EmbeddingDimension).Returns(1024);
        _ollamaMock.Setup(o => o.ProviderName).Returns("Ollama");

        // Default: intermediate providers fail (API key not configured) so tests still exercise Voyage -> Ollama path
        var notConfiguredResult = new EmbeddingResult
        {
            IsSuccess = false, Provider = "NotConfigured", ErrorMessage = "API key not configured"
        };
        _jinaMock.Setup(j => j.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(notConfiguredResult);
        _cohereMock.Setup(c => c.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(notConfiguredResult);
        _geminiMock.Setup(g => g.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(notConfiguredResult);
        _huggingFaceMock.Setup(h => h.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(notConfiguredResult);

        var notConfiguredBatch = new BatchEmbeddingResult
        {
            IsSuccess = false, Provider = "NotConfigured", ErrorMessage = "API key not configured"
        };
        _jinaMock.Setup(j => j.GenerateBatchEmbeddingsAsync(It.IsAny<string[]>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(notConfiguredBatch);
        _cohereMock.Setup(c => c.GenerateBatchEmbeddingsAsync(It.IsAny<string[]>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(notConfiguredBatch);
        _geminiMock.Setup(g => g.GenerateBatchEmbeddingsAsync(It.IsAny<string[]>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(notConfiguredBatch);
        _huggingFaceMock.Setup(h => h.GenerateBatchEmbeddingsAsync(It.IsAny<string[]>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(notConfiguredBatch);

        // Default PII redaction: pass-through (no PII detected)
        _piiRedactorMock.Setup(p => p.Redact(It.IsAny<string>())).Returns<string>(s => s);

        return new ResilientEmbeddingProvider(
            _voyageMock.Object,
            _jinaMock.Object,
            _cohereMock.Object,
            _geminiMock.Object,
            _huggingFaceMock.Object,
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

        // No fallback providers should be called
        _jinaMock.Verify(j => j.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
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
    public async Task GenerateEmbeddingAsync_VoyageFails_JinaSucceeds_ReturnsJinaResult()
    {
        var provider = CreateProvider();

        // Override Voyage to fail (after CreateProvider so it takes precedence)
        _voyageMock.Setup(v => v.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { IsSuccess = false, Provider = "VoyageAI", ErrorMessage = "429" });

        // Override default Jina failure with a success (after CreateProvider so it takes precedence)
        _jinaMock.Setup(j => j.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                IsSuccess = true,
                Embedding = new float[] { 0.7f, 0.8f, 0.9f },
                Provider = "Jina",
                TokensUsed = 15
            });

        var result = await provider.GenerateEmbeddingAsync(
            "Workers compensation claim for warehouse injury.");

        Assert.True(result.IsSuccess);
        Assert.Equal("Jina", result.Provider);

        // Ollama should NOT be called since Jina succeeded
        _ollamaMock.Verify(o => o.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
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

        // First call triggers Voyage failure -> cooldown -> falls through chain to Ollama
        var result1 = await provider.GenerateEmbeddingAsync("First claim analysis text.");
        Assert.Equal("Ollama", result1.Provider);

        // Second call should skip Voyage (cooldown) and go through chain to Ollama
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

        // Initially Voyage AI is active (last successful defaults to VoyageAI)
        Assert.Contains("VoyageAI", provider.ProviderName);
    }

    [Fact]
    public void EmbeddingDimension_ReflectsActiveProvider()
    {
        var provider = CreateProvider();

        Assert.Equal(1024, provider.EmbeddingDimension);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_AllProvidersFail_ReturnsAllFailedError()
    {
        _voyageMock.Setup(v => v.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { IsSuccess = false, Provider = "VoyageAI", ErrorMessage = "Voyage down" });

        _ollamaMock.Setup(o => o.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { IsSuccess = false, Provider = "Ollama", ErrorMessage = "Ollama not running" });

        var provider = CreateProvider();
        var result = await provider.GenerateEmbeddingAsync("Claim under investigation for potential fraud indicators.");

        Assert.False(result.IsSuccess);
        Assert.Contains("AllFailed", result.Provider);
        Assert.Contains("6", result.ErrorMessage);
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

    [Fact]
    public async Task GenerateEmbeddingAsync_MiddleProviderSucceeds_SkipsRemainingChain()
    {
        var provider = CreateProvider();

        // Override Voyage to fail (after CreateProvider so it takes precedence)
        _voyageMock.Setup(v => v.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { IsSuccess = false, Provider = "VoyageAI", ErrorMessage = "Rate limited" });

        // Jina fails (default from CreateProvider is preserved)
        // Cohere succeeds (override after CreateProvider so it takes precedence)
        _cohereMock.Setup(c => c.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                IsSuccess = true,
                Embedding = new float[] { 0.5f, 0.6f },
                Provider = "Cohere",
                TokensUsed = 8
            });

        var result = await provider.GenerateEmbeddingAsync(
            "Liability claim for slip and fall at insured premises.");

        Assert.True(result.IsSuccess);
        Assert.Equal("Cohere", result.Provider);

        // Gemini, HuggingFace, and Ollama should NOT be called (Cohere succeeded first)
        _geminiMock.Verify(g => g.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        _huggingFaceMock.Verify(h => h.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        _ollamaMock.Verify(o => o.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_PerProviderCooldown_OnlyAffectedProviderSkipped()
    {
        var provider = CreateProvider();

        // Override after CreateProvider: Voyage fails, Jina succeeds
        _voyageMock.Setup(v => v.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { IsSuccess = false, Provider = "VoyageAI", ErrorMessage = "429" });

        _jinaMock.Setup(j => j.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                IsSuccess = true,
                Embedding = new float[] { 0.3f },
                Provider = "Jina"
            });

        // Call 1: Voyage fails -> cooldown, Jina succeeds
        var result1 = await provider.GenerateEmbeddingAsync("First call.");
        Assert.Equal("Jina", result1.Provider);

        // Call 2: Voyage in cooldown (skipped), Jina still available
        var result2 = await provider.GenerateEmbeddingAsync("Second call.");
        Assert.Equal("Jina", result2.Provider);

        // Voyage should have been called only once (second call skipped due to cooldown)
        _voyageMock.Verify(v => v.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        // Jina should have been called twice (not in cooldown)
        _jinaMock.Verify(j => j.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // ==========================================
    // Feature 2: Full 6-provider chain validation
    // ==========================================

    [Fact]
    public async Task FallsThrough_SixProvider_Chain()
    {
        // All 5 cloud providers fail with various real-world error reasons, Ollama succeeds
        _voyageMock.Setup(v => v.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { IsSuccess = false, Provider = "VoyageAI", ErrorMessage = "Voyage AI 50M token quota exhausted" });
        _jinaMock.Setup(j => j.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { IsSuccess = false, Provider = "Jina", ErrorMessage = "Jina AI 1M token limit reached" });
        _cohereMock.Setup(c => c.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { IsSuccess = false, Provider = "Cohere", ErrorMessage = "Cohere trial 1000 req/month exhausted" });
        _geminiMock.Setup(g => g.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { IsSuccess = false, Provider = "GeminiEmbed", ErrorMessage = "Gemini 1500 req/day quota exceeded" });
        _huggingFaceMock.Setup(h => h.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { IsSuccess = false, Provider = "HuggingFaceEmbed", ErrorMessage = "HuggingFace model loading timeout (503)" });

        _ollamaMock.Setup(o => o.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                IsSuccess = true,
                Embedding = new float[] { 0.88f, 0.77f, 0.66f },
                Provider = "Ollama"
            });

        var provider = CreateProvider();
        var result = await provider.GenerateEmbeddingAsync(
            "Commercial auto fleet insurance renewal with 47 vehicles across three regional warehouses.");

        Assert.True(result.IsSuccess);
        Assert.Equal("Ollama", result.Provider);
        Assert.Equal(3, result.Dimension);
        Assert.Equal(0.88f, result.Embedding[0]);

        // Verify ALL 6 providers were attempted in fallback order
        _voyageMock.Verify(v => v.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        _jinaMock.Verify(j => j.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        _cohereMock.Verify(c => c.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        _geminiMock.Verify(g => g.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        _huggingFaceMock.Verify(h => h.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        _ollamaMock.Verify(o => o.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Respects_PerProvider_Cooldown()
    {
        var provider = CreateProvider();

        // Override after CreateProvider: Voyage and Jina fail, Cohere succeeds
        _voyageMock.Setup(v => v.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { IsSuccess = false, Provider = "VoyageAI", ErrorMessage = "Rate limit 429" });
        _jinaMock.Setup(j => j.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { IsSuccess = false, Provider = "Jina", ErrorMessage = "Rate limit 429" });
        _cohereMock.Setup(c => c.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                IsSuccess = true,
                Embedding = new float[] { 0.33f },
                Provider = "Cohere"
            });

        // First call: Voyage fails (enters cooldown) -> Jina fails (enters cooldown) -> Cohere succeeds
        var result1 = await provider.GenerateEmbeddingAsync(
            "Surety bond claim for construction project default by general contractor.");
        Assert.Equal("Cohere", result1.Provider);

        // Second call: Both Voyage AND Jina should be skipped (per-provider cooldown), go straight to Cohere
        var result2 = await provider.GenerateEmbeddingAsync(
            "Performance bond indemnity agreement for highway infrastructure project.");
        Assert.Equal("Cohere", result2.Provider);

        // Voyage and Jina each called once only (skipped on second call due to per-provider cooldown)
        _voyageMock.Verify(v => v.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        _jinaMock.Verify(j => j.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);

        // Cohere called twice (once per call, no cooldown since it succeeded both times)
        _cohereMock.Verify(c => c.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task All_Cloud_Fail_Falls_To_Ollama()
    {
        // All 5 cloud providers fail with different realistic error messages
        _voyageMock.Setup(v => v.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { IsSuccess = false, Provider = "VoyageAI", ErrorMessage = "API key not configured" });
        _jinaMock.Setup(j => j.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { IsSuccess = false, Provider = "Jina", ErrorMessage = "API key not configured" });
        _cohereMock.Setup(c => c.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { IsSuccess = false, Provider = "Cohere", ErrorMessage = "Free trial quota exhausted" });
        _geminiMock.Setup(g => g.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { IsSuccess = false, Provider = "GeminiEmbed", ErrorMessage = "Daily request limit exceeded" });
        _huggingFaceMock.Setup(h => h.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { IsSuccess = false, Provider = "HuggingFaceEmbed", ErrorMessage = "Model loading timeout (503)" });

        // Ollama (local) succeeds as the final fallback
        _ollamaMock.Setup(o => o.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                IsSuccess = true,
                Embedding = new float[] { 0.55f, 0.44f },
                Provider = "Ollama"
            });

        var provider = CreateProvider();
        var result = await provider.GenerateEmbeddingAsync(
            "Directors and officers liability insurance policy with fiduciary liability extension.");

        Assert.True(result.IsSuccess);
        Assert.Equal("Ollama", result.Provider);
        Assert.Equal(2, result.Dimension);
        Assert.Equal(0.55f, result.Embedding[0]);

        // Verify entire 6-provider chain was traversed before reaching Ollama
        _voyageMock.Verify(v => v.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        _jinaMock.Verify(j => j.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        _cohereMock.Verify(c => c.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        _geminiMock.Verify(g => g.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        _huggingFaceMock.Verify(h => h.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        _ollamaMock.Verify(o => o.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_AllCloudFail_FallsBackToOllama()
    {
        // All 5 cloud providers fail with different realistic errors, Ollama (local) succeeds
        _voyageMock.Setup(v => v.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { IsSuccess = false, Provider = "VoyageAI", ErrorMessage = "Rate limit exceeded (429)" });

        _jinaMock.Setup(j => j.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { IsSuccess = false, Provider = "Jina", ErrorMessage = "Free tier 1M token limit exhausted" });

        _cohereMock.Setup(c => c.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { IsSuccess = false, Provider = "Cohere", ErrorMessage = "Trial API key expired" });

        _geminiMock.Setup(g => g.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { IsSuccess = false, Provider = "GeminiEmbed", ErrorMessage = "Daily quota exceeded (1500 req/day)" });

        _huggingFaceMock.Setup(h => h.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { IsSuccess = false, Provider = "HuggingFaceEmbed", ErrorMessage = "Model loading timeout (503)" });

        _ollamaMock.Setup(o => o.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                IsSuccess = true,
                Embedding = new float[] { 0.22f, 0.44f, 0.66f },
                Provider = "Ollama",
                TokensUsed = 12
            });

        var provider = CreateProvider();
        var result = await provider.GenerateEmbeddingAsync(
            "Commercial property policy CLM-2024-88712 fire damage claim for insured warehouse at 1200 Industrial Blvd.");

        Assert.True(result.IsSuccess);
        Assert.Equal("Ollama", result.Provider);
        Assert.Equal(3, result.Dimension);

        // Verify all 6 providers were tried in order
        _voyageMock.Verify(v => v.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        _jinaMock.Verify(j => j.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        _cohereMock.Verify(c => c.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        _geminiMock.Verify(g => g.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        _huggingFaceMock.Verify(h => h.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        _ollamaMock.Verify(o => o.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_PerProviderCooldown_SkipsFailedProviders()
    {
        var provider = CreateProvider();

        // Call 1: Voyage fails, Jina fails, Cohere fails, Gemini succeeds
        // Call 2: Voyage/Jina/Cohere all in cooldown (skipped), Gemini succeeds again
        _voyageMock.Setup(v => v.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { IsSuccess = false, Provider = "VoyageAI", ErrorMessage = "429 Too Many Requests" });

        _jinaMock.Setup(j => j.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { IsSuccess = false, Provider = "Jina", ErrorMessage = "Free tier limit reached" });

        // Cohere fails on first call (then enters cooldown, so second call is skipped)
        _cohereMock.SetupSequence(c => c.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { IsSuccess = false, Provider = "Cohere", ErrorMessage = "Server error" })
            .ReturnsAsync(new EmbeddingResult
            {
                IsSuccess = true,
                Embedding = new float[] { 0.88f, 0.77f },
                Provider = "Cohere"
            });

        // Gemini succeeds consistently
        _geminiMock.Setup(g => g.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                IsSuccess = true,
                Embedding = new float[] { 0.55f, 0.66f },
                Provider = "GeminiEmbed"
            });

        // Call 1: Voyage fails, Jina fails, Cohere fails, Gemini succeeds
        var result1 = await provider.GenerateEmbeddingAsync(
            "Professional liability claim for insurance broker errors and omissions in policy placement.");
        Assert.True(result1.IsSuccess);
        Assert.Equal("GeminiEmbed", result1.Provider);

        // Call 2: Voyage (cooldown), Jina (cooldown), Cohere (cooldown), Gemini available -> succeeds
        var result2 = await provider.GenerateEmbeddingAsync(
            "Directors and officers liability coverage for regulatory investigation defense costs.");
        Assert.True(result2.IsSuccess);
        Assert.Equal("GeminiEmbed", result2.Provider);

        // Voyage called once (first call only, then cooldown)
        _voyageMock.Verify(v => v.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        // Jina called once (first call only, then cooldown)
        _jinaMock.Verify(j => j.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        // Cohere called once (first call only, then cooldown)
        _cohereMock.Verify(c => c.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        // Gemini called twice (succeeded both times, no cooldown applied)
        _geminiMock.Verify(g => g.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GenerateBatchEmbeddingsAsync_FallsThroughChain()
    {
        var provider = CreateProvider();

        // Override AFTER CreateProvider() so these take precedence over defaults
        // First 3 providers fail for batch, Gemini succeeds
        _voyageMock.Setup(v => v.GenerateBatchEmbeddingsAsync(It.IsAny<string[]>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BatchEmbeddingResult { IsSuccess = false, Provider = "VoyageAI", ErrorMessage = "Voyage batch rate limited" });

        _jinaMock.Setup(j => j.GenerateBatchEmbeddingsAsync(It.IsAny<string[]>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BatchEmbeddingResult { IsSuccess = false, Provider = "Jina", ErrorMessage = "Jina batch quota exceeded" });

        _cohereMock.Setup(c => c.GenerateBatchEmbeddingsAsync(It.IsAny<string[]>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BatchEmbeddingResult { IsSuccess = false, Provider = "Cohere", ErrorMessage = "Cohere trial key expired" });

        _geminiMock.Setup(g => g.GenerateBatchEmbeddingsAsync(It.IsAny<string[]>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BatchEmbeddingResult
            {
                IsSuccess = true,
                Embeddings = new float[][]
                {
                    new[] { 0.11f, 0.22f },
                    new[] { 0.33f, 0.44f },
                    new[] { 0.55f, 0.66f }
                },
                Provider = "GeminiEmbed",
                TotalTokensUsed = 0 // Gemini does not report token usage
            });
        var result = await provider.GenerateBatchEmbeddingsAsync(new[]
        {
            "Homeowners policy HO-3 special form coverage for dwelling and personal property.",
            "Inland marine floater endorsement for scheduled jewelry and fine arts coverage.",
            "Umbrella liability policy providing $2M excess over primary auto and homeowners policies."
        }, "document");

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Count);
        Assert.Equal(2, result.Dimension);
        Assert.Equal("GeminiEmbed", result.Provider);
        Assert.Equal(0.33f, result.Embeddings[1][0]);
        Assert.Equal(0.66f, result.Embeddings[2][1]);

        // Verify fallback chain was followed in order
        _voyageMock.Verify(v => v.GenerateBatchEmbeddingsAsync(It.IsAny<string[]>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        _jinaMock.Verify(j => j.GenerateBatchEmbeddingsAsync(It.IsAny<string[]>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        _cohereMock.Verify(c => c.GenerateBatchEmbeddingsAsync(It.IsAny<string[]>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        _geminiMock.Verify(g => g.GenerateBatchEmbeddingsAsync(It.IsAny<string[]>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);

        // HuggingFace and Ollama should NOT be called (Gemini succeeded)
        _huggingFaceMock.Verify(h => h.GenerateBatchEmbeddingsAsync(It.IsAny<string[]>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        _ollamaMock.Verify(o => o.GenerateBatchEmbeddingsAsync(It.IsAny<string[]>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
