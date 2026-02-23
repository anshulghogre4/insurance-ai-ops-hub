using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SentimentAnalyzer.Agents.Configuration;
using SentimentAnalyzer.Agents.Orchestration;
using SentimentAnalyzer.API.Services.Providers;
using Xunit;

namespace SentimentAnalyzer.Tests;

/// <summary>
/// Tests for ResilientKernelProvider — verifies fallback chain behavior,
/// cooldown management, exponential backoff, and health status reporting.
/// Uses Ollama as the only provider since it requires no API key.
/// </summary>
public class ResilientKernelProviderTests
{
    private readonly Mock<ILogger<ResilientKernelProvider>> _loggerMock = new();

    /// <summary>
    /// Creates a ResilientKernelProvider with configurable settings.
    /// By default uses Ollama-only chain since it needs no API key.
    /// </summary>
    private ResilientKernelProvider CreateProvider(
        List<string>? fallbackChain = null,
        string? groqKey = null,
        string? mistralKey = null,
        string? geminiKey = null)
    {
        var settings = new AgentSystemSettings
        {
            Provider = "Ollama",
            FallbackChain = fallbackChain ?? ["Ollama"],
            Groq = new GroqSettings { ApiKey = groqKey ?? string.Empty },
            Ollama = new OllamaSettings(),
            Gemini = new GeminiSettings { ApiKey = geminiKey ?? string.Empty },
            Mistral = new MistralSettings { ApiKey = mistralKey ?? string.Empty },
            OpenRouter = new OpenRouterSettings()
        };

        var optionsMock = new Mock<IOptions<AgentSystemSettings>>();
        optionsMock.Setup(o => o.Value).Returns(settings);

        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["OpenAI:ApiKey"]).Returns(string.Empty);
        configMock.Setup(c => c["OpenAI:Model"]).Returns("gpt-4o-mini");

        return new ResilientKernelProvider(optionsMock.Object, configMock.Object, _loggerMock.Object);
    }

    // ──────────────────────────────────────────
    // Construction tests
    // ──────────────────────────────────────────

    [Fact]
    public void Constructor_WithOllamaOnly_Succeeds()
    {
        var provider = CreateProvider();
        Assert.NotNull(provider);
    }

    [Fact]
    public void Constructor_WithNoProviders_ThrowsInvalidOperation()
    {
        // All providers in chain have empty API keys and chain only has cloud providers
        var settings = new AgentSystemSettings
        {
            Provider = "Groq",
            FallbackChain = ["Groq", "Mistral"],
            Groq = new GroqSettings { ApiKey = "" },
            Mistral = new MistralSettings { ApiKey = "" }
        };

        var optionsMock = new Mock<IOptions<AgentSystemSettings>>();
        optionsMock.Setup(o => o.Value).Returns(settings);

        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["OpenAI:ApiKey"]).Returns(string.Empty);
        configMock.Setup(c => c["OpenAI:Model"]).Returns("gpt-4o-mini");

        Assert.Throws<InvalidOperationException>(() =>
            new ResilientKernelProvider(optionsMock.Object, configMock.Object, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_NullSettings_ThrowsArgumentNull()
    {
        var configMock = new Mock<IConfiguration>();
        var optionsMock = new Mock<IOptions<AgentSystemSettings>>();
        optionsMock.Setup(o => o.Value).Returns((AgentSystemSettings)null!);

        Assert.Throws<ArgumentNullException>(() =>
            new ResilientKernelProvider(optionsMock.Object, configMock.Object, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNull()
    {
        var optionsMock = new Mock<IOptions<AgentSystemSettings>>();
        optionsMock.Setup(o => o.Value).Returns(new AgentSystemSettings());
        var configMock = new Mock<IConfiguration>();

        Assert.Throws<ArgumentNullException>(() =>
            new ResilientKernelProvider(optionsMock.Object, configMock.Object, null!));
    }

    // ──────────────────────────────────────────
    // GetKernel tests
    // ──────────────────────────────────────────

    [Fact]
    public void GetKernel_WithHealthyProvider_ReturnsKernel()
    {
        var provider = CreateProvider();
        var kernel = provider.GetKernel();
        Assert.NotNull(kernel);
    }

    [Fact]
    public void GetKernel_CalledMultipleTimes_ReturnsSameKernel()
    {
        var provider = CreateProvider();
        var kernel1 = provider.GetKernel();
        var kernel2 = provider.GetKernel();
        Assert.Same(kernel1, kernel2);
    }

    // ──────────────────────────────────────────
    // ActiveProviderName tests
    // ──────────────────────────────────────────

    [Fact]
    public void ActiveProviderName_WithOllama_ReturnsOllama()
    {
        var provider = CreateProvider();
        Assert.Equal("Ollama", provider.ActiveProviderName);
    }

    // ──────────────────────────────────────────
    // Health status tests
    // ──────────────────────────────────────────

    [Fact]
    public void GetHealthStatus_InitialState_AllAvailable()
    {
        var provider = CreateProvider();
        var health = provider.GetHealthStatus();

        Assert.Single(health);
        Assert.True(health["Ollama"].IsAvailable);
        Assert.Equal(0, health["Ollama"].ConsecutiveFailures);
    }

    [Fact]
    public void GetHealthStatus_ReturnsSnapshot_NotLiveReference()
    {
        var provider = CreateProvider();
        var health1 = provider.GetHealthStatus();
        var health2 = provider.GetHealthStatus();

        // Should be different object references (snapshots)
        Assert.NotSame(health1, health2);
    }

    // ──────────────────────────────────────────
    // ReportFailure + cooldown tests
    // ──────────────────────────────────────────

    [Fact]
    public void ReportFailure_MarksProviderUnavailable()
    {
        var provider = CreateProvider();
        provider.ReportFailure("Ollama", new HttpRequestException("Connection refused"));

        var health = provider.GetHealthStatus();
        Assert.False(health["Ollama"].IsAvailable);
        Assert.Equal(1, health["Ollama"].ConsecutiveFailures);
    }

    [Fact]
    public void ReportFailure_SetsCooldownExpiry()
    {
        var provider = CreateProvider();
        var beforeFailure = DateTime.UtcNow;

        provider.ReportFailure("Ollama", new HttpRequestException("Service unavailable"));

        var health = provider.GetHealthStatus();
        Assert.NotNull(health["Ollama"].CooldownExpiresUtc);
        Assert.True(health["Ollama"].CooldownExpiresUtc > beforeFailure);
    }

    [Fact]
    public void ReportFailure_FirstFailure_CooldownIs30Seconds()
    {
        var provider = CreateProvider();
        provider.ReportFailure("Ollama", new HttpRequestException("429 Too Many Requests"));

        var health = provider.GetHealthStatus();
        Assert.Equal(30, health["Ollama"].CooldownDuration.TotalSeconds);
    }

    [Fact]
    public void ReportFailure_SecondFailure_CooldownIs60Seconds()
    {
        var provider = CreateProvider();
        provider.ReportFailure("Ollama", new HttpRequestException("429"));
        provider.ReportFailure("Ollama", new HttpRequestException("429"));

        var health = provider.GetHealthStatus();
        Assert.Equal(60, health["Ollama"].CooldownDuration.TotalSeconds);
    }

    [Fact]
    public void ReportFailure_ThirdFailure_CooldownIs120Seconds()
    {
        var provider = CreateProvider();
        provider.ReportFailure("Ollama", new HttpRequestException("error"));
        provider.ReportFailure("Ollama", new HttpRequestException("error"));
        provider.ReportFailure("Ollama", new HttpRequestException("error"));

        var health = provider.GetHealthStatus();
        Assert.Equal(120, health["Ollama"].CooldownDuration.TotalSeconds);
    }

    [Fact]
    public void ReportFailure_ExponentialBackoff_CapsAt300Seconds()
    {
        var provider = CreateProvider();

        // 5+ failures should hit the 300s cap
        for (int i = 0; i < 10; i++)
            provider.ReportFailure("Ollama", new HttpRequestException("error"));

        var health = provider.GetHealthStatus();
        Assert.Equal(300, health["Ollama"].CooldownDuration.TotalSeconds);
    }

    [Fact]
    public void ReportFailure_UnknownProvider_IsIgnored()
    {
        var provider = CreateProvider();
        // Should not throw
        provider.ReportFailure("NonExistentProvider", new Exception("test"));

        // Original health unchanged
        var health = provider.GetHealthStatus();
        Assert.True(health["Ollama"].IsAvailable);
    }

    // ──────────────────────────────────────────
    // Fallback chain with multiple providers
    // ──────────────────────────────────────────

    [Fact]
    public void MultipleProviders_GroqUnavailable_FallsBackToOllama()
    {
        var provider = CreateProvider(
            fallbackChain: ["Groq", "Ollama"],
            groqKey: "gsk_test_key_12345678901234567890");

        // Initially should use Groq (first in chain)
        Assert.Equal("Groq", provider.ActiveProviderName);

        // Report Groq failure
        provider.ReportFailure("Groq", new HttpRequestException("429 Rate Limited"));

        // Should now fall back to Ollama
        Assert.Equal("Ollama", provider.ActiveProviderName);
    }

    [Fact]
    public void MultipleProviders_AllInCooldown_ForcesLastProvider()
    {
        var provider = CreateProvider(
            fallbackChain: ["Groq", "Ollama"],
            groqKey: "gsk_test_key_12345678901234567890");

        // Put both providers in cooldown
        provider.ReportFailure("Groq", new HttpRequestException("429"));
        provider.ReportFailure("Ollama", new HttpRequestException("Connection refused"));

        // GetKernel should still return something (forces last provider)
        var kernel = provider.GetKernel();
        Assert.NotNull(kernel);
    }

    [Fact]
    public void MultipleProviders_HealthStatus_ShowsCorrectCount()
    {
        var provider = CreateProvider(
            fallbackChain: ["Groq", "Ollama"],
            groqKey: "gsk_test_key_12345678901234567890");

        var health = provider.GetHealthStatus();
        Assert.Equal(2, health.Count);
        Assert.True(health.ContainsKey("Groq"));
        Assert.True(health.ContainsKey("Ollama"));
    }

    // ──────────────────────────────────────────
    // Skipping unconfigured providers
    // ──────────────────────────────────────────

    [Fact]
    public void Constructor_SkipsProvidersWithoutApiKeys()
    {
        // Include Mistral in chain but no API key — should be skipped
        var provider = CreateProvider(
            fallbackChain: ["Mistral", "Ollama"],
            mistralKey: "");

        var health = provider.GetHealthStatus();
        Assert.Single(health); // Only Ollama
        Assert.True(health.ContainsKey("Ollama"));
        Assert.False(health.ContainsKey("Mistral"));
    }
}
