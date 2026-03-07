using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;
using SentimentAnalyzer.Agents.Orchestration;
using SentimentAnalyzer.API.Models;
using SentimentAnalyzer.API.Services.Documents.RAGQuery;

namespace SentimentAnalyzer.Tests;

/// <summary>
/// Tests for RAG Query Agent Team: QueryReformulator, AnswerEvaluator, CrossDocReasoner.
/// Uses Moq to avoid actual LLM calls while verifying pipeline logic.
/// </summary>
public class RAGQueryAgentTeamTests
{
    private readonly Mock<IResilientKernelProvider> _mockKernelProvider;
    private readonly Mock<IChatCompletionService> _mockChatService;

    public RAGQueryAgentTeamTests()
    {
        _mockKernelProvider = new Mock<IResilientKernelProvider>();
        _mockChatService = new Mock<IChatCompletionService>();

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(_mockChatService.Object);
        var kernel = kernelBuilder.Build();

        _mockKernelProvider.Setup(x => x.GetKernel()).Returns(kernel);
    }

    // ==================== QueryReformulator ====================

    [Fact]
    public async Task QueryReformulator_ShortQuery_SkipsReformulation()
    {
        var reformulator = new QueryReformulatorService(
            _mockKernelProvider.Object,
            Mock.Of<ILogger<QueryReformulatorService>>());

        var result = await reformulator.ReformulateAsync("deductible?");

        Assert.False(result.WasReformulated);
        Assert.Single(result.ReformulatedQueries);
        Assert.Equal("deductible?", result.ReformulatedQueries[0]);
    }

    [Fact]
    public async Task QueryReformulator_LongQuery_CallsLLM()
    {
        SetupChatResponse("[\"flood coverage exclusions\", \"water damage policy terms\"]");

        var reformulator = new QueryReformulatorService(
            _mockKernelProvider.Object,
            Mock.Of<ILogger<QueryReformulatorService>>());

        var result = await reformulator.ReformulateAsync(
            "What does my homeowner policy cover for flood damage?");

        Assert.True(result.WasReformulated);
        Assert.Equal(2, result.ReformulatedQueries.Count);
        Assert.Contains("flood coverage exclusions", result.ReformulatedQueries);
    }

    [Fact]
    public async Task QueryReformulator_LLMFailure_ReturnsOriginalQuery()
    {
        _mockChatService
            .Setup(x => x.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(), It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("LLM provider unavailable"));

        var reformulator = new QueryReformulatorService(
            _mockKernelProvider.Object,
            Mock.Of<ILogger<QueryReformulatorService>>());

        var result = await reformulator.ReformulateAsync(
            "What are my coverage limits for auto collision?");

        Assert.False(result.WasReformulated);
        Assert.Single(result.ReformulatedQueries);
        Assert.Equal("What are my coverage limits for auto collision?", result.ReformulatedQueries[0]);
    }

    // ==================== AnswerEvaluator ====================

    [Fact]
    public async Task AnswerEvaluator_GroundedAnswer_ReturnsHighQuality()
    {
        SetupChatResponse("""
            {"qualityScore": 0.92, "isGrounded": true, "ungroundedClaims": [],
             "citationsValid": true, "isComplete": true, "suggestions": []}
            """);

        var evaluator = new AnswerEvaluatorService(
            _mockKernelProvider.Object,
            Mock.Of<ILogger<AnswerEvaluatorService>>());

        var result = await evaluator.EvaluateAsync(
            "What is my deductible?",
            "Your comprehensive deductible is $500 per occurrence [1].",
            ["The comprehensive coverage deductible is $500 per occurrence."]);

        Assert.True(result.IsGrounded);
        Assert.True(result.CitationsValid);
        Assert.True(result.IsComplete);
        Assert.True(result.QualityScore > 0.9);
    }

    [Fact]
    public async Task AnswerEvaluator_UngroundedAnswer_FlagsIssues()
    {
        SetupChatResponse("""
            {"qualityScore": 0.35, "isGrounded": false,
             "ungroundedClaims": ["The deductible waiver applies automatically after 3 claims"],
             "citationsValid": true, "isComplete": false,
             "suggestions": ["Remove unsubstantiated deductible waiver claim"]}
            """);

        var evaluator = new AnswerEvaluatorService(
            _mockKernelProvider.Object,
            Mock.Of<ILogger<AnswerEvaluatorService>>());

        var result = await evaluator.EvaluateAsync(
            "What is my deductible?",
            "Your deductible is $500. The deductible waiver applies automatically after 3 claims.",
            ["The comprehensive coverage deductible is $500 per occurrence."]);

        Assert.False(result.IsGrounded);
        Assert.Single(result.UngroundedClaims);
        Assert.True(result.QualityScore < 0.5);
    }

    [Fact]
    public async Task AnswerEvaluator_LLMFailure_ReturnsDefaultPass()
    {
        _mockChatService
            .Setup(x => x.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(), It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Service unavailable"));

        var evaluator = new AnswerEvaluatorService(
            _mockKernelProvider.Object,
            Mock.Of<ILogger<AnswerEvaluatorService>>());

        var result = await evaluator.EvaluateAsync(
            "Coverage?", "Your policy covers...",
            ["Coverage includes comprehensive and collision."]);

        // Default pass-through: don't block user responses
        Assert.True(result.IsGrounded);
        Assert.Equal(0.7, result.QualityScore);
    }

    // ==================== CrossDocReasoner ====================

    [Fact]
    public async Task CrossDocReasoner_SingleDocument_SkipsSynthesis()
    {
        var reasoner = new CrossDocReasonerService(
            _mockKernelProvider.Object,
            Mock.Of<ILogger<CrossDocReasonerService>>());

        var citations = new List<DocumentCitation>
        {
            new() { DocumentId = 1, FileName = "policy.pdf", SectionName = "COVERAGE" },
            new() { DocumentId = 1, FileName = "policy.pdf", SectionName = "EXCLUSIONS" }
        };

        var result = await reasoner.SynthesizeAsync(
            "What's covered?", citations,
            ["Coverage includes...", "Exclusions include..."]);

        Assert.False(result.WasSynthesized);
        Assert.Equal(1, result.DocumentCount);
    }

    [Fact]
    public async Task CrossDocReasoner_MultipleDocuments_SynthesizesAnswer()
    {
        SetupChatResponse("""
            {"synthesizedAnswer": "Based on both documents, your auto policy covers collision damage up to $50,000 [1], while the endorsement adds rental car coverage at $50/day [2].",
             "documentCount": 2,
             "conflicts": ["Base policy excludes rental cars but endorsement adds coverage"],
             "wasSynthesized": true}
            """);

        var reasoner = new CrossDocReasonerService(
            _mockKernelProvider.Object,
            Mock.Of<ILogger<CrossDocReasonerService>>());

        var citations = new List<DocumentCitation>
        {
            new() { DocumentId = 1, FileName = "auto-policy.pdf", SectionName = "COVERAGE" },
            new() { DocumentId = 2, FileName = "endorsement-A.pdf", SectionName = "RENTAL_COVERAGE" }
        };

        var result = await reasoner.SynthesizeAsync(
            "Does my auto policy cover rental cars?", citations,
            ["Collision coverage up to $50,000. Rental car coverage excluded.",
             "This endorsement adds rental car reimbursement at $50 per day."]);

        Assert.True(result.WasSynthesized);
        Assert.Equal(2, result.DocumentCount);
        Assert.Single(result.Conflicts);
        Assert.Contains("endorsement", result.SynthesizedAnswer);
    }

    [Fact]
    public async Task CrossDocReasoner_LLMFailure_ReturnsUnsynthesized()
    {
        _mockChatService
            .Setup(x => x.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(), It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("LLM timeout"));

        var reasoner = new CrossDocReasonerService(
            _mockKernelProvider.Object,
            Mock.Of<ILogger<CrossDocReasonerService>>());

        var citations = new List<DocumentCitation>
        {
            new() { DocumentId = 1, FileName = "doc-a.pdf", SectionName = "SEC_A" },
            new() { DocumentId = 2, FileName = "doc-b.pdf", SectionName = "SEC_B" }
        };

        var result = await reasoner.SynthesizeAsync(
            "Compare coverages", citations, ["Coverage A", "Coverage B"]);

        Assert.False(result.WasSynthesized);
        Assert.Equal(2, result.DocumentCount);
    }

    // ==================== Helpers ====================

    private void SetupChatResponse(string responseText)
    {
        _mockChatService
            .Setup(x => x.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatMessageContent>
            {
                new(AuthorRole.Assistant, responseText)
            });
    }
}
