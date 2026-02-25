using Microsoft.Extensions.Configuration;
using Moq;
using SentimentAnalyzer.Agents.Orchestration;
using SentimentAnalyzer.API.Features.Health.Queries;
using Xunit;

namespace SentimentAnalyzer.Tests;

/// <summary>
/// Tests for provider health query handler.
/// </summary>
public class ProviderHealthTests
{
    private readonly Mock<IResilientKernelProvider> _mockProvider;
    private readonly IConfiguration _config;

    public ProviderHealthTests()
    {
        _mockProvider = new Mock<IResilientKernelProvider>();
        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AgentSystem:Deepgram:ApiKey"] = "test-key",
                ["AgentSystem:AzureVision:ApiKey"] = "test-key",
                ["AgentSystem:OcrSpace:ApiKey"] = "test-key",
                ["AgentSystem:HuggingFace:ApiKey"] = "test-key"
            })
            .Build();
    }

    [Fact]
    public async Task GetProviderHealth_ReturnsAllProviders()
    {
        // Arrange
        _mockProvider.Setup(p => p.GetHealthStatus())
            .Returns(new Dictionary<string, ProviderHealthStatus>
            {
                ["Groq"] = new() { ProviderName = "Groq", IsAvailable = true, ConsecutiveFailures = 0 },
                ["Gemini"] = new() { ProviderName = "Gemini", IsAvailable = true, ConsecutiveFailures = 0 },
                ["Ollama"] = new() { ProviderName = "Ollama", IsAvailable = true, ConsecutiveFailures = 0 }
            });

        var handler = new GetProviderHealthHandler(_mockProvider.Object, _config);

        // Act
        var result = await handler.Handle(new GetProviderHealthQuery(), CancellationToken.None);

        // Assert
        Assert.Equal(3, result.LlmProviders.Count);
        Assert.All(result.LlmProviders, p => Assert.Equal("Healthy", p.Status));
        Assert.All(result.LlmProviders, p => Assert.True(p.IsAvailable));
    }

    [Fact]
    public async Task GetProviderHealth_DegradedProvider_ShowsCorrectStatus()
    {
        // Arrange
        _mockProvider.Setup(p => p.GetHealthStatus())
            .Returns(new Dictionary<string, ProviderHealthStatus>
            {
                ["Groq"] = new() { ProviderName = "Groq", IsAvailable = false, ConsecutiveFailures = 2, CooldownDuration = TimeSpan.FromSeconds(30) },
                ["Gemini"] = new() { ProviderName = "Gemini", IsAvailable = true, ConsecutiveFailures = 0 }
            });

        var handler = new GetProviderHealthHandler(_mockProvider.Object, _config);

        // Act
        var result = await handler.Handle(new GetProviderHealthQuery(), CancellationToken.None);

        // Assert
        var groq = result.LlmProviders.First(p => p.Name == "Groq");
        Assert.Equal("Degraded", groq.Status);
        Assert.False(groq.IsAvailable);
        Assert.Equal(30, groq.CooldownSeconds);
    }

    [Fact]
    public async Task GetProviderHealth_DownProvider_ShowsDownStatus()
    {
        // Arrange
        _mockProvider.Setup(p => p.GetHealthStatus())
            .Returns(new Dictionary<string, ProviderHealthStatus>
            {
                ["Groq"] = new() { ProviderName = "Groq", IsAvailable = false, ConsecutiveFailures = 5, CooldownDuration = TimeSpan.FromSeconds(120) }
            });

        var handler = new GetProviderHealthHandler(_mockProvider.Object, _config);

        // Act
        var result = await handler.Handle(new GetProviderHealthQuery(), CancellationToken.None);

        // Assert
        var groq = result.LlmProviders.First(p => p.Name == "Groq");
        Assert.Equal("Down", groq.Status);
    }

    [Fact]
    public async Task GetProviderHealth_IncludesMultimodalServices()
    {
        // Arrange
        _mockProvider.Setup(p => p.GetHealthStatus())
            .Returns(new Dictionary<string, ProviderHealthStatus>());

        var handler = new GetProviderHealthHandler(_mockProvider.Object, _config);

        // Act
        var result = await handler.Handle(new GetProviderHealthQuery(), CancellationToken.None);

        // Assert
        Assert.Equal(6, result.MultimodalServices.Count);
        var deepgram = result.MultimodalServices.First(s => s.Name.Contains("Deepgram"));
        Assert.True(deepgram.IsConfigured);
        Assert.Equal("Available", deepgram.Status);
    }

    [Fact]
    public async Task GetProviderHealth_UnconfiguredService_ShowsNotConfigured()
    {
        // Arrange — config with no CloudflareVision key
        _mockProvider.Setup(p => p.GetHealthStatus())
            .Returns(new Dictionary<string, ProviderHealthStatus>());

        var handler = new GetProviderHealthHandler(_mockProvider.Object, _config);

        // Act
        var result = await handler.Handle(new GetProviderHealthQuery(), CancellationToken.None);

        // Assert
        var cloudflare = result.MultimodalServices.First(s => s.Name.Contains("Cloudflare"));
        Assert.False(cloudflare.IsConfigured);
        Assert.Equal("NotConfigured", cloudflare.Status);
    }
}
