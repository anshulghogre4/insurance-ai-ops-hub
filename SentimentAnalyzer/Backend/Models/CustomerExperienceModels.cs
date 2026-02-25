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
