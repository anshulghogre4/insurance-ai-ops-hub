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
/// Unit tests for CX Copilot Conversation Memory — verifies session creation,
/// message persistence, sliding window, PII redaction on stored messages,
/// and session-aware ChatHistory loading into the LLM.
/// </summary>
public class CxConversationMemoryTests
{
    private readonly Mock<IResilientKernelProvider> _mockKernelProvider;
    private readonly Mock<IChatCompletionService> _mockChatCompletion;
    private readonly Mock<IPIIRedactor> _mockPiiRedactor;
    private readonly Mock<ICxInteractionRepository> _mockAuditRepo;
    private readonly Mock<ICxConversationRepository> _mockConversationRepo;
    private readonly Mock<ILogger<CustomerExperienceService>> _mockLogger;
    private readonly CustomerExperienceService _sut;

    /// <summary>
    /// Realistic insurance customer message used across tests.
    /// </summary>
    private const string SampleCustomerMessage =
        "My water damage claim CLM-2026-11234 was filed two weeks ago but I haven't received any adjuster contact yet.";

    /// <summary>
    /// Typical LLM response for session-aware tests.
    /// </summary>
    private const string SampleLlmResponse =
        "I understand your frustration with the delay on your water damage claim. " +
        "Two weeks without adjuster contact is longer than our standard timeline. " +
        "I recommend calling the claims hotline to request an expedited assignment.\n\n" +
        "[TONE:Empathetic]";

    /// <summary>
    /// Test session ID.
    /// </summary>
    private const string TestSessionId = "a1b2c3d4-e5f6-7890-abcd-ef1234567890";

    public CxConversationMemoryTests()
    {
        _mockKernelProvider = new Mock<IResilientKernelProvider>();
        _mockChatCompletion = new Mock<IChatCompletionService>();
        _mockPiiRedactor = new Mock<IPIIRedactor>();
        _mockAuditRepo = new Mock<ICxInteractionRepository>();
        _mockConversationRepo = new Mock<ICxConversationRepository>();
        _mockLogger = new Mock<ILogger<CustomerExperienceService>>();

        // Default PII redaction: pass-through
        _mockPiiRedactor.Setup(p => p.Redact(It.IsAny<string>())).Returns<string>(s => s);

        // Build a test Kernel with the mocked IChatCompletionService
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(_mockChatCompletion.Object);
        var kernel = builder.Build();

        _mockKernelProvider.Setup(p => p.GetKernel()).Returns(kernel);
        _mockKernelProvider.Setup(p => p.ActiveProviderName).Returns("Groq");

        // Default conversation repo setup
        _mockConversationRepo
            .Setup(r => r.GetRecentTurnsAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(new List<CxMessageRecord>());
        _mockConversationRepo
            .Setup(r => r.AppendTurnAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        _sut = new CustomerExperienceService(
            _mockKernelProvider.Object,
            _mockPiiRedactor.Object,
            _mockAuditRepo.Object,
            _mockLogger.Object,
            _mockConversationRepo.Object);
    }

    // ────────────────────────────────────────────────────────────
    // Session-Aware ChatAsync Tests
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ChatAsync_WithSessionId_LoadsHistoryIntoChat()
    {
        // Arrange — existing conversation with 2 prior turns
        var priorTurns = new List<CxMessageRecord>
        {
            new() { Role = "user", Content = "What does my homeowners policy cover?", Timestamp = DateTime.UtcNow.AddMinutes(-5) },
            new() { Role = "assistant", Content = "Your homeowners policy covers dwelling, personal property, and liability.", Timestamp = DateTime.UtcNow.AddMinutes(-4) }
        };

        _mockConversationRepo
            .Setup(r => r.GetRecentTurnsAsync(TestSessionId, CustomerExperienceService.MaxConversationTurns))
            .ReturnsAsync(priorTurns);

        ChatHistory? capturedHistory = null;
        _mockChatCompletion
            .Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(),
                It.IsAny<CancellationToken>()))
            .Callback<ChatHistory, PromptExecutionSettings?, Kernel?, CancellationToken>((history, _, _, _) =>
            {
                capturedHistory = history;
            })
            .ReturnsAsync(new List<ChatMessageContent>
            {
                new(AuthorRole.Assistant, SampleLlmResponse)
            });

        // Act
        var result = await _sut.ChatAsync(SampleCustomerMessage, sessionId: TestSessionId);

        // Assert — ChatHistory should have: System + prior user + prior assistant + current user = 4 messages
        Assert.NotNull(capturedHistory);
        Assert.Equal(4, capturedHistory.Count);
        Assert.Equal(AuthorRole.System, capturedHistory[0].Role);
        Assert.Equal(AuthorRole.User, capturedHistory[1].Role);
        Assert.Contains("homeowners policy", capturedHistory[1].Content);
        Assert.Equal(AuthorRole.Assistant, capturedHistory[2].Role);
        Assert.Contains("dwelling", capturedHistory[2].Content);
        Assert.Equal(AuthorRole.User, capturedHistory[3].Role);
        Assert.Contains("water damage claim", capturedHistory[3].Content);

        // Verify conversation history was loaded
        _mockConversationRepo.Verify(
            r => r.GetRecentTurnsAsync(TestSessionId, CustomerExperienceService.MaxConversationTurns),
            Times.Once);
    }

    [Fact]
    public async Task ChatAsync_WithSessionId_SavesBothTurnsAfterResponse()
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
        var result = await _sut.ChatAsync(SampleCustomerMessage, sessionId: TestSessionId);

        // Assert — both user and assistant turns saved
        _mockConversationRepo.Verify(
            r => r.AppendTurnAsync(TestSessionId, "user", It.IsAny<string>(), CustomerExperienceService.MaxConversationTurns),
            Times.Once);
        _mockConversationRepo.Verify(
            r => r.AppendTurnAsync(TestSessionId, "assistant", It.IsAny<string>(), CustomerExperienceService.MaxConversationTurns),
            Times.Once);
    }

    [Fact]
    public async Task ChatAsync_WithoutSessionId_DoesNotLoadOrSaveHistory()
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

        // Act — no session ID (stateless mode)
        var result = await _sut.ChatAsync(SampleCustomerMessage);

        // Assert — conversation repo never called
        _mockConversationRepo.Verify(
            r => r.GetRecentTurnsAsync(It.IsAny<string>(), It.IsAny<int>()),
            Times.Never);
        _mockConversationRepo.Verify(
            r => r.AppendTurnAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()),
            Times.Never);
    }

    [Fact]
    public async Task ChatAsync_WithSessionId_PiiRedactsStoredMessages()
    {
        // Arrange — PII in the message
        var rawMessage = "My SSN is 123-45-6789 and claim CLM-2026-11234 is pending.";
        var redactedMessage = "My SSN is [SSN-REDACTED] and claim [CLAIM-REDACTED] is pending.";

        _mockPiiRedactor
            .Setup(p => p.Redact(rawMessage))
            .Returns(redactedMessage);

        _mockChatCompletion
            .Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatMessageContent>
            {
                new(AuthorRole.Assistant, "I can help you check the status of your claim. [TONE:Professional]")
            });

        // Act
        await _sut.ChatAsync(rawMessage, sessionId: TestSessionId);

        // Assert — the REDACTED message was saved, not raw PII
        _mockConversationRepo.Verify(
            r => r.AppendTurnAsync(TestSessionId, "user", redactedMessage, CustomerExperienceService.MaxConversationTurns),
            Times.Once);
    }

    [Fact]
    public async Task ChatAsync_WithEmptySessionHistory_StillWorks()
    {
        // Arrange — session exists but has no prior messages
        _mockConversationRepo
            .Setup(r => r.GetRecentTurnsAsync(TestSessionId, CustomerExperienceService.MaxConversationTurns))
            .ReturnsAsync(new List<CxMessageRecord>());

        ChatHistory? capturedHistory = null;
        _mockChatCompletion
            .Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(),
                It.IsAny<CancellationToken>()))
            .Callback<ChatHistory, PromptExecutionSettings?, Kernel?, CancellationToken>((history, _, _, _) =>
            {
                capturedHistory = history;
            })
            .ReturnsAsync(new List<ChatMessageContent>
            {
                new(AuthorRole.Assistant, SampleLlmResponse)
            });

        // Act
        var result = await _sut.ChatAsync(SampleCustomerMessage, sessionId: TestSessionId);

        // Assert — System + current user = 2 messages (no prior turns)
        Assert.NotNull(capturedHistory);
        Assert.Equal(2, capturedHistory.Count);
        Assert.Equal(AuthorRole.System, capturedHistory[0].Role);
        Assert.Equal(AuthorRole.User, capturedHistory[1].Role);
    }

    [Fact]
    public async Task ChatAsync_ConversationRepoFailure_GracefullyDegrades()
    {
        // Arrange — conversation repo throws on load
        _mockConversationRepo
            .Setup(r => r.GetRecentTurnsAsync(TestSessionId, CustomerExperienceService.MaxConversationTurns))
            .ThrowsAsync(new InvalidOperationException("Database connection lost"));

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

        // Act — should NOT throw, gracefully degrades to stateless
        var result = await _sut.ChatAsync(SampleCustomerMessage, sessionId: TestSessionId);

        // Assert — response still returned successfully
        Assert.NotNull(result);
        Assert.Contains("frustration", result.Response);
        Assert.Equal("Empathetic", result.Tone);
    }

    // ────────────────────────────────────────────────────────────
    // Session-Aware StreamChatAsync Tests
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task StreamChatAsync_WithSessionId_LoadsHistoryAndSavesTurns()
    {
        // Arrange — existing conversation
        var priorTurns = new List<CxMessageRecord>
        {
            new() { Role = "user", Content = "What is my deductible for water damage?", Timestamp = DateTime.UtcNow.AddMinutes(-3) },
            new() { Role = "assistant", Content = "Your water damage deductible is $1,000 per occurrence.", Timestamp = DateTime.UtcNow.AddMinutes(-2) }
        };

        _mockConversationRepo
            .Setup(r => r.GetRecentTurnsAsync(TestSessionId, CustomerExperienceService.MaxConversationTurns))
            .ReturnsAsync(priorTurns);

        var streamingContents = new List<StreamingChatMessageContent>
        {
            new(AuthorRole.Assistant, "Based on our prior discussion, "),
            new(AuthorRole.Assistant, "your claim is being processed. "),
            new(AuthorRole.Assistant, "[TONE:Professional]")
        };

        ChatHistory? capturedHistory = null;
        _mockChatCompletion
            .Setup(c => c.GetStreamingChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(),
                It.IsAny<CancellationToken>()))
            .Callback<ChatHistory, PromptExecutionSettings?, Kernel?, CancellationToken>((history, _, _, _) =>
            {
                capturedHistory = history;
            })
            .Returns(streamingContents.ToAsyncEnumerable());

        // Act
        var chunks = new List<CustomerExperienceStreamChunk>();
        await foreach (var chunk in _sut.StreamChatAsync(SampleCustomerMessage, sessionId: TestSessionId))
        {
            chunks.Add(chunk);
        }

        // Assert — ChatHistory includes prior turns
        Assert.NotNull(capturedHistory);
        Assert.Equal(4, capturedHistory.Count); // System + prior user + prior assistant + current user

        // Assert — both turns saved
        _mockConversationRepo.Verify(
            r => r.AppendTurnAsync(TestSessionId, "user", It.IsAny<string>(), CustomerExperienceService.MaxConversationTurns),
            Times.Once);
        _mockConversationRepo.Verify(
            r => r.AppendTurnAsync(TestSessionId, "assistant", It.IsAny<string>(), CustomerExperienceService.MaxConversationTurns),
            Times.Once);
    }

    [Fact]
    public async Task StreamChatAsync_WithoutSessionId_DoesNotLoadOrSaveHistory()
    {
        // Arrange
        var streamingContents = new List<StreamingChatMessageContent>
        {
            new(AuthorRole.Assistant, "I can help with your claim. "),
            new(AuthorRole.Assistant, "[TONE:Professional]")
        };

        _mockChatCompletion
            .Setup(c => c.GetStreamingChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(),
                It.IsAny<CancellationToken>()))
            .Returns(streamingContents.ToAsyncEnumerable());

        // Act — no session ID
        var chunks = new List<CustomerExperienceStreamChunk>();
        await foreach (var chunk in _sut.StreamChatAsync(SampleCustomerMessage))
        {
            chunks.Add(chunk);
        }

        // Assert — conversation repo never called
        _mockConversationRepo.Verify(
            r => r.GetRecentTurnsAsync(It.IsAny<string>(), It.IsAny<int>()),
            Times.Never);
        _mockConversationRepo.Verify(
            r => r.AppendTurnAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()),
            Times.Never);
    }

    // ────────────────────────────────────────────────────────────
    // Backward Compatibility Tests
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ChatAsync_NullConversationRepo_WorksWithoutMemory()
    {
        // Arrange — service created without conversation repo (backward compat)
        var service = new CustomerExperienceService(
            _mockKernelProvider.Object,
            _mockPiiRedactor.Object,
            _mockAuditRepo.Object,
            _mockLogger.Object,
            conversationRepo: null);

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

        // Act — passing session ID with null repo should not throw
        var result = await service.ChatAsync(SampleCustomerMessage, sessionId: TestSessionId);

        // Assert — still returns valid response
        Assert.NotNull(result);
        Assert.Equal("Empathetic", result.Tone);
        Assert.False(string.IsNullOrWhiteSpace(result.Response));
    }

    // ────────────────────────────────────────────────────────────
    // Sliding Window Tests
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ChatAsync_WithFullHistory_LoadsOnlyMaxTurns()
    {
        // Arrange — 10 prior turns (max window)
        var priorTurns = Enumerable.Range(1, 10).Select(i => new CxMessageRecord
        {
            Role = i % 2 == 1 ? "user" : "assistant",
            Content = $"Turn {i} about policy coverage for auto insurance claim CLM-2026-{i:D5}.",
            Timestamp = DateTime.UtcNow.AddMinutes(-10 + i)
        }).ToList();

        _mockConversationRepo
            .Setup(r => r.GetRecentTurnsAsync(TestSessionId, CustomerExperienceService.MaxConversationTurns))
            .ReturnsAsync(priorTurns);

        ChatHistory? capturedHistory = null;
        _mockChatCompletion
            .Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(),
                It.IsAny<CancellationToken>()))
            .Callback<ChatHistory, PromptExecutionSettings?, Kernel?, CancellationToken>((history, _, _, _) =>
            {
                capturedHistory = history;
            })
            .ReturnsAsync(new List<ChatMessageContent>
            {
                new(AuthorRole.Assistant, SampleLlmResponse)
            });

        // Act
        var result = await _sut.ChatAsync(SampleCustomerMessage, sessionId: TestSessionId);

        // Assert — System(1) + 10 prior turns + current user(1) = 12 messages
        Assert.NotNull(capturedHistory);
        Assert.Equal(12, capturedHistory.Count);
    }
}
