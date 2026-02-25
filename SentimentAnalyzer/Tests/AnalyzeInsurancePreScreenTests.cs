using Microsoft.Extensions.Logging;
using Moq;
using SentimentAnalyzer.Agents.Models;
using SentimentAnalyzer.Agents.Orchestration;
using SentimentAnalyzer.API.Data;
using SentimentAnalyzer.API.Data.Entities;
using SentimentAnalyzer.API.Features.Insurance.Commands;
using SentimentAnalyzer.API.Services.Multimodal;
using SentimentAnalyzer.Domain.Enums;
using Xunit;

namespace SentimentAnalyzer.Tests;

/// <summary>
/// Tests for AnalyzeInsuranceHandler's FinBERT pre-screening integration.
/// Verifies short-circuit on high confidence and fallthrough to orchestration otherwise.
/// </summary>
public class AnalyzeInsurancePreScreenTests
{
    private readonly Mock<IAnalysisOrchestrator> _orchestratorMock = new();
    private readonly Mock<IAnalysisRepository> _repositoryMock = new();
    private readonly Mock<ILogger<AnalyzeInsuranceHandler>> _loggerMock = new();
    private readonly Mock<IFinancialSentimentPreScreener> _preScreenerMock = new();
    private readonly Mock<IPIIRedactor> _piiRedactorMock = new();

    private AnalyzeInsuranceHandler CreateHandler(
        bool includePreScreener = true,
        bool includePiiRedactor = true,
        bool setupDefaultRepository = true)
    {
        if (setupDefaultRepository)
        {
            _repositoryMock.Setup(r => r.SaveAsync(It.IsAny<AnalysisRecord>()))
                .Returns(Task.CompletedTask);
        }

        return new AnalyzeInsuranceHandler(
            _orchestratorMock.Object,
            _repositoryMock.Object,
            _loggerMock.Object,
            includePreScreener ? _preScreenerMock.Object : null,
            includePiiRedactor ? _piiRedactorMock.Object : null);
    }

    private static AgentAnalysisResult CreateFallbackAgentResult(string sentiment = "Neutral")
    {
        return new AgentAnalysisResult
        {
            IsSuccess = true,
            Sentiment = sentiment,
            ConfidenceScore = 0.72,
            Explanation = "Full orchestration result."
        };
    }

    [Fact]
    public async Task Handle_WithHighConfidencePreScreen_SkipsOrchestration()
    {
        _piiRedactorMock.Setup(r => r.Redact(It.IsAny<string>())).Returns<string>(s => s);
        _preScreenerMock.Setup(p => p.PreScreenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinancialSentimentResult
            {
                IsSuccess = true,
                Sentiment = "negative",
                TopScore = 0.94,
                Scores = new() { ["negative"] = 0.94, ["neutral"] = 0.04, ["positive"] = 0.02 },
                IsHighConfidence = true
            });

        var handler = CreateHandler();
        var command = new AnalyzeInsuranceCommand(
            "I am very unhappy with my claim denial on policy HO-2024-123456.");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal("Negative", result.Sentiment);
        Assert.Equal(0.94, result.ConfidenceScore);
        Assert.Contains("FinBERT", result.Explanation);
        // Orchestrator should NOT have been called
        _orchestratorMock.Verify(
            o => o.AnalyzeAsync(It.IsAny<string>(), It.IsAny<InteractionType>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithLowConfidencePreScreen_FallsThroughToOrchestration()
    {
        _piiRedactorMock.Setup(r => r.Redact(It.IsAny<string>())).Returns<string>(s => s);
        _preScreenerMock.Setup(p => p.PreScreenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinancialSentimentResult
            {
                IsSuccess = true,
                Sentiment = "neutral",
                TopScore = 0.45,
                Scores = new() { ["neutral"] = 0.45, ["negative"] = 0.35, ["positive"] = 0.20 },
                IsHighConfidence = false
            });
        _orchestratorMock.Setup(o => o.AnalyzeAsync(It.IsAny<string>(), It.IsAny<InteractionType>()))
            .ReturnsAsync(CreateFallbackAgentResult());

        var handler = CreateHandler();
        var result = await handler.Handle(
            new AnalyzeInsuranceCommand("Policy renewal notice received."),
            CancellationToken.None);

        _orchestratorMock.Verify(
            o => o.AnalyzeAsync(It.IsAny<string>(), It.IsAny<InteractionType>()),
            Times.Once);
        Assert.Equal("Full orchestration result.", result.Explanation);
    }

    [Fact]
    public async Task Handle_WithPreScreenerFailure_FallsThroughToOrchestration()
    {
        _piiRedactorMock.Setup(r => r.Redact(It.IsAny<string>())).Returns<string>(s => s);
        _preScreenerMock.Setup(p => p.PreScreenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinancialSentimentResult
            {
                IsSuccess = false,
                ErrorMessage = "FinBERT model is loading."
            });
        _orchestratorMock.Setup(o => o.AnalyzeAsync(It.IsAny<string>(), It.IsAny<InteractionType>()))
            .ReturnsAsync(CreateFallbackAgentResult("Negative"));

        var handler = CreateHandler();
        var result = await handler.Handle(
            new AnalyzeInsuranceCommand("Claim denied under policy HO-2024-999."),
            CancellationToken.None);

        _orchestratorMock.Verify(
            o => o.AnalyzeAsync(It.IsAny<string>(), It.IsAny<InteractionType>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithNoPreScreenerRegistered_FallsThroughToOrchestration()
    {
        _orchestratorMock.Setup(o => o.AnalyzeAsync(It.IsAny<string>(), It.IsAny<InteractionType>()))
            .ReturnsAsync(CreateFallbackAgentResult("Positive"));

        var handler = CreateHandler(includePreScreener: false);
        var result = await handler.Handle(
            new AnalyzeInsuranceCommand("Great customer service experience!"),
            CancellationToken.None);

        _orchestratorMock.Verify(
            o => o.AnalyzeAsync(It.IsAny<string>(), It.IsAny<InteractionType>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithPreScreenerException_FallsThroughToOrchestration()
    {
        _piiRedactorMock.Setup(r => r.Redact(It.IsAny<string>())).Returns<string>(s => s);
        _preScreenerMock.Setup(p => p.PreScreenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));
        _orchestratorMock.Setup(o => o.AnalyzeAsync(It.IsAny<string>(), It.IsAny<InteractionType>()))
            .ReturnsAsync(CreateFallbackAgentResult("Negative"));

        var handler = CreateHandler();
        var result = await handler.Handle(
            new AnalyzeInsuranceCommand("I want to file a complaint about my adjuster."),
            CancellationToken.None);

        _orchestratorMock.Verify(
            o => o.AnalyzeAsync(It.IsAny<string>(), It.IsAny<InteractionType>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_PreScreenAppliesPiiRedaction_BeforeSendingToFinBert()
    {
        var rawText = "Policy HO-2024-123456 claim is terrible.";
        var redactedText = "[POLICY-REDACTED] claim is terrible.";

        _piiRedactorMock.Setup(r => r.Redact(rawText)).Returns(redactedText);
        _preScreenerMock.Setup(p => p.PreScreenAsync(redactedText, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinancialSentimentResult
            {
                IsSuccess = true,
                Sentiment = "negative",
                TopScore = 0.92,
                Scores = new() { ["negative"] = 0.92, ["neutral"] = 0.05, ["positive"] = 0.03 },
                IsHighConfidence = true
            });

        var handler = CreateHandler();
        await handler.Handle(new AnalyzeInsuranceCommand(rawText), CancellationToken.None);

        // Verify the pre-screener received the REDACTED text, not the raw text
        _preScreenerMock.Verify(
            p => p.PreScreenAsync(redactedText, It.IsAny<CancellationToken>()),
            Times.Once);
        _preScreenerMock.Verify(
            p => p.PreScreenAsync(rawText, It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ========================================================================
    // Complaint Escalation Structured Logging
    // ========================================================================

    [Theory]
    [InlineData("High")]
    [InlineData("Critical")]
    public async Task Handle_HighComplaintEscalationRisk_LogsWarning(string riskLevel)
    {
        _orchestratorMock.Setup(o => o.AnalyzeAsync(It.IsAny<string>(), It.IsAny<InteractionType>()))
            .ReturnsAsync(new AgentAnalysisResult
            {
                IsSuccess = true,
                Sentiment = "Negative",
                ConfidenceScore = 0.92,
                Explanation = "Angry policyholder threatening legal action",
                InsuranceAnalysis = new InsuranceAnalysisDetail
                {
                    RiskIndicators = new RiskIndicatorDetail
                    {
                        ComplaintEscalationRisk = riskLevel,
                        ChurnRisk = "High",
                        FraudIndicators = "None"
                    },
                    InteractionType = "Complaint",
                    KeyTopics = ["claim denial", "attorney"]
                }
            });

        var handler = CreateHandler(includePreScreener: false);
        await handler.Handle(
            new AnalyzeInsuranceCommand("I will file a complaint with the department of insurance!", "Complaint", "CUST-001"),
            CancellationToken.None);

        // Verify structured warning was logged for complaint escalation
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("COMPLAINT_ESCALATION")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_LowComplaintEscalationRisk_DoesNotLogWarning()
    {
        _orchestratorMock.Setup(o => o.AnalyzeAsync(It.IsAny<string>(), It.IsAny<InteractionType>()))
            .ReturnsAsync(CreateFallbackAgentResult("Positive"));

        var handler = CreateHandler(includePreScreener: false);
        await handler.Handle(
            new AnalyzeInsuranceCommand("Happy with my policy renewal.", "General"),
            CancellationToken.None);

        // No COMPLAINT_ESCALATION warning should be logged for low-risk
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("COMPLAINT_ESCALATION")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    // ========================================================================
    // PII Redaction before DB Persistence (P0 Security Fix)
    // ========================================================================

    [Fact]
    public async Task Handle_FullOrchestration_PersistsRedactedInputText()
    {
        var rawText = "Claim denied on policy HO-2024-123456. SSN 123-45-6789.";
        var redactedText = "Claim denied on policy [POLICY-REDACTED]. SSN [SSN-REDACTED].";

        _piiRedactorMock.Setup(r => r.Redact(It.IsAny<string>())).Returns<string>(s => s);
        _piiRedactorMock.Setup(r => r.Redact(rawText)).Returns(redactedText);
        _orchestratorMock.Setup(o => o.AnalyzeAsync(It.IsAny<string>(), It.IsAny<InteractionType>()))
            .ReturnsAsync(CreateFallbackAgentResult());

        var handler = CreateHandler(includePreScreener: false, includePiiRedactor: true, setupDefaultRepository: false);

        AnalysisRecord? savedRecord = null;
        _repositoryMock.Setup(r => r.SaveAsync(It.IsAny<AnalysisRecord>()))
            .Callback<AnalysisRecord>(r => savedRecord = r)
            .Returns(Task.CompletedTask);

        await handler.Handle(new AnalyzeInsuranceCommand(rawText), CancellationToken.None);

        Assert.NotNull(savedRecord);
        Assert.Equal(redactedText, savedRecord!.InputText);
        Assert.DoesNotContain("HO-2024-123456", savedRecord.InputText);
        Assert.DoesNotContain("123-45-6789", savedRecord.InputText);
    }

    [Fact]
    public async Task Handle_PreScreenShortCircuit_PersistsRedactedInputText()
    {
        var rawText = "My policy HO-9999-555555 was cancelled unfairly.";
        var redactedText = "My policy [POLICY-REDACTED] was cancelled unfairly.";

        _piiRedactorMock.Setup(r => r.Redact(It.IsAny<string>())).Returns<string>(s => s);
        _piiRedactorMock.Setup(r => r.Redact(rawText)).Returns(redactedText);
        _preScreenerMock.Setup(p => p.PreScreenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinancialSentimentResult
            {
                IsSuccess = true,
                Sentiment = "negative",
                TopScore = 0.96,
                Scores = new() { ["negative"] = 0.96, ["neutral"] = 0.03, ["positive"] = 0.01 },
                IsHighConfidence = true
            });

        var handler = CreateHandler(setupDefaultRepository: false);

        AnalysisRecord? savedRecord = null;
        _repositoryMock.Setup(r => r.SaveAsync(It.IsAny<AnalysisRecord>()))
            .Callback<AnalysisRecord>(r => savedRecord = r)
            .Returns(Task.CompletedTask);

        await handler.Handle(new AnalyzeInsuranceCommand(rawText), CancellationToken.None);

        Assert.NotNull(savedRecord);
        Assert.Equal(redactedText, savedRecord!.InputText);
        Assert.DoesNotContain("HO-9999-555555", savedRecord.InputText);
    }

    [Fact]
    public async Task Handle_WithNoPiiRedactor_PersistsRawInputText()
    {
        var rawText = "My policy HO-2024-100001 is great.";

        _orchestratorMock.Setup(o => o.AnalyzeAsync(It.IsAny<string>(), It.IsAny<InteractionType>()))
            .ReturnsAsync(CreateFallbackAgentResult("Positive"));

        var handler = CreateHandler(includePreScreener: false, includePiiRedactor: false, setupDefaultRepository: false);

        AnalysisRecord? savedRecord = null;
        _repositoryMock.Setup(r => r.SaveAsync(It.IsAny<AnalysisRecord>()))
            .Callback<AnalysisRecord>(r => savedRecord = r)
            .Returns(Task.CompletedTask);

        await handler.Handle(new AnalyzeInsuranceCommand(rawText), CancellationToken.None);

        Assert.NotNull(savedRecord);
        // Without PII redactor, raw text is persisted (backward compat)
        Assert.Equal(rawText, savedRecord!.InputText);
    }

    [Fact]
    public async Task Handle_ExplanationAlsoPiiRedacted_BeforePersisting()
    {
        var rawText = "Test input for analysis.";
        _piiRedactorMock.Setup(r => r.Redact(It.IsAny<string>())).Returns<string>(s => s);
        _piiRedactorMock.Setup(r => r.Redact(It.Is<string>(s => s.Contains("Full orchestration"))))
            .Returns("Full orchestration result [REDACTED].");

        _orchestratorMock.Setup(o => o.AnalyzeAsync(It.IsAny<string>(), It.IsAny<InteractionType>()))
            .ReturnsAsync(CreateFallbackAgentResult());

        var handler = CreateHandler(includePreScreener: false, includePiiRedactor: true, setupDefaultRepository: false);

        AnalysisRecord? savedRecord = null;
        _repositoryMock.Setup(r => r.SaveAsync(It.IsAny<AnalysisRecord>()))
            .Callback<AnalysisRecord>(r => savedRecord = r)
            .Returns(Task.CompletedTask);

        await handler.Handle(new AnalyzeInsuranceCommand(rawText), CancellationToken.None);

        Assert.NotNull(savedRecord);
        // Explanation should also be PII-redacted (defense-in-depth)
        Assert.Contains("[REDACTED]", savedRecord!.Explanation);
    }
}
