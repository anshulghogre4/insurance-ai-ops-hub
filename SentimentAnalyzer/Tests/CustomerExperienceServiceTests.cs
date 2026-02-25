using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;
using SentimentAnalyzer.Agents.Orchestration;
using SentimentAnalyzer.API.Data;
using SentimentAnalyzer.API.Models;
using SentimentAnalyzer.API.Services.CustomerExperience;

namespace SentimentAnalyzer.Tests;

/// <summary>
/// Unit tests for <see cref="CustomerExperienceService"/> — the AI-powered CX Copilot
/// that provides empathetic, context-aware insurance Q&amp;A with PII redaction,
/// escalation detection, and tone classification. Tests cover both single-turn
/// <see cref="ICustomerExperienceService.ChatAsync"/> and streaming
/// <see cref="ICustomerExperienceService.StreamChatAsync"/> paths.
/// </summary>
public class CustomerExperienceServiceTests
{
    private readonly Mock<IResilientKernelProvider> _mockKernelProvider;
    private readonly Mock<IChatCompletionService> _mockChatCompletion;
    private readonly Mock<IPIIRedactor> _mockPiiRedactor;
    private readonly Mock<ICxInteractionRepository> _mockAuditRepo;
    private readonly Mock<ILogger<CustomerExperienceService>> _mockLogger;
    private readonly CustomerExperienceService _sut;

    /// <summary>
    /// Realistic insurance customer message used across tests.
    /// Per project guidelines: never use "test", "foo", "bar".
    /// </summary>
    private const string SampleCustomerMessage =
        "I reported water damage to my basement on January 15th. It's been 3 weeks and I haven't heard anything from my adjuster. " +
        "My policy number is HO-2024-789456. Can you tell me what's happening with my claim?";

    /// <summary>
    /// Typical LLM response with tone and no escalation.
    /// </summary>
    private const string SampleLlmResponse =
        "I completely understand your frustration, and I'm sorry you've been waiting so long for an update on your water damage claim. " +
        "Three weeks without hearing from your adjuster is certainly longer than expected.\n\n" +
        "Typically, after a water damage claim is filed, an adjuster should make initial contact within 3-5 business days. " +
        "I'd recommend calling your claims department directly and referencing your claim number to request a status update.\n\n" +
        "In the meantime, please make sure to document any additional damage or mitigation steps you've taken, as this can help " +
        "expedite the process.\n\n[TONE:Empathetic]";

    /// <summary>
    /// LLM response that includes an escalation tag.
    /// </summary>
    private const string EscalationLlmResponse =
        "I understand this has been an extremely frustrating experience, and I'm sorry for the delays. " +
        "Given the severity of your situation and the extended timeline, I would strongly recommend speaking " +
        "with a supervisor or your assigned agent directly to expedite your claim.\n\n" +
        "[TONE:Urgent]\n[ESCALATE:Customer has waited 3 weeks with no adjuster contact, mentions attorney involvement]";

    public CustomerExperienceServiceTests()
    {
        _mockKernelProvider = new Mock<IResilientKernelProvider>();
        _mockChatCompletion = new Mock<IChatCompletionService>();
        _mockPiiRedactor = new Mock<IPIIRedactor>();
        _mockAuditRepo = new Mock<ICxInteractionRepository>();
        _mockLogger = new Mock<ILogger<CustomerExperienceService>>();

        // Default PII redaction: pass-through (no PII detected in test data)
        _mockPiiRedactor.Setup(p => p.Redact(It.IsAny<string>())).Returns<string>(s => s);

        // Build a test Kernel with the mocked IChatCompletionService
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(_mockChatCompletion.Object);
        var kernel = builder.Build();

        _mockKernelProvider.Setup(p => p.GetKernel()).Returns(kernel);
        _mockKernelProvider.Setup(p => p.ActiveProviderName).Returns("Groq");

        _sut = new CustomerExperienceService(
            _mockKernelProvider.Object,
            _mockPiiRedactor.Object,
            _mockAuditRepo.Object,
            _mockLogger.Object);
    }

    // ────────────────────────────────────────────────────────────
    // ChatAsync Tests
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ChatAsync_WithValidMessage_ReturnsResponse()
    {
        // Arrange
        _mockChatCompletion
            .Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatMessageContent>
            {
                new(AuthorRole.Assistant, SampleLlmResponse)
            });

        // Act
        var result = await _sut.ChatAsync(SampleCustomerMessage);

        // Assert
        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.Response));
        Assert.Contains("frustration", result.Response);
        Assert.Equal("Empathetic", result.Tone);
        Assert.False(result.EscalationRecommended);
        Assert.Null(result.EscalationReason);
        Assert.Equal("Groq", result.LlmProvider);
        Assert.True(result.ElapsedMilliseconds >= 0);

        // The tone tag should be stripped from the cleaned response
        Assert.DoesNotContain("[TONE:", result.Response);

        // BA-C2: Regulatory disclaimer must be present
        Assert.Contains(CustomerExperienceService.RegulatoryDisclaimer, result.Response);
        Assert.Equal(CustomerExperienceService.RegulatoryDisclaimer, result.Disclaimer);
    }

    [Fact]
    public async Task ChatAsync_WithClaimContext_IncludesContextInPrompt()
    {
        // Arrange
        var claimContext = "Claim CLM-2024-78901, water damage, filed 2024-01-15, status: pending adjuster assignment";
        string? capturedPrompt = null;

        _mockChatCompletion
            .Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(),
                It.IsAny<CancellationToken>()))
            .Callback<ChatHistory, PromptExecutionSettings?, Kernel?, CancellationToken>((history, _, _, _) =>
            {
                // Capture the user message sent to LLM to verify claim context was included
                capturedPrompt = history.LastOrDefault(m => m.Role == AuthorRole.User)?.Content;
            })
            .ReturnsAsync(new List<ChatMessageContent>
            {
                new(AuthorRole.Assistant, SampleLlmResponse)
            });

        // Act
        var result = await _sut.ChatAsync(SampleCustomerMessage, claimContext);

        // Assert — prompt includes both claim context and customer message
        Assert.NotNull(capturedPrompt);
        Assert.Contains("CLAIM/POLICY CONTEXT", capturedPrompt);
        Assert.Contains("water damage", capturedPrompt);
        Assert.Contains("pending adjuster assignment", capturedPrompt);
        Assert.Contains("CUSTOMER MESSAGE", capturedPrompt);
        Assert.Contains(SampleCustomerMessage, capturedPrompt);
    }

    [Fact]
    public async Task ChatAsync_PiiRedactsMessageBeforeLlm()
    {
        // Arrange — PII redactor transforms SSN and policy number
        var rawMessage = "My SSN is 123-45-6789 and my policy is HO-2024-789456. Why was my claim denied?";
        var redactedMessage = "My SSN is [SSN-REDACTED] and my policy is [POLICY-REDACTED]. Why was my claim denied?";

        _mockPiiRedactor
            .Setup(p => p.Redact(rawMessage))
            .Returns(redactedMessage);

        string? capturedPrompt = null;
        _mockChatCompletion
            .Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(),
                It.IsAny<CancellationToken>()))
            .Callback<ChatHistory, PromptExecutionSettings?, Kernel?, CancellationToken>((history, _, _, _) =>
            {
                capturedPrompt = history.LastOrDefault(m => m.Role == AuthorRole.User)?.Content;
            })
            .ReturnsAsync(new List<ChatMessageContent>
            {
                new(AuthorRole.Assistant,
                    "I understand your concern about the claim denial. [TONE:Empathetic]")
            });

        // Act
        var result = await _sut.ChatAsync(rawMessage);

        // Assert — PII redactor was called with the raw message
        _mockPiiRedactor.Verify(p => p.Redact(rawMessage), Times.Once);

        // Assert — LLM received the redacted text, NOT raw PII
        Assert.NotNull(capturedPrompt);
        Assert.Contains("[SSN-REDACTED]", capturedPrompt);
        Assert.Contains("[POLICY-REDACTED]", capturedPrompt);
        Assert.DoesNotContain("123-45-6789", capturedPrompt);
        Assert.DoesNotContain("HO-2024-789456", capturedPrompt);
    }

    [Fact]
    public async Task ChatAsync_WithEscalationTag_DetectsEscalation()
    {
        // Arrange — LLM response includes [ESCALATE:] tag
        _mockChatCompletion
            .Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatMessageContent>
            {
                new(AuthorRole.Assistant, EscalationLlmResponse)
            });

        // Act
        var result = await _sut.ChatAsync(
            "I've been waiting 3 weeks and my attorney says I should file a complaint with the department of insurance.");

        // Assert
        Assert.True(result.EscalationRecommended);
        Assert.NotNull(result.EscalationReason);
        Assert.Contains("attorney", result.EscalationReason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Urgent", result.Tone);

        // Tags should be stripped from the cleaned response
        Assert.DoesNotContain("[ESCALATE:", result.Response);
        Assert.DoesNotContain("[TONE:", result.Response);
    }

    [Fact]
    public async Task ChatAsync_WithEscalationKeyword_DetectsEscalationWithoutTag()
    {
        // Arrange — LLM forgets the [ESCALATE:] tag but response contains escalation keywords
        var responseWithKeyword =
            "I understand your frustration. Given the complexity of your situation, I recommend speaking with " +
            "a supervisor who can review your claim status and expedite the process.\n\n[TONE:Empathetic]";

        _mockChatCompletion
            .Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatMessageContent>
            {
                new(AuthorRole.Assistant, responseWithKeyword)
            });

        // Act
        var result = await _sut.ChatAsync(
            "Nobody is returning my calls. I want to speak with someone who can actually help me.");

        // Assert — escalation detected via keyword fallback ("speak with a supervisor")
        Assert.True(result.EscalationRecommended);
        Assert.NotNull(result.EscalationReason);
        Assert.Contains("escalation indicators", result.EscalationReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ChatAsync_LlmThrows_ReturnsErrorResponse()
    {
        // Arrange — LLM throws (simulating provider failure after all fallbacks exhausted)
        _mockChatCompletion
            .Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Groq API returned 503 Service Unavailable"));

        // Act
        var result = await _sut.ChatAsync(SampleCustomerMessage);

        // Assert — graceful degradation with user-friendly message
        Assert.NotNull(result);
        Assert.Contains("temporarily unavailable", result.Response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("customer service line", result.Response, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Professional", result.Tone);
        Assert.False(result.EscalationRecommended);
        Assert.Equal("Error", result.LlmProvider);
        Assert.True(result.ElapsedMilliseconds >= 0);

        // Verify failure was reported to trigger provider cooldown
        _mockKernelProvider.Verify(
            p => p.ReportFailure(It.IsAny<string>(), It.IsAny<Exception>()),
            Times.Once);
    }

    [Fact]
    public async Task ChatAsync_EmptyResponse_ReturnsDefaultMessage()
    {
        // Arrange — LLM returns null content (simulating an empty/malformed response)
        _mockChatCompletion
            .Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatMessageContent>
            {
                new(AuthorRole.Assistant, content: null)
            });

        // Act
        var result = await _sut.ChatAsync("What is the status of my water damage claim CLM-2024-55001?");

        // Assert — fallback message is used when LLM returns null/empty
        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.Response));
        Assert.Contains("unable to generate a response", result.Response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("customer service", result.Response, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Professional", result.Tone);
        Assert.Equal("Groq", result.LlmProvider);
    }

    [Fact]
    public async Task ChatAsync_WithNoClaimContext_SendsMessageDirectlyAsPrompt()
    {
        // Arrange — no claim context provided, message should go directly as user prompt
        var simpleMessage = "What does 'deductible' mean on my homeowners policy?";
        string? capturedPrompt = null;

        _mockChatCompletion
            .Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(),
                It.IsAny<CancellationToken>()))
            .Callback<ChatHistory, PromptExecutionSettings?, Kernel?, CancellationToken>((history, _, _, _) =>
            {
                capturedPrompt = history.LastOrDefault(m => m.Role == AuthorRole.User)?.Content;
            })
            .ReturnsAsync(new List<ChatMessageContent>
            {
                new(AuthorRole.Assistant,
                    "A deductible is the amount you pay out of pocket before your insurance coverage kicks in. " +
                    "For homeowners policies, this is typically a fixed dollar amount.\n\n[TONE:Informational]")
            });

        // Act
        var result = await _sut.ChatAsync(simpleMessage, claimContext: null);

        // Assert — message sent directly without "CLAIM/POLICY CONTEXT" wrapper
        Assert.NotNull(capturedPrompt);
        Assert.DoesNotContain("CLAIM/POLICY CONTEXT", capturedPrompt);
        Assert.Equal(simpleMessage, capturedPrompt);
        Assert.Equal("Informational", result.Tone);
    }

    [Fact]
    public async Task ChatAsync_PiiRedactsClaimContextToo()
    {
        // Arrange — both message and claim context contain PII
        var rawMessage = "Why was my claim denied?";
        var rawContext = "Policyholder: Jane Smith, SSN 987-65-4321, Claim CLM-2024-99001";
        var redactedContext = "Policyholder: [NAME-REDACTED], SSN [SSN-REDACTED], Claim [CLAIM-REDACTED]";

        _mockPiiRedactor
            .Setup(p => p.Redact(rawMessage))
            .Returns(rawMessage); // No PII in the message itself
        _mockPiiRedactor
            .Setup(p => p.Redact(rawContext))
            .Returns(redactedContext);

        string? capturedPrompt = null;
        _mockChatCompletion
            .Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(),
                It.IsAny<CancellationToken>()))
            .Callback<ChatHistory, PromptExecutionSettings?, Kernel?, CancellationToken>((history, _, _, _) =>
            {
                capturedPrompt = history.LastOrDefault(m => m.Role == AuthorRole.User)?.Content;
            })
            .ReturnsAsync(new List<ChatMessageContent>
            {
                new(AuthorRole.Assistant, "I understand your concern about the denial. [TONE:Empathetic]")
            });

        // Act
        var result = await _sut.ChatAsync(rawMessage, rawContext);

        // Assert — PII redactor was called for both message and context
        _mockPiiRedactor.Verify(p => p.Redact(rawMessage), Times.Once);
        _mockPiiRedactor.Verify(p => p.Redact(rawContext), Times.Once);

        // Assert — LLM received redacted context, NOT raw PII
        Assert.NotNull(capturedPrompt);
        Assert.Contains("[NAME-REDACTED]", capturedPrompt);
        Assert.Contains("[SSN-REDACTED]", capturedPrompt);
        Assert.Contains("[CLAIM-REDACTED]", capturedPrompt);
        Assert.DoesNotContain("Jane Smith", capturedPrompt);
        Assert.DoesNotContain("987-65-4321", capturedPrompt);
        Assert.DoesNotContain("CLM-2024-99001", capturedPrompt);
    }

    // ────────────────────────────────────────────────────────────
    // StreamChatAsync Tests
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task StreamChatAsync_YieldsContentChunks()
    {
        // Arrange — streaming returns multiple content chunks followed by tone tag
        var streamingContents = new List<StreamingChatMessageContent>
        {
            new(AuthorRole.Assistant, "I understand "),
            new(AuthorRole.Assistant, "your frustration "),
            new(AuthorRole.Assistant, "with the delay. "),
            new(AuthorRole.Assistant, "Please contact your adjuster. "),
            new(AuthorRole.Assistant, "[TONE:Empathetic]")
        };

        _mockChatCompletion
            .Setup(c => c.GetStreamingChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(),
                It.IsAny<CancellationToken>()))
            .Returns(streamingContents.ToAsyncEnumerable());

        // Act
        var chunks = new List<CustomerExperienceStreamChunk>();
        await foreach (var chunk in _sut.StreamChatAsync(SampleCustomerMessage))
        {
            chunks.Add(chunk);
        }

        // Assert — should have content chunks + metadata + done
        var contentChunks = chunks.Where(c => c.Type == "content").ToList();
        var metadataChunks = chunks.Where(c => c.Type == "metadata").ToList();
        var doneChunks = chunks.Where(c => c.Type == "done").ToList();

        Assert.True(contentChunks.Count >= 1, "Should have at least one content chunk.");
        Assert.Single(metadataChunks);
        Assert.Single(doneChunks);

        // UX-H2: TONE tags must NOT appear in content chunks
        foreach (var contentChunk in contentChunks)
        {
            Assert.DoesNotContain("[TONE:", contentChunk.Content, StringComparison.OrdinalIgnoreCase);
        }

        // Metadata chunk should contain the final parsed response with tone
        var metadata = metadataChunks[0].Metadata;
        Assert.NotNull(metadata);
        Assert.Equal("Empathetic", metadata.Tone);
        Assert.False(string.IsNullOrWhiteSpace(metadata.Response));
        Assert.Equal("Groq", metadata.LlmProvider);
        Assert.True(metadata.ElapsedMilliseconds >= 0);

        // BA-C2: Regulatory disclaimer in stream metadata response
        Assert.Contains(CustomerExperienceService.RegulatoryDisclaimer, metadata.Response);
        Assert.Equal(CustomerExperienceService.RegulatoryDisclaimer, metadata.Disclaimer);
    }

    [Fact]
    public async Task StreamChatAsync_PiiRedactsBeforeStreaming()
    {
        // Arrange — verify PII redaction happens before the streaming LLM call
        var rawMessage = "My SSN is 111-22-3333 and I need help with policy HO-2025-112233.";
        var redactedMessage = "My SSN is [SSN-REDACTED] and I need help with policy [POLICY-REDACTED].";

        _mockPiiRedactor
            .Setup(p => p.Redact(rawMessage))
            .Returns(redactedMessage);

        string? capturedPrompt = null;
        _mockChatCompletion
            .Setup(c => c.GetStreamingChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(),
                It.IsAny<CancellationToken>()))
            .Callback<ChatHistory, PromptExecutionSettings?, Kernel?, CancellationToken>((history, _, _, _) =>
            {
                capturedPrompt = history.LastOrDefault(m => m.Role == AuthorRole.User)?.Content;
            })
            .Returns(new List<StreamingChatMessageContent>
            {
                new(AuthorRole.Assistant, "I can help you with your policy questions. [TONE:Professional]")
            }.ToAsyncEnumerable());

        // Act
        var chunks = new List<CustomerExperienceStreamChunk>();
        await foreach (var chunk in _sut.StreamChatAsync(rawMessage))
        {
            chunks.Add(chunk);
        }

        // Assert — PII redactor was called
        _mockPiiRedactor.Verify(p => p.Redact(rawMessage), Times.Once);

        // Assert — LLM received redacted text
        Assert.NotNull(capturedPrompt);
        Assert.Contains("[SSN-REDACTED]", capturedPrompt);
        Assert.Contains("[POLICY-REDACTED]", capturedPrompt);
        Assert.DoesNotContain("111-22-3333", capturedPrompt);
        Assert.DoesNotContain("HO-2025-112233", capturedPrompt);
    }

    [Fact]
    public async Task StreamChatAsync_KernelInitFails_YieldsErrorAndDoneChunks()
    {
        // Arrange — kernel provider throws (all providers exhausted)
        _mockKernelProvider
            .Setup(p => p.GetKernel())
            .Throws(new InvalidOperationException("All LLM providers are in cooldown."));

        // Act
        var chunks = new List<CustomerExperienceStreamChunk>();
        await foreach (var chunk in _sut.StreamChatAsync(SampleCustomerMessage))
        {
            chunks.Add(chunk);
        }

        // Assert — should yield an error chunk and a done chunk
        Assert.Equal(2, chunks.Count);
        Assert.Equal("error", chunks[0].Type);
        Assert.Contains("temporarily unavailable", chunks[0].Content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("done", chunks[1].Type);

        // Verify failure was reported for provider cooldown
        _mockKernelProvider.Verify(
            p => p.ReportFailure(It.IsAny<string>(), It.IsAny<Exception>()),
            Times.Once);
    }

    [Fact]
    public async Task ChatAsync_ToneClassification_Professional()
    {
        // Arrange — LLM returns a response with [TONE:Professional] tag
        var professionalResponse =
            "Your homeowners policy typically covers water damage from sudden and accidental events " +
            "such as burst pipes or appliance overflow. However, gradual water damage or flooding from " +
            "external sources may require separate flood insurance.\n\n[TONE:Professional]";

        _mockChatCompletion
            .Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatMessageContent>
            {
                new(AuthorRole.Assistant, professionalResponse)
            });

        // Act
        var result = await _sut.ChatAsync("Does my homeowners policy cover water damage from burst pipes?");

        // Assert
        Assert.Equal("Professional", result.Tone);
        Assert.False(result.EscalationRecommended);
        Assert.DoesNotContain("[TONE:", result.Response);
    }

    [Fact]
    public async Task ChatAsync_NoToneTag_DefaultsToProfessional()
    {
        // Arrange — LLM forgets to include a [TONE:] tag entirely
        var responseWithoutTone =
            "A deductible is the amount you must pay out-of-pocket before your insurance " +
            "coverage begins to pay. For example, if your deductible is $1,000 and you " +
            "have a covered loss of $5,000, your insurer would pay $4,000.";

        _mockChatCompletion
            .Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatMessageContent>
            {
                new(AuthorRole.Assistant, responseWithoutTone)
            });

        // Act
        var result = await _sut.ChatAsync("What does 'deductible' mean?");

        // Assert — defaults to "Professional" when no tag present
        Assert.Equal("Professional", result.Tone);
        Assert.Contains("deductible", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    // ────────────────────────────────────────────────────────────
    // BA-C2: Regulatory Disclaimer Tests
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ChatAsync_ResponseAlwaysContainsDisclaimer()
    {
        // Arrange — LLM response does NOT include the disclaimer (service must append it)
        var llmResponse = "Your claim is currently under review by the adjustments team.\n\n[TONE:Professional]";

        _mockChatCompletion
            .Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatMessageContent>
            {
                new(AuthorRole.Assistant, llmResponse)
            });

        // Act
        var result = await _sut.ChatAsync("What is the status of my water damage claim?");

        // Assert — disclaimer appended to response and set as dedicated property
        Assert.Contains(CustomerExperienceService.RegulatoryDisclaimer, result.Response);
        Assert.Equal(CustomerExperienceService.RegulatoryDisclaimer, result.Disclaimer);
    }

    [Fact]
    public async Task ChatAsync_DoesNotDuplicateDisclaimerIfLlmIncludesIt()
    {
        // Arrange — LLM already included the disclaimer in its response
        var llmResponse =
            "Your claim is currently under review.\n\n" +
            CustomerExperienceService.RegulatoryDisclaimer +
            "\n\n[TONE:Professional]";

        _mockChatCompletion
            .Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatMessageContent>
            {
                new(AuthorRole.Assistant, llmResponse)
            });

        // Act
        var result = await _sut.ChatAsync("What is happening with my flood damage claim?");

        // Assert — disclaimer appears exactly once (not duplicated)
        var disclaimerCount = CountOccurrences(result.Response, CustomerExperienceService.RegulatoryDisclaimer);
        Assert.Equal(1, disclaimerCount);
    }

    [Fact]
    public async Task ChatAsync_LlmThrows_ErrorResponseHasNoDisclaimer()
    {
        // Arrange — LLM throws (simulating provider failure)
        _mockChatCompletion
            .Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Provider unavailable"));

        // Act
        var result = await _sut.ChatAsync(SampleCustomerMessage);

        // Assert — error response should NOT have disclaimer (it's a fallback, not AI advice)
        Assert.Equal("Error", result.LlmProvider);
        Assert.Null(result.Disclaimer);
    }

    // ────────────────────────────────────────────────────────────
    // BA-H3: Output-Side PII Redaction Tests
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ChatAsync_RedactsPiiFromLlmOutput()
    {
        // Arrange — LLM echoes back PII despite redacted input
        var llmResponseWithPii =
            "I see you are John Smith with policy HO-2024-123456. Let me look into that for you.\n\n[TONE:Professional]";
        var redactedOutput =
            "I see you are [NAME-REDACTED] with policy [POLICY-REDACTED]. Let me look into that for you.";

        _mockChatCompletion
            .Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatMessageContent>
            {
                new(AuthorRole.Assistant, llmResponseWithPii)
            });

        // Simulate PII redactor stripping PII from the LLM output
        var callCount = 0;
        _mockPiiRedactor
            .Setup(p => p.Redact(It.IsAny<string>()))
            .Returns<string>(s =>
            {
                callCount++;
                // First call is for input redaction (pass-through), second is for output redaction
                if (callCount <= 1)
                    return s;
                return s.Replace("John Smith", "[NAME-REDACTED]")
                        .Replace("HO-2024-123456", "[POLICY-REDACTED]");
            });

        // Act
        var result = await _sut.ChatAsync("Can you check my claim please?");

        // Assert — output was PII-redacted
        Assert.DoesNotContain("John Smith", result.Response);
        Assert.DoesNotContain("HO-2024-123456", result.Response);
        Assert.Contains("[NAME-REDACTED]", result.Response);
        Assert.Contains("[POLICY-REDACTED]", result.Response);

        // Verify PII redactor was called at least twice (once for input, once for output)
        _mockPiiRedactor.Verify(p => p.Redact(It.IsAny<string>()), Times.AtLeast(2));
    }

    [Fact]
    public async Task StreamChatAsync_RedactsPiiFromMetadataResponse()
    {
        // Arrange — streaming LLM output contains echoed PII
        var streamingContents = new List<StreamingChatMessageContent>
        {
            new(AuthorRole.Assistant, "Hello John Smith, "),
            new(AuthorRole.Assistant, "your policy HO-2024-123456 is active. "),
            new(AuthorRole.Assistant, "[TONE:Professional]")
        };

        _mockChatCompletion
            .Setup(c => c.GetStreamingChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(),
                It.IsAny<CancellationToken>()))
            .Returns(streamingContents.ToAsyncEnumerable());

        // Simulate output PII redaction on the metadata response
        var callCount = 0;
        _mockPiiRedactor
            .Setup(p => p.Redact(It.IsAny<string>()))
            .Returns<string>(s =>
            {
                callCount++;
                // First call is for input, subsequent for output
                if (callCount <= 1)
                    return s;
                return s.Replace("John Smith", "[NAME-REDACTED]")
                        .Replace("HO-2024-123456", "[POLICY-REDACTED]");
            });

        // Act
        var chunks = new List<CustomerExperienceStreamChunk>();
        await foreach (var chunk in _sut.StreamChatAsync("Check my policy status please."))
        {
            chunks.Add(chunk);
        }

        // Assert — metadata response has PII redacted
        var metadata = chunks.First(c => c.Type == "metadata").Metadata;
        Assert.NotNull(metadata);
        Assert.DoesNotContain("John Smith", metadata.Response);
        Assert.DoesNotContain("HO-2024-123456", metadata.Response);
    }

    // ────────────────────────────────────────────────────────────
    // QA-C1: Mid-Stream Error Handling Tests
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task StreamChatAsync_MidStreamFailure_YieldsErrorAndDoneChunks()
    {
        // Arrange — streaming produces some content then throws mid-stream
        _mockChatCompletion
            .Setup(c => c.GetStreamingChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(),
                It.IsAny<CancellationToken>()))
            .Returns(ThrowingAsyncEnumerable(
                new StreamingChatMessageContent(AuthorRole.Assistant, "I understand your "),
                new StreamingChatMessageContent(AuthorRole.Assistant, "concern about "),
                new HttpRequestException("Connection lost mid-stream")));

        // Act
        var chunks = new List<CustomerExperienceStreamChunk>();
        await foreach (var chunk in _sut.StreamChatAsync(SampleCustomerMessage))
        {
            chunks.Add(chunk);
        }

        // Assert — content chunks from before the error, then error + done
        var contentChunks = chunks.Where(c => c.Type == "content").ToList();
        var errorChunks = chunks.Where(c => c.Type == "error").ToList();
        var doneChunks = chunks.Where(c => c.Type == "done").ToList();
        var metadataChunks = chunks.Where(c => c.Type == "metadata").ToList();

        Assert.Equal(2, contentChunks.Count);
        Assert.Contains("I understand your ", contentChunks[0].Content);
        Assert.Contains("concern about ", contentChunks[1].Content);
        Assert.Single(errorChunks);
        Assert.Contains("encountered an issue", errorChunks[0].Content, StringComparison.OrdinalIgnoreCase);
        Assert.Single(doneChunks);
        Assert.Empty(metadataChunks); // No metadata on error — stream was interrupted

        // Verify failure was reported for provider cooldown
        _mockKernelProvider.Verify(
            p => p.ReportFailure(It.IsAny<string>(), It.IsAny<Exception>()),
            Times.Once);
    }

    // ────────────────────────────────────────────────────────────
    // UX-H2: Tag Filtering in Stream Tests
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task StreamChatAsync_FiltersEscalateTagFromContentChunks()
    {
        // Arrange — streaming includes [ESCALATE:] tag alongside content
        var streamingContents = new List<StreamingChatMessageContent>
        {
            new(AuthorRole.Assistant, "I recommend speaking with your adjuster directly. "),
            new(AuthorRole.Assistant, "\n\n[TONE:Urgent]"),
            new(AuthorRole.Assistant, "\n[ESCALATE:Customer reports extensive water damage with no adjuster contact]")
        };

        _mockChatCompletion
            .Setup(c => c.GetStreamingChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(),
                It.IsAny<CancellationToken>()))
            .Returns(streamingContents.ToAsyncEnumerable());

        // Act
        var chunks = new List<CustomerExperienceStreamChunk>();
        await foreach (var chunk in _sut.StreamChatAsync("Nobody is responding to my claim about the flood damage."))
        {
            chunks.Add(chunk);
        }

        // Assert — no content chunk should contain [TONE: or [ESCALATE: tags
        var contentChunks = chunks.Where(c => c.Type == "content").ToList();
        foreach (var contentChunk in contentChunks)
        {
            Assert.DoesNotContain("[TONE:", contentChunk.Content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("[ESCALATE:", contentChunk.Content, StringComparison.OrdinalIgnoreCase);
        }

        // Metadata should still have the escalation info parsed correctly
        var metadata = chunks.First(c => c.Type == "metadata").Metadata;
        Assert.NotNull(metadata);
        Assert.Equal("Urgent", metadata.Tone);
        Assert.True(metadata.EscalationRecommended);
    }

    [Fact]
    public async Task StreamChatAsync_MixedChunkWithTextAndTag_YieldsOnlyTextPortion()
    {
        // Arrange — a single chunk contains both text and a [TONE: tag
        var streamingContents = new List<StreamingChatMessageContent>
        {
            new(AuthorRole.Assistant, "Your claim is being processed. "),
            new(AuthorRole.Assistant, "Please wait for your adjuster.\n\n[TONE:Professional]")
        };

        _mockChatCompletion
            .Setup(c => c.GetStreamingChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(),
                It.IsAny<CancellationToken>()))
            .Returns(streamingContents.ToAsyncEnumerable());

        // Act
        var chunks = new List<CustomerExperienceStreamChunk>();
        await foreach (var chunk in _sut.StreamChatAsync("When will my adjuster contact me about my claim?"))
        {
            chunks.Add(chunk);
        }

        // Assert — content chunks have text before the tag, but not the tag itself
        var contentChunks = chunks.Where(c => c.Type == "content").ToList();
        Assert.Equal(2, contentChunks.Count);
        Assert.Equal("Your claim is being processed. ", contentChunks[0].Content);
        Assert.Equal("Please wait for your adjuster.", contentChunks[1].Content);

        // No [TONE: in any content chunk
        foreach (var contentChunk in contentChunks)
        {
            Assert.DoesNotContain("[TONE:", contentChunk.Content, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ────────────────────────────────────────────────────────────
    // BA-M3: Expanded Escalation Keywords Tests
    // ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("The insurer is acting in bad faith by delaying my water damage claim.")]
    [InlineData("I'm filing a complaint with the attorney general about this denied claim.")]
    [InlineData("This is a breach of contract — my policy clearly covers flood damage.")]
    [InlineData("I want to report to the state about these unfair claims practices.")]
    [InlineData("Let me speak to someone in charge about my homeowners policy dispute.")]
    public async Task ChatAsync_NewEscalationKeywords_DetectsEscalation(string customerMessage)
    {
        // Arrange — LLM response contains the escalation keyword but no [ESCALATE:] tag
        var responseText =
            "I understand your concern and apologize for the difficulties you've experienced. " +
            customerMessage + "\n\n[TONE:Urgent]";

        _mockChatCompletion
            .Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatMessageContent>
            {
                new(AuthorRole.Assistant, responseText)
            });

        // Act
        var result = await _sut.ChatAsync(customerMessage);

        // Assert — escalation detected by expanded keyword list (BA-M3)
        Assert.True(result.EscalationRecommended, $"Expected escalation for: {customerMessage}");
        Assert.NotNull(result.EscalationReason);
    }

    // ────────────────────────────────────────────────────────────
    // Helper Methods
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Counts occurrences of a substring within a string (case-insensitive).
    /// </summary>
    private static int CountOccurrences(string text, string substring)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(substring, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            index += substring.Length;
        }
        return count;
    }

    /// <summary>
    /// Creates an <see cref="IAsyncEnumerable{T}"/> that yields items then throws an exception,
    /// simulating a mid-stream LLM failure for QA-C1 testing.
    /// </summary>
    private static async IAsyncEnumerable<StreamingChatMessageContent> ThrowingAsyncEnumerable(
        StreamingChatMessageContent item1,
        StreamingChatMessageContent item2,
        Exception exception)
    {
        yield return item1;
        yield return item2;
        await Task.CompletedTask;
        throw exception;
    }
}

/// <summary>
/// Helper extension to convert a <see cref="List{T}"/> to an <see cref="IAsyncEnumerable{T}"/>
/// for mocking Semantic Kernel streaming chat completions.
/// </summary>
internal static class AsyncEnumerableExtensions
{
    public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> source)
    {
        foreach (var item in source)
        {
            yield return item;
        }

        await Task.CompletedTask; // Ensure the method is truly async
    }
}
