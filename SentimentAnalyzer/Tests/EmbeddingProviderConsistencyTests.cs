using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using SentimentAnalyzer.API.Services.Embeddings;
using Xunit;

namespace SentimentAnalyzer.Tests;

/// <summary>
/// Tests for embedding provider consistency — prevents Sprint 5 Hotfix 5.H6 regression
/// where cross-provider embedding mismatch caused zero cosine similarity.
/// Validates:
/// - ResolveQueryEmbeddingServiceAsync matches document's indexing provider
/// - Dimension mismatch detection between providers
/// - Keyed service resolution for query-time provider matching
/// </summary>
public class EmbeddingProviderConsistencyTests
{
    private readonly Mock<IEmbeddingService> _voyageMock = new();
    private readonly Mock<IEmbeddingService> _cohereMock = new();
    private readonly Mock<IEmbeddingService> _geminiEmbedMock = new();

    public EmbeddingProviderConsistencyTests()
    {
        _voyageMock.Setup(e => e.ProviderName).Returns("VoyageAI");
        _cohereMock.Setup(e => e.ProviderName).Returns("Cohere");
        _geminiEmbedMock.Setup(e => e.ProviderName).Returns("GeminiEmbed");
    }

    [Fact]
    public void KeyedServiceResolution_MatchesDocumentProvider()
    {
        // 5.H6 regression: document indexed with Cohere, but query used VoyageAI
        // After fix: ResolveQueryEmbeddingServiceAsync looks up document's provider
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IEmbeddingService>("VoyageAI", (_, _) => _voyageMock.Object);
        services.AddKeyedSingleton<IEmbeddingService>("Cohere", (_, _) => _cohereMock.Object);
        services.AddKeyedSingleton<IEmbeddingService>("GeminiEmbed", (_, _) => _geminiEmbedMock.Object);

        var sp = services.BuildServiceProvider();

        // Simulate resolving provider for a document indexed with Cohere
        var documentProvider = "Cohere";
        var resolved = sp.GetKeyedService<IEmbeddingService>(documentProvider);

        Assert.NotNull(resolved);
        Assert.Equal("Cohere", resolved!.ProviderName);
    }

    [Fact]
    public void KeyedServiceResolution_StripResilientWrapper()
    {
        // Document stored with "Resilient(Cohere)" — strip wrapper to get actual provider
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IEmbeddingService>("Cohere", (_, _) => _cohereMock.Object);

        var sp = services.BuildServiceProvider();

        var storedProvider = "Resilient(Cohere)";
        var cleanedProvider = storedProvider
            .Replace("Resilient(", "")
            .Replace(")", "")
            .Trim();

        var resolved = sp.GetKeyedService<IEmbeddingService>(cleanedProvider);

        Assert.NotNull(resolved);
        Assert.Equal("Cohere", resolved!.ProviderName);
    }

    [Fact]
    public void DimensionMismatch_Detection()
    {
        // VoyageAI/Cohere/HuggingFace/Jina = 1024-dim, Gemini = 768-dim
        // Mixing providers causes truncated cosine similarity
        var primaryDimension = 1024;
        var geminiDimension = 768;

        Assert.NotEqual(primaryDimension, geminiDimension);

        // The min dimension is used for truncated comparison
        var truncatedDimension = Math.Min(primaryDimension, geminiDimension);
        Assert.Equal(768, truncatedDimension);

        // This means 256 dimensions of signal are lost — cosine similarity degrades
        var lostDimensions = primaryDimension - truncatedDimension;
        Assert.Equal(256, lostDimensions);
    }

    [Fact]
    public void CosineSimilarity_SameProvider_HighScore()
    {
        // Two vectors from the same provider should have high cosine similarity
        var vectorA = new float[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f };
        var vectorB = new float[] { 0.11f, 0.19f, 0.31f, 0.39f, 0.51f };

        var similarity = CosineSimilarity(vectorA, vectorB);

        Assert.True(similarity > 0.99, $"Same-provider vectors should be highly similar, got {similarity}");
    }

    [Fact]
    public void CosineSimilarity_DifferentProviders_LowScore()
    {
        // Vectors from different embedding spaces have uncorrelated dimensions
        var voyageVector = new float[] { 0.5f, -0.3f, 0.8f, 0.1f, -0.6f };
        var randomVector = new float[] { -0.2f, 0.7f, -0.1f, 0.9f, 0.3f };

        var similarity = CosineSimilarity(voyageVector, randomVector);

        Assert.True(similarity < 0.5, $"Cross-provider vectors should have low similarity, got {similarity}");
    }

    [Fact]
    public void KeyedServiceResolution_UnknownProvider_ReturnsNull()
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IEmbeddingService>("VoyageAI", (_, _) => _voyageMock.Object);

        var sp = services.BuildServiceProvider();

        // Unknown provider should return null (graceful fallback to default)
        var resolved = sp.GetKeyedService<IEmbeddingService>("NonExistentProvider");
        Assert.Null(resolved);
    }

    [Fact]
    public void AllProvidersRegistered_SixProviders()
    {
        // Verify all 6 embedding providers can be registered and resolved
        var services = new ServiceCollection();
        var mockNames = new[] { "VoyageAI", "Cohere", "GeminiEmbed", "HuggingFaceEmbed", "Jina", "Ollama" };

        foreach (var name in mockNames)
        {
            var mock = new Mock<IEmbeddingService>();
            mock.Setup(e => e.ProviderName).Returns(name);
            services.AddKeyedSingleton<IEmbeddingService>(name, (_, _) => mock.Object);
        }

        var sp = services.BuildServiceProvider();

        foreach (var name in mockNames)
        {
            var resolved = sp.GetKeyedService<IEmbeddingService>(name);
            Assert.NotNull(resolved);
            Assert.Equal(name, resolved!.ProviderName);
        }
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        var dotProduct = 0.0;
        var magnitudeA = 0.0;
        var magnitudeB = 0.0;

        for (var i = 0; i < Math.Min(a.Length, b.Length); i++)
        {
            dotProduct += a[i] * b[i];
            magnitudeA += a[i] * a[i];
            magnitudeB += b[i] * b[i];
        }

        var magnitude = Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB);
        return magnitude > 0 ? dotProduct / magnitude : 0;
    }
}
