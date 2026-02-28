using SentimentAnalyzer.API.Models;

namespace SentimentAnalyzer.API.Data;

/// <summary>
/// Repository interface for CX Copilot conversation session persistence.
/// Manages sliding-window message history for multi-turn conversations.
/// All stored messages are PII-redacted before persistence.
/// </summary>
public interface ICxConversationRepository
{
    /// <summary>
    /// Creates a new conversation session and returns the generated session ID.
    /// </summary>
    /// <returns>The GUID session ID for the new conversation.</returns>
    Task<string> CreateSessionAsync();

    /// <summary>
    /// Retrieves the most recent turns from a conversation session.
    /// Returns an empty list if the session does not exist.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="maxTurns">Maximum number of messages to return (default 10).</param>
    /// <returns>Ordered list of messages (oldest first), capped at maxTurns.</returns>
    Task<List<CxMessageRecord>> GetRecentTurnsAsync(string sessionId, int maxTurns = 10);

    /// <summary>
    /// Appends a message to the conversation session's history.
    /// Enforces the sliding window by trimming oldest messages when the limit is exceeded.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="role">Message role: "user" or "assistant".</param>
    /// <param name="content">PII-redacted message content.</param>
    /// <param name="maxTurns">Maximum messages retained in the window (default 10).</param>
    Task AppendTurnAsync(string sessionId, string role, string content, int maxTurns = 10);

    /// <summary>
    /// Checks whether a session with the given ID exists.
    /// </summary>
    /// <param name="sessionId">The session identifier to check.</param>
    /// <returns>True if the session exists, false otherwise.</returns>
    Task<bool> SessionExistsAsync(string sessionId);
}
