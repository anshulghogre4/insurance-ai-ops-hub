namespace SentimentAnalyzer.API.Models;

/// <summary>
/// Request body for the Customer Experience Copilot chat endpoint.
/// </summary>
public class CustomerExperienceRequest
{
    /// <summary>The customer's message or question.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Optional claim or policy context to ground the response (e.g., "Claim CLM-2024-78901, water damage, pending 3 weeks").</summary>
    public string? ClaimContext { get; set; }

    /// <summary>Optional session ID for conversation continuity. If null, a stateless single-turn chat is performed.</summary>
    public string? SessionId { get; set; }
}

/// <summary>
/// Response returned when creating a new CX Copilot conversation session.
/// </summary>
public class CxSessionResponse
{
    /// <summary>The unique session identifier (GUID) for this conversation.</summary>
    public string SessionId { get; set; } = string.Empty;
}

/// <summary>
/// Response containing the full message history for a conversation session.
/// </summary>
public class CxMessageHistoryResponse
{
    /// <summary>The session identifier.</summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>Ordered list of messages in the conversation (oldest first).</summary>
    public List<CxMessageRecord> Messages { get; set; } = [];
}

/// <summary>
/// A single message in a CX Copilot conversation history.
/// </summary>
public class CxMessageRecord
{
    /// <summary>The role of the message author: "user" or "assistant".</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>The PII-redacted message content.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>UTC timestamp of when the message was recorded.</summary>
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Response from the Customer Experience Copilot for non-streaming chat.
/// </summary>
public class CustomerExperienceResponse
{
    /// <summary>The AI-generated response text.</summary>
    public string Response { get; set; } = string.Empty;

    /// <summary>Detected tone of the response: Professional, Empathetic, Urgent, or Informational.</summary>
    public string Tone { get; set; } = "Professional";

    /// <summary>Whether the AI recommends escalating this interaction to a human agent.</summary>
    public bool EscalationRecommended { get; set; }

    /// <summary>Reason for escalation recommendation, if applicable.</summary>
    public string? EscalationReason { get; set; }

    /// <summary>The LLM provider that generated the response (e.g., "Groq", "Gemini").</summary>
    public string LlmProvider { get; set; } = string.Empty;

    /// <summary>Total elapsed time in milliseconds for the chat completion.</summary>
    public long ElapsedMilliseconds { get; set; }

    /// <summary>Regulatory disclaimer appended to all AI-generated insurance responses. Required for compliance.</summary>
    public string? Disclaimer { get; set; }
}

/// <summary>
/// A single chunk in the SSE stream from the Customer Experience Copilot.
/// </summary>
public class CustomerExperienceStreamChunk
{
    /// <summary>Chunk type: "content" for text tokens, "metadata" for final summary, "error" for failures, "done" for stream end.</summary>
    public string Type { get; set; } = "content";

    /// <summary>The text content of this chunk. For "content" type, this is a token or partial response.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Final metadata, populated only on the last "metadata" chunk with tone, escalation info, and timing.</summary>
    public CustomerExperienceResponse? Metadata { get; set; }
}
