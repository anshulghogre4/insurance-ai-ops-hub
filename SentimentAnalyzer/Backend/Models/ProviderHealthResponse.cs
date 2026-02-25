namespace SentimentAnalyzer.API.Models;

/// <summary>
/// Response model for the provider health monitoring endpoint.
/// Shows status of all LLM providers and multimodal services.
/// </summary>
public class ProviderHealthResponse
{
    /// <summary>Health status of LLM providers in the fallback chain.</summary>
    public List<LlmProviderHealth> LlmProviders { get; set; } = [];

    /// <summary>Health status of multimodal services.</summary>
    public List<ServiceHealth> MultimodalServices { get; set; } = [];

    /// <summary>When this health check was performed.</summary>
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Health status of a single LLM provider.
/// </summary>
public class LlmProviderHealth
{
    /// <summary>Provider name (Groq, Mistral, Gemini, OpenRouter, Ollama).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Current status: Healthy, Degraded, Down.</summary>
    public string Status { get; set; } = "Healthy";

    /// <summary>Whether this provider is currently available.</summary>
    public bool IsAvailable { get; set; } = true;

    /// <summary>Number of consecutive failures.</summary>
    public int ConsecutiveFailures { get; set; }

    /// <summary>Current cooldown duration in seconds.</summary>
    public double CooldownSeconds { get; set; }

    /// <summary>When the cooldown expires (null if not in cooldown).</summary>
    public DateTime? CooldownExpiresUtc { get; set; }
}

/// <summary>
/// Health status of a multimodal service.
/// </summary>
public class ServiceHealth
{
    /// <summary>Service name (Deepgram, AzureVision, CloudflareVision, OcrSpace, HuggingFace).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Whether the service is configured (API key present).</summary>
    public bool IsConfigured { get; set; }

    /// <summary>Current status: Available, NotConfigured.</summary>
    public string Status { get; set; } = "Available";
}
