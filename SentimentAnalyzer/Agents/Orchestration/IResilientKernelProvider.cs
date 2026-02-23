using Microsoft.SemanticKernel;

namespace SentimentAnalyzer.Agents.Orchestration;

/// <summary>
/// Provides a Semantic Kernel instance with automatic provider fallback.
/// When the primary provider returns 429/500/503, the next provider in the
/// configured fallback chain is used. Implements exponential backoff cooldown.
/// </summary>
public interface IResilientKernelProvider
{
    /// <summary>
    /// Returns the currently healthy Kernel. If the primary is in cooldown,
    /// returns the next available Kernel in the fallback chain.
    /// Ollama (local) is always the last resort.
    /// </summary>
    /// <returns>A healthy Kernel instance.</returns>
    Kernel GetKernel();

    /// <summary>
    /// Reports a failure for the given provider, placing it in cooldown
    /// with exponential backoff (30s → 60s → 120s → 300s max).
    /// </summary>
    /// <param name="providerName">Name of the failed provider (e.g., "Groq").</param>
    /// <param name="ex">The exception that caused the failure.</param>
    void ReportFailure(string providerName, Exception ex);

    /// <summary>
    /// Returns the name of the currently active (primary healthy) provider.
    /// </summary>
    string ActiveProviderName { get; }

    /// <summary>
    /// Returns health status for all configured providers.
    /// </summary>
    IReadOnlyDictionary<string, ProviderHealthStatus> GetHealthStatus();
}

/// <summary>
/// Tracks the health and cooldown state of a single AI provider.
/// </summary>
public class ProviderHealthStatus
{
    /// <summary>Provider name (e.g., "Groq", "Mistral").</summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>Whether this provider is currently available (not in cooldown).</summary>
    public bool IsAvailable { get; set; } = true;

    /// <summary>When the last failure occurred (null if never failed).</summary>
    public DateTime? LastFailureUtc { get; set; }

    /// <summary>Number of consecutive failures.</summary>
    public int ConsecutiveFailures { get; set; }

    /// <summary>Current cooldown duration (increases with exponential backoff).</summary>
    public TimeSpan CooldownDuration { get; set; } = TimeSpan.Zero;

    /// <summary>When cooldown expires and provider should be retried.</summary>
    public DateTime? CooldownExpiresUtc { get; set; }
}
