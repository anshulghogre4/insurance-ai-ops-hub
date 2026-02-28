using SentimentAnalyzer.API.Models;

namespace SentimentAnalyzer.API.Services.CustomerExperience;

/// <summary>
/// Service for AI-powered customer experience copilot.
/// Provides context-aware insurance Q&amp;A with optional claim/policy context.
/// Uses direct kernel access for low-latency single-turn chat and
/// streaming chat completion for SSE-based multi-turn interactions.
/// Supports optional session-based conversation memory for multi-turn context.
/// </summary>
public interface ICustomerExperienceService
{
    /// <summary>
    /// Single-turn Q&amp;A using direct kernel for low latency.
    /// PII is redacted before the external AI call.
    /// When sessionId is provided, conversation history is loaded and appended.
    /// </summary>
    /// <param name="message">The customer's message or question.</param>
    /// <param name="claimContext">Optional claim/policy context to ground the response.</param>
    /// <param name="sessionId">Optional session ID for conversation memory.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A complete customer experience response with tone and escalation analysis.</returns>
    Task<CustomerExperienceResponse> ChatAsync(string message, string? claimContext = null, string? sessionId = null, CancellationToken ct = default);

    /// <summary>
    /// Streaming chat via SSE using kernel streaming chat completion.
    /// Yields content chunks as they arrive, followed by a final metadata chunk.
    /// PII is redacted before the external AI call.
    /// When sessionId is provided, conversation history is loaded and appended.
    /// </summary>
    /// <param name="message">The customer's message or question.</param>
    /// <param name="claimContext">Optional claim/policy context to ground the response.</param>
    /// <param name="sessionId">Optional session ID for conversation memory.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An async enumerable of stream chunks (content tokens, metadata, done).</returns>
    IAsyncEnumerable<CustomerExperienceStreamChunk> StreamChatAsync(string message, string? claimContext = null, string? sessionId = null, CancellationToken ct = default);
}
