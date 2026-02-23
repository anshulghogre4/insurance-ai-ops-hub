using SentimentAnalyzer.Agents.Configuration;
using SentimentAnalyzer.Agents.Orchestration;
using Xunit;

namespace SentimentAnalyzer.Tests;

/// <summary>
/// Tests for provider configuration classes — verifies default values,
/// fallback chain parsing, and settings class structure.
/// </summary>
public class ProviderConfigurationTests
{
    // ──────────────────────────────────────────
    // Default values
    // ──────────────────────────────────────────

    [Fact]
    public void AgentSystemSettings_DefaultProvider_IsGroq()
    {
        var settings = new AgentSystemSettings();
        Assert.Equal("Groq", settings.Provider);
    }

    [Fact]
    public void AgentSystemSettings_DefaultFallbackChain_HasFiveProviders()
    {
        var settings = new AgentSystemSettings();
        Assert.Equal(5, settings.FallbackChain.Count);
    }

    [Fact]
    public void AgentSystemSettings_DefaultFallbackChain_StartsWithGroq_EndsWithOllama()
    {
        var settings = new AgentSystemSettings();
        Assert.Equal("Groq", settings.FallbackChain[0]);
        Assert.Equal("Ollama", settings.FallbackChain[^1]);
    }

    [Fact]
    public void AgentSystemSettings_DefaultFallbackChain_CorrectOrder()
    {
        var settings = new AgentSystemSettings();
        var expected = new[] { "Groq", "Mistral", "Gemini", "OpenRouter", "Ollama" };
        Assert.Equal(expected, settings.FallbackChain);
    }

    // ──────────────────────────────────────────
    // Sub-settings default initialization
    // ──────────────────────────────────────────

    [Fact]
    public void AgentSystemSettings_AllSubSettings_AreNotNull()
    {
        var settings = new AgentSystemSettings();
        Assert.NotNull(settings.Groq);
        Assert.NotNull(settings.Ollama);
        Assert.NotNull(settings.Gemini);
        Assert.NotNull(settings.Mistral);
        Assert.NotNull(settings.OpenRouter);
        Assert.NotNull(settings.Deepgram);
        Assert.NotNull(settings.Cloudflare);
        Assert.NotNull(settings.AzureVision);
        Assert.NotNull(settings.OcrSpace);
        Assert.NotNull(settings.HuggingFace);
    }

    // ──────────────────────────────────────────
    // Provider-specific defaults
    // ──────────────────────────────────────────

    [Fact]
    public void GroqSettings_Defaults_AreCorrect()
    {
        var settings = new GroqSettings();
        Assert.Equal(string.Empty, settings.ApiKey);
        Assert.Equal("https://api.groq.com/openai/v1", settings.Endpoint);
        Assert.Equal("llama-3.3-70b-versatile", settings.Model);
    }

    [Fact]
    public void OllamaSettings_Defaults_AreCorrect()
    {
        var settings = new OllamaSettings();
        Assert.Equal("http://localhost:11434", settings.Endpoint);
        Assert.Equal("llama3.2", settings.Model);
    }

    [Fact]
    public void GeminiSettings_Defaults_AreCorrect()
    {
        var settings = new GeminiSettings();
        Assert.Equal(string.Empty, settings.ApiKey);
        Assert.Equal("gemini-2.5-flash", settings.Model);
    }

    [Fact]
    public void MistralSettings_Defaults_AreCorrect()
    {
        var settings = new MistralSettings();
        Assert.Equal(string.Empty, settings.ApiKey);
        Assert.Equal("https://api.mistral.ai/v1", settings.Endpoint);
        Assert.Equal("mistral-large-latest", settings.Model);
    }

    [Fact]
    public void OpenRouterSettings_Defaults_AreCorrect()
    {
        var settings = new OpenRouterSettings();
        Assert.Equal(string.Empty, settings.ApiKey);
        Assert.Equal("https://openrouter.ai/api/v1", settings.Endpoint);
        Assert.Equal("deepseek/deepseek-r1:free", settings.Model);
        Assert.Equal("http://localhost:5143", settings.SiteUrl);
    }

    [Fact]
    public void DeepgramSettings_Defaults_AreCorrect()
    {
        var settings = new DeepgramSettings();
        Assert.Equal(string.Empty, settings.ApiKey);
        Assert.Equal("https://api.deepgram.com/v1", settings.Endpoint);
        Assert.Equal("nova-2", settings.Model);
    }

    [Fact]
    public void CloudflareSettings_Defaults_AreCorrect()
    {
        var settings = new CloudflareSettings();
        Assert.Equal(string.Empty, settings.ApiKey);
        Assert.Equal(string.Empty, settings.AccountId);
        Assert.Equal("@cf/meta/llama-4-scout-17b-16e-instruct", settings.VisionModel);
    }

    [Fact]
    public void AzureVisionSettings_Defaults_AreCorrect()
    {
        var settings = new AzureVisionSettings();
        Assert.Equal(string.Empty, settings.ApiKey);
        Assert.Equal(string.Empty, settings.Endpoint);
    }

    [Fact]
    public void OcrSpaceSettings_Defaults_AreCorrect()
    {
        var settings = new OcrSpaceSettings();
        Assert.Equal(string.Empty, settings.ApiKey);
        Assert.Equal("https://api.ocr.space/parse/image", settings.Endpoint);
    }

    [Fact]
    public void HuggingFaceSettings_Defaults_AreCorrect()
    {
        var settings = new HuggingFaceSettings();
        Assert.Equal(string.Empty, settings.ApiKey);
        Assert.Equal("dslim/bert-base-NER", settings.NerModel);
    }

    // ──────────────────────────────────────────
    // API key missing detection (used by ResilientKernelProvider)
    // ──────────────────────────────────────────

    [Fact]
    public void ProviderWithEmptyApiKey_IsDetectedAsMissing()
    {
        var groq = new GroqSettings { ApiKey = "" };
        Assert.True(string.IsNullOrWhiteSpace(groq.ApiKey));
    }

    [Fact]
    public void ProviderWithWhitespaceApiKey_IsDetectedAsMissing()
    {
        var groq = new GroqSettings { ApiKey = "   " };
        Assert.True(string.IsNullOrWhiteSpace(groq.ApiKey));
    }

    [Fact]
    public void ProviderWithValidApiKey_IsNotMissing()
    {
        var groq = new GroqSettings { ApiKey = "gsk_test_key_123" };
        Assert.False(string.IsNullOrWhiteSpace(groq.ApiKey));
    }

    // ──────────────────────────────────────────
    // ProviderHealthStatus model tests
    // ──────────────────────────────────────────

    [Fact]
    public void ProviderHealthStatus_Defaults_AreAvailable()
    {
        var status = new ProviderHealthStatus();
        Assert.True(status.IsAvailable);
        Assert.Equal(0, status.ConsecutiveFailures);
        Assert.Null(status.LastFailureUtc);
        Assert.Null(status.CooldownExpiresUtc);
        Assert.Equal(TimeSpan.Zero, status.CooldownDuration);
    }

    [Fact]
    public void ProviderHealthStatus_AfterFailure_TracksCooldown()
    {
        var status = new ProviderHealthStatus
        {
            ProviderName = "Groq",
            IsAvailable = false,
            ConsecutiveFailures = 2,
            LastFailureUtc = DateTime.UtcNow,
            CooldownDuration = TimeSpan.FromSeconds(60),
            CooldownExpiresUtc = DateTime.UtcNow.AddSeconds(60)
        };

        Assert.False(status.IsAvailable);
        Assert.Equal(2, status.ConsecutiveFailures);
        Assert.Equal(60, status.CooldownDuration.TotalSeconds);
        Assert.NotNull(status.CooldownExpiresUtc);
    }
}
