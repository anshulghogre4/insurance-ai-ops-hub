using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SentimentAnalyzer.Agents.Orchestration;
using SentimentAnalyzer.API.Data;
using SentimentAnalyzer.API.Data.Entities;
using SentimentAnalyzer.API.Models;
using SentimentAnalyzer.API.Services.Multimodal;

namespace SentimentAnalyzer.API.Services.CustomerExperience;

/// <summary>
/// AI-powered customer experience copilot for insurance interactions.
/// Provides empathetic, context-aware responses using direct kernel access
/// for single-turn chat and streaming chat completion for SSE.
/// PII is redacted before any external AI provider call.
/// </summary>
public class CustomerExperienceService : ICustomerExperienceService
{
    private readonly IResilientKernelProvider _kernelProvider;
    private readonly IPIIRedactor _piiRedactor;
    private readonly ICxInteractionRepository _auditRepo;
    private readonly ICxConversationRepository? _conversationRepo;
    private readonly ILogger<CustomerExperienceService> _logger;
    private readonly IContentSafetyService? _contentSafety;

    /// <summary>
    /// Maximum number of messages retained in the sliding conversation window.
    /// </summary>
    public const int MaxConversationTurns = 10;

    /// <summary>
    /// Standard regulatory disclaimer appended to all CX Copilot responses.
    /// Required for insurance domain compliance — AI-generated responses must not be
    /// mistaken for professional insurance advice or binding coverage decisions.
    /// </summary>
    public const string RegulatoryDisclaimer =
        "This is AI-generated guidance and does not constitute professional insurance advice. " +
        "Please consult your licensed insurance agent for binding decisions.";

    /// <summary>
    /// Insurance CX specialist system prompt. Defines behavior boundaries:
    /// empathetic tone, plain-language explanations, escalation detection,
    /// no coverage promises, always recommend contacting agent for binding decisions.
    /// Includes mandatory disclaimer instruction for regulatory compliance.
    /// </summary>
    private const string SystemPrompt = """
        You are an expert insurance customer experience specialist. Your role is to help
        policyholders understand their coverage, claims process, and policy terms in plain,
        empathetic language.

        GUIDELINES:
        1. Be empathetic and acknowledge the customer's situation before providing information.
        2. Explain insurance terms in plain language — avoid jargon unless the customer uses it first.
        3. If the customer seems frustrated, angry, or mentions legal action, recommend they speak
           with a licensed agent or supervisor. Flag this as an escalation.
        4. NEVER make coverage promises or confirm/deny specific claim outcomes.
        5. NEVER provide legal advice. Always recommend consulting a licensed professional.
        6. For binding decisions (policy changes, claim settlements), always recommend contacting
           their assigned agent or calling the customer service line.
        7. If claim context is provided, reference it naturally in your response.
        8. Keep responses concise but thorough — aim for 2-4 paragraphs maximum.
        9. End with a clear next step or offer of further assistance.
        10. Always end your response with the following disclaimer on a new line:
            'This is AI-generated guidance and does not constitute professional insurance advice. Please consult your licensed insurance agent for binding decisions.'

        ESCALATION TRIGGERS (recommend human agent):
        - Customer mentions attorney, lawyer, or legal action
        - Customer mentions filing a complaint with the department of insurance
        - Customer expresses extreme frustration or anger (profanity, all caps, threats)
        - Customer asks about specific settlement amounts or coverage determinations
        - Customer reports bodily injury or emergency situations
        - Customer mentions switching insurers or cancellation

        TONE CLASSIFICATION:
        At the end of your response, on a new line, add exactly one of these tags:
        [TONE:Professional] — standard informational response
        [TONE:Empathetic] — customer appears distressed, frustrated, or worried
        [TONE:Urgent] — emergency, legal threat, or time-sensitive matter
        [TONE:Informational] — purely factual/educational question

        If escalation is recommended, add on the next line:
        [ESCALATE:reason for escalation]
        """;

    /// <summary>
    /// Initializes the Customer Experience Copilot service.
    /// </summary>
    /// <param name="kernelProvider">Resilient kernel provider for LLM access with automatic fallback.</param>
    /// <param name="piiRedactor">PII redaction service — mandatory before external AI calls.</param>
    /// <param name="auditRepo">Repository for CX interaction audit trail (regulatory compliance).</param>
    /// <param name="logger">Structured logger for this service.</param>
    /// <param name="conversationRepo">Optional repository for conversation session persistence. Null disables conversation memory.</param>
    /// <param name="contentSafety">Optional content safety screening service for policyholder protection.</param>
    public CustomerExperienceService(
        IResilientKernelProvider kernelProvider,
        IPIIRedactor piiRedactor,
        ICxInteractionRepository auditRepo,
        ILogger<CustomerExperienceService> logger,
        ICxConversationRepository? conversationRepo = null,
        IContentSafetyService? contentSafety = null)
    {
        _kernelProvider = kernelProvider ?? throw new ArgumentNullException(nameof(kernelProvider));
        _piiRedactor = piiRedactor ?? throw new ArgumentNullException(nameof(piiRedactor));
        _auditRepo = auditRepo ?? throw new ArgumentNullException(nameof(auditRepo));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _conversationRepo = conversationRepo;
        _contentSafety = contentSafety;
    }

    /// <inheritdoc />
    public async Task<CustomerExperienceResponse> ChatAsync(
        string message, string? claimContext = null, string? sessionId = null, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("CX Copilot chat: {MessageLength} chars, hasClaimContext={HasContext}, hasSession={HasSession}",
            message.Length, claimContext != null, sessionId != null);

        try
        {
            // PII redaction before external AI call (mandatory per CLAUDE.md)
            var redactedMessage = _piiRedactor.Redact(message);
            var redactedContext = claimContext != null ? _piiRedactor.Redact(claimContext) : null;

            var userPrompt = BuildUserPrompt(redactedMessage, redactedContext);

            var kernel = _kernelProvider.GetKernel();
            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(SystemPrompt);

            // Load conversation history if session is provided
            if (!string.IsNullOrWhiteSpace(sessionId) && _conversationRepo != null)
            {
                await LoadConversationHistoryAsync(chatHistory, sessionId);
            }

            chatHistory.AddUserMessage(userPrompt);

            var response = await chatService.GetChatMessageContentAsync(chatHistory, cancellationToken: ct);
            var rawContent = response.Content ?? "I apologize, but I was unable to generate a response. Please try again or contact our customer service line.";

            // Content safety screening — protect policyholders from harmful AI responses
            if (_contentSafety != null)
            {
                var safetyResult = await _contentSafety.AnalyzeTextAsync(rawContent, ct);
                if (safetyResult.IsSuccess && !safetyResult.IsSafe)
                {
                    _logger.LogWarning("CX Copilot response flagged by Content Safety: {Categories}",
                        string.Join(", ", safetyResult.FlaggedCategories));
                    rawContent = "I apologize, but I was unable to generate an appropriate response. " +
                        "Please contact our customer service line for assistance.";
                }
            }

            var (cleanedResponse, tone, escalationRecommended, escalationReason) = ParseResponseMetadata(rawContent);

            // Output-side PII redaction: LLM may echo back PII even though input was redacted
            var redactedResponse = _piiRedactor.Redact(cleanedResponse);

            // Ensure mandatory regulatory disclaimer is present
            if (!redactedResponse.Contains(RegulatoryDisclaimer, StringComparison.OrdinalIgnoreCase))
            {
                redactedResponse = $"{redactedResponse}\n\n{RegulatoryDisclaimer}";
            }

            sw.Stop();
            var providerName = _kernelProvider.ActiveProviderName;

            _logger.LogInformation("CX Copilot chat completed: provider={Provider}, tone={Tone}, escalation={Escalation}, elapsed={Elapsed}ms",
                providerName, tone, escalationRecommended, sw.ElapsedMilliseconds);

            // BA-C1: Audit trail for regulatory compliance (fire-and-forget, never block response)
            await SaveAuditRecordAsync(message, redactedResponse, tone, escalationRecommended, escalationReason,
                providerName, sw.ElapsedMilliseconds, claimContext != null, wasStreamed: false);

            // Persist conversation turns if session is active
            if (!string.IsNullOrWhiteSpace(sessionId) && _conversationRepo != null)
            {
                await SaveConversationTurnsAsync(sessionId, redactedMessage, redactedResponse);
            }

            return new CustomerExperienceResponse
            {
                Response = redactedResponse,
                Tone = tone,
                EscalationRecommended = escalationRecommended,
                EscalationReason = escalationReason,
                LlmProvider = providerName,
                ElapsedMilliseconds = sw.ElapsedMilliseconds,
                Disclaimer = RegulatoryDisclaimer,
                ContentSafetyScreened = _contentSafety != null
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "CX Copilot chat failed after {Elapsed}ms", sw.ElapsedMilliseconds);

            // Report failure to trigger provider cooldown and fallback
            _kernelProvider.ReportFailure(_kernelProvider.ActiveProviderName, ex);

            return new CustomerExperienceResponse
            {
                Response = "I apologize for the inconvenience. Our AI assistant is temporarily unavailable. " +
                           "Please try again in a moment, or contact our customer service line for immediate assistance.",
                Tone = "Professional",
                EscalationRecommended = false,
                LlmProvider = "Error",
                ElapsedMilliseconds = sw.ElapsedMilliseconds,
                ContentSafetyScreened = false
            };
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<CustomerExperienceStreamChunk> StreamChatAsync(
        string message, string? claimContext = null, string? sessionId = null, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("CX Copilot stream: {MessageLength} chars, hasClaimContext={HasContext}, hasSession={HasSession}",
            message.Length, claimContext != null, sessionId != null);

        // PII redaction before external AI call (mandatory per CLAUDE.md)
        var redactedMessage = _piiRedactor.Redact(message);
        var redactedContext = claimContext != null ? _piiRedactor.Redact(claimContext) : null;

        var userPrompt = BuildUserPrompt(redactedMessage, redactedContext);
        var fullResponse = new StringBuilder();
        var providerName = _kernelProvider.ActiveProviderName;

        // Initialize kernel and chat service outside try/catch to avoid yield-in-catch CS1631
        ChatHistory? chatHistory = null;
        IChatCompletionService? chatService = null;
        bool initFailed = false;

        try
        {
            var kernel = _kernelProvider.GetKernel();
            chatService = kernel.GetRequiredService<IChatCompletionService>();
            chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(SystemPrompt);

            // Load conversation history if session is provided
            if (!string.IsNullOrWhiteSpace(sessionId) && _conversationRepo != null)
            {
                await LoadConversationHistoryAsync(chatHistory, sessionId);
            }

            chatHistory.AddUserMessage(userPrompt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CX Copilot stream failed to initialize kernel");
            _kernelProvider.ReportFailure(providerName, ex);
            initFailed = true;
        }

        // Yield error chunks outside catch block (C# disallows yield inside catch)
        if (initFailed)
        {
            yield return new CustomerExperienceStreamChunk
            {
                Type = "error",
                Content = "Our AI assistant is temporarily unavailable. Please try again in a moment."
            };

            yield return new CustomerExperienceStreamChunk
            {
                Type = "done",
                Content = string.Empty
            };

            yield break;
        }

        // Stream content chunks from the LLM using manual enumerator pattern.
        // C# disallows yield inside try-catch, so we use try-catch around MoveNextAsync
        // and yield outside the try block. This preserves true streaming latency.
        bool streamErrored = false;
        bool inMetadataSection = false;
        var enumerator = chatService!.GetStreamingChatMessageContentsAsync(chatHistory!, cancellationToken: ct).GetAsyncEnumerator(ct);

        try
        {
            while (true)
            {
                // Wrap only the MoveNextAsync in try-catch to handle mid-stream failures (QA-C1)
                bool hasNext;
                try
                {
                    hasNext = await enumerator.MoveNextAsync();
                }
                catch (Exception ex)
                {
                    streamErrored = true;
                    sw.Stop();
                    _logger.LogError(ex, "CX Copilot stream failed mid-stream after {Elapsed}ms", sw.ElapsedMilliseconds);
                    _kernelProvider.ReportFailure(providerName, ex);
                    break;
                }

                if (!hasNext || ct.IsCancellationRequested)
                    break;

                var content = enumerator.Current.Content;
                if (string.IsNullOrEmpty(content))
                    continue;

                fullResponse.Append(content);

                // UX-H2: Suppress chunks containing [TONE: or [ESCALATE: metadata tags.
                // Once we see a tag prefix, all subsequent chunks are metadata — do not stream to client.
                if (inMetadataSection)
                    continue;

                if (content.Contains("[TONE:", StringComparison.OrdinalIgnoreCase) ||
                    content.Contains("[ESCALATE:", StringComparison.OrdinalIgnoreCase))
                {
                    inMetadataSection = true;
                    // Strip any leading text before the tag in this chunk and yield it if non-empty
                    var tagIndex = content.IndexOf("[TONE:", StringComparison.OrdinalIgnoreCase);
                    if (tagIndex < 0)
                        tagIndex = content.IndexOf("[ESCALATE:", StringComparison.OrdinalIgnoreCase);

                    if (tagIndex > 0)
                    {
                        var beforeTag = content[..tagIndex].TrimEnd();
                        if (!string.IsNullOrEmpty(beforeTag))
                        {
                            yield return new CustomerExperienceStreamChunk
                            {
                                Type = "content",
                                Content = beforeTag
                            };
                        }
                    }
                    continue;
                }

                yield return new CustomerExperienceStreamChunk
                {
                    Type = "content",
                    Content = content
                };
            }
        }
        finally
        {
            await enumerator.DisposeAsync();
        }

        // Yield error/done chunks outside try block
        if (streamErrored)
        {
            yield return new CustomerExperienceStreamChunk
            {
                Type = "error",
                Content = "Our AI assistant encountered an issue during streaming. Please try again in a moment."
            };

            yield return new CustomerExperienceStreamChunk
            {
                Type = "done",
                Content = string.Empty
            };

            yield break;
        }

        sw.Stop();
        providerName = _kernelProvider.ActiveProviderName;

        var rawContent = fullResponse.ToString();

        // Content safety screening — protect policyholders from harmful AI responses
        if (_contentSafety != null)
        {
            var safetyResult = await _contentSafety.AnalyzeTextAsync(rawContent, ct);
            if (safetyResult.IsSuccess && !safetyResult.IsSafe)
            {
                _logger.LogWarning("CX Copilot streamed response flagged by Content Safety: {Categories}",
                    string.Join(", ", safetyResult.FlaggedCategories));
                rawContent = "I apologize, but I was unable to generate an appropriate response. " +
                    "Please contact our customer service line for assistance.";
            }
        }

        var (cleanedResponse, tone, escalationRecommended, escalationReason) = ParseResponseMetadata(rawContent);

        // BA-H3: Output-side PII redaction — LLM may echo back PII even though input was redacted
        var redactedResponse = _piiRedactor.Redact(cleanedResponse);

        // BA-C2: Ensure mandatory regulatory disclaimer is present
        if (!redactedResponse.Contains(RegulatoryDisclaimer, StringComparison.OrdinalIgnoreCase))
        {
            redactedResponse = $"{redactedResponse}\n\n{RegulatoryDisclaimer}";
        }

        _logger.LogInformation("CX Copilot stream completed: provider={Provider}, tone={Tone}, escalation={Escalation}, elapsed={Elapsed}ms",
            providerName, tone, escalationRecommended, sw.ElapsedMilliseconds);

        // BA-C1: Audit trail for regulatory compliance
        await SaveAuditRecordAsync(message, redactedResponse, tone, escalationRecommended, escalationReason,
            providerName, sw.ElapsedMilliseconds, claimContext != null, wasStreamed: true);

        // Persist conversation turns if session is active
        if (!string.IsNullOrWhiteSpace(sessionId) && _conversationRepo != null)
        {
            await SaveConversationTurnsAsync(sessionId, redactedMessage, redactedResponse);
        }

        // Send metadata chunk with tone analysis and timing
        yield return new CustomerExperienceStreamChunk
        {
            Type = "metadata",
            Content = string.Empty,
            Metadata = new CustomerExperienceResponse
            {
                Response = redactedResponse,
                Tone = tone,
                EscalationRecommended = escalationRecommended,
                EscalationReason = escalationReason,
                LlmProvider = providerName,
                ElapsedMilliseconds = sw.ElapsedMilliseconds,
                Disclaimer = RegulatoryDisclaimer,
                ContentSafetyScreened = _contentSafety != null
            }
        };

        // Send done signal
        yield return new CustomerExperienceStreamChunk
        {
            Type = "done",
            Content = string.Empty
        };
    }

    /// <summary>
    /// Loads conversation history from the session repository into the ChatHistory.
    /// Prior turns are added as alternating user/assistant messages to provide context.
    /// </summary>
    /// <param name="chatHistory">The ChatHistory to populate with prior turns.</param>
    /// <param name="sessionId">The session identifier to load history for.</param>
    private async Task LoadConversationHistoryAsync(ChatHistory chatHistory, string sessionId)
    {
        try
        {
            var priorTurns = await _conversationRepo!.GetRecentTurnsAsync(sessionId, MaxConversationTurns);

            foreach (var turn in priorTurns)
            {
                if (string.Equals(turn.Role, "user", StringComparison.OrdinalIgnoreCase))
                {
                    chatHistory.AddUserMessage(turn.Content);
                }
                else if (string.Equals(turn.Role, "assistant", StringComparison.OrdinalIgnoreCase))
                {
                    chatHistory.AddAssistantMessage(turn.Content);
                }
            }

            _logger.LogInformation("Loaded {TurnCount} prior turns for CX session {SessionId}",
                priorTurns.Count, sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load conversation history for session {SessionId} — proceeding without context", sessionId);
        }
    }

    /// <summary>
    /// Saves both user and assistant messages to the conversation session.
    /// PII must already be redacted before calling this method.
    /// Failures are logged but never propagated — persistence must not block the response.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="redactedUserMessage">The PII-redacted user message.</param>
    /// <param name="redactedAssistantResponse">The PII-redacted assistant response.</param>
    private async Task SaveConversationTurnsAsync(string sessionId, string redactedUserMessage, string redactedAssistantResponse)
    {
        try
        {
            await _conversationRepo!.AppendTurnAsync(sessionId, "user", redactedUserMessage, MaxConversationTurns);
            await _conversationRepo.AppendTurnAsync(sessionId, "assistant", redactedAssistantResponse, MaxConversationTurns);

            _logger.LogInformation("Saved conversation turns for CX session {SessionId}", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save conversation turns for session {SessionId} — response already sent to user", sessionId);
        }
    }

    /// <summary>
    /// Builds the user prompt, optionally including claim context for grounded responses.
    /// </summary>
    /// <param name="redactedMessage">The PII-redacted customer message.</param>
    /// <param name="redactedContext">Optional PII-redacted claim/policy context.</param>
    /// <returns>The assembled user prompt string.</returns>
    private static string BuildUserPrompt(string redactedMessage, string? redactedContext)
    {
        if (string.IsNullOrWhiteSpace(redactedContext))
        {
            return redactedMessage;
        }

        return $"""
            CLAIM/POLICY CONTEXT:
            {redactedContext}

            CUSTOMER MESSAGE:
            {redactedMessage}
            """;
    }

    /// <summary>
    /// Parses the LLM response to extract tone classification and escalation metadata.
    /// Removes the metadata tags from the response text for clean display.
    /// </summary>
    /// <param name="rawContent">The raw LLM response including metadata tags.</param>
    /// <returns>Tuple of (cleaned response, tone, escalation flag, escalation reason).</returns>
    private static (string CleanedResponse, string Tone, bool EscalationRecommended, string? EscalationReason) ParseResponseMetadata(string rawContent)
    {
        var tone = "Professional";
        var escalationRecommended = false;
        string? escalationReason = null;
        var cleanedResponse = rawContent;

        // Extract tone tag: [TONE:Professional], [TONE:Empathetic], [TONE:Urgent], [TONE:Informational]
        var toneStartIndex = rawContent.IndexOf("[TONE:", StringComparison.OrdinalIgnoreCase);
        if (toneStartIndex >= 0)
        {
            var toneEndIndex = rawContent.IndexOf(']', toneStartIndex);
            if (toneEndIndex > toneStartIndex)
            {
                var toneValue = rawContent.Substring(toneStartIndex + 6, toneEndIndex - toneStartIndex - 6).Trim();
                if (IsValidTone(toneValue))
                {
                    tone = toneValue;
                }
                cleanedResponse = cleanedResponse.Remove(toneStartIndex, toneEndIndex - toneStartIndex + 1).Trim();
            }
        }

        // Extract escalation tag: [ESCALATE:reason]
        var escalateStartIndex = cleanedResponse.IndexOf("[ESCALATE:", StringComparison.OrdinalIgnoreCase);
        if (escalateStartIndex >= 0)
        {
            var escalateEndIndex = cleanedResponse.IndexOf(']', escalateStartIndex);
            if (escalateEndIndex > escalateStartIndex)
            {
                escalationRecommended = true;
                escalationReason = cleanedResponse.Substring(escalateStartIndex + 10, escalateEndIndex - escalateStartIndex - 10).Trim();
                cleanedResponse = cleanedResponse.Remove(escalateStartIndex, escalateEndIndex - escalateStartIndex + 1).Trim();
            }
        }

        // Also detect escalation keywords in the response even if the LLM forgot the tag
        if (!escalationRecommended)
        {
            escalationRecommended = DetectEscalationKeywords(rawContent);
            if (escalationRecommended)
            {
                escalationReason = "Customer interaction contains escalation indicators detected by keyword analysis.";
            }
        }

        return (cleanedResponse, tone, escalationRecommended, escalationReason);
    }

    /// <summary>
    /// Validates the tone value against the allowed set.
    /// </summary>
    private static bool IsValidTone(string tone)
    {
        return tone is "Professional" or "Empathetic" or "Urgent" or "Informational";
    }

    /// <summary>
    /// Persists a CX interaction audit record for regulatory compliance.
    /// Failures are logged but never propagated — audit must not block the user's response.
    /// </summary>
    private async Task SaveAuditRecordAsync(
        string originalMessage, string redactedResponse, string tone,
        bool escalationRecommended, string? escalationReason,
        string llmProvider, long elapsedMs, bool hasClaimContext, bool wasStreamed)
    {
        try
        {
            var record = new CxInteractionRecord
            {
                MessageHash = ComputeSha256(originalMessage),
                MessageLength = originalMessage.Length,
                ResponseText = redactedResponse.Length > 5000 ? redactedResponse[..4997] + "..." : redactedResponse,
                Tone = tone,
                EscalationRecommended = escalationRecommended,
                EscalationReason = escalationReason,
                LlmProvider = llmProvider,
                ElapsedMilliseconds = elapsedMs,
                HasClaimContext = hasClaimContext,
                WasStreamed = wasStreamed
            };

            await _auditRepo.SaveInteractionAsync(record);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save CX interaction audit record — response already sent to user");
        }
    }

    /// <summary>
    /// Computes a truncated SHA-256 hash for diagnostic/audit logging. Never stores raw PII.
    /// </summary>
    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }

    /// <summary>
    /// Detects escalation keywords in the response text as a fallback
    /// when the LLM does not include the [ESCALATE:] tag.
    /// </summary>
    /// <param name="text">The response text to scan.</param>
    /// <returns>True if escalation indicators are detected.</returns>
    private static bool DetectEscalationKeywords(string text)
    {
        var lowerText = text.ToLowerInvariant();
        string[] escalationKeywords =
        [
            "speak with a supervisor",
            "speak with a manager",
            "speak to someone in charge",
            "contact a licensed agent",
            "recommend speaking with a supervisor",
            "recommend speaking with a manager",
            "recommend contacting a supervisor",
            "recommend contacting a manager",
            "escalate this matter",
            "department of insurance",
            "legal counsel",
            "file a formal complaint",
            "filing a complaint",
            "report to the state",
            "attorney general",
            "bad faith",
            "breach of contract",
            "unfair claims practices"
        ];

        foreach (var keyword in escalationKeywords)
        {
            if (lowerText.Contains(keyword, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
