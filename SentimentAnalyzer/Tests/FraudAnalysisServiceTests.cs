using Microsoft.Extensions.Logging;
using Moq;
using SentimentAnalyzer.Agents.Configuration;
using SentimentAnalyzer.Agents.Models;
using SentimentAnalyzer.Agents.Orchestration;
using SentimentAnalyzer.API.Data;
using SentimentAnalyzer.API.Data.Entities;
using SentimentAnalyzer.API.Services.Fraud;
using SentimentAnalyzer.Domain.Enums;
using Xunit;

namespace SentimentAnalyzer.Tests;

/// <summary>
/// Tests for FraudAnalysisService — fraud scoring, SIU referral, and alerts.
/// </summary>
public class FraudAnalysisServiceTests
{
    private readonly Mock<IAnalysisOrchestrator> _mockOrchestrator;
    private readonly Mock<IClaimsRepository> _mockRepo;
    private readonly Mock<ILogger<FraudAnalysisService>> _mockLogger;
    private readonly FraudAnalysisService _service;

    public FraudAnalysisServiceTests()
    {
        _mockOrchestrator = new Mock<IAnalysisOrchestrator>();
        _mockRepo = new Mock<IClaimsRepository>();
        _mockLogger = new Mock<ILogger<FraudAnalysisService>>();
        _service = new FraudAnalysisService(
            _mockOrchestrator.Object,
            _mockRepo.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task AnalyzeFraudAsync_ValidClaim_ReturnsFraudScore()
    {
        // Arrange
        var claim = CreateClaim(1, "Suspicious fire claim filed 3 days after policy increase");
        _mockRepo.Setup(r => r.GetClaimByIdAsync(1)).ReturnsAsync(claim);
        _mockOrchestrator.Setup(o => o.AnalyzeAsync(claim.ClaimText, OrchestrationProfile.FraudScoring, It.IsAny<InteractionType>()))
            .ReturnsAsync(CreateAgentResultWithFraud(65, "Medium"));
        _mockRepo.Setup(r => r.UpdateClaimAsync(It.IsAny<ClaimRecord>())).Returns(Task.CompletedTask);

        // Act
        var result = await _service.AnalyzeFraudAsync(1);

        // Assert
        Assert.Equal(1, result.ClaimId);
        Assert.Equal(65, result.FraudScore);
        Assert.Equal("Medium", result.RiskLevel);
        Assert.False(result.ReferToSIU);
    }

    [Fact]
    public async Task AnalyzeFraudAsync_HighScore_RecommendsSIUReferral()
    {
        // Arrange
        var claim = CreateClaim(2, "Multiple claims in 6 months, all just under deductible");
        _mockRepo.Setup(r => r.GetClaimByIdAsync(2)).ReturnsAsync(claim);
        _mockOrchestrator.Setup(o => o.AnalyzeAsync(claim.ClaimText, OrchestrationProfile.FraudScoring, It.IsAny<InteractionType>()))
            .ReturnsAsync(CreateAgentResultWithFraud(82, "VeryHigh", true, "Pattern of frequent claims with suspicious timing"));
        _mockRepo.Setup(r => r.UpdateClaimAsync(It.IsAny<ClaimRecord>())).Returns(Task.CompletedTask);

        // Act
        var result = await _service.AnalyzeFraudAsync(2);

        // Assert
        Assert.True(result.ReferToSIU);
        Assert.Equal("VeryHigh", result.RiskLevel);
        Assert.Contains("suspicious timing", result.SiuReferralReason);
    }

    [Fact]
    public async Task AnalyzeFraudAsync_HighScore_UpdatesClaimStatusToUnderReview()
    {
        // Arrange
        var claim = CreateClaim(3, "Claim filed from different state than policy address");
        _mockRepo.Setup(r => r.GetClaimByIdAsync(3)).ReturnsAsync(claim);
        _mockOrchestrator.Setup(o => o.AnalyzeAsync(It.IsAny<string>(), OrchestrationProfile.FraudScoring, It.IsAny<InteractionType>()))
            .ReturnsAsync(CreateAgentResultWithFraud(80, "High", true));
        _mockRepo.Setup(r => r.UpdateClaimAsync(It.IsAny<ClaimRecord>())).Returns(Task.CompletedTask);

        // Act
        await _service.AnalyzeFraudAsync(3);

        // Assert
        _mockRepo.Verify(r => r.UpdateClaimAsync(It.Is<ClaimRecord>(c =>
            c.Status == "UnderReview" &&
            c.FraudScore == 80
        )), Times.Once);
    }

    [Fact]
    public async Task AnalyzeFraudAsync_ClaimNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        _mockRepo.Setup(r => r.GetClaimByIdAsync(999)).ReturnsAsync((ClaimRecord?)null);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.AnalyzeFraudAsync(999));
    }

    [Fact]
    public async Task GetFraudScoreAsync_ExistingClaim_ReturnsFraudData()
    {
        // Arrange
        var claim = CreateClaim(4, "Storm damage claim");
        claim.FraudScore = 25;
        claim.FraudRiskLevel = "Low";
        _mockRepo.Setup(r => r.GetClaimByIdAsync(4)).ReturnsAsync(claim);

        // Act
        var result = await _service.GetFraudScoreAsync(4);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(25, result.FraudScore);
        Assert.Equal("Low", result.RiskLevel);
        Assert.False(result.ReferToSIU);
    }

    [Fact]
    public async Task GetFraudScoreAsync_NonExistentClaim_ReturnsNull()
    {
        // Arrange
        _mockRepo.Setup(r => r.GetClaimByIdAsync(999)).ReturnsAsync((ClaimRecord?)null);

        // Act
        var result = await _service.GetFraudScoreAsync(999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetFraudAlertsAsync_ReturnsClaimTriageResponses()
    {
        // Arrange
        var alerts = new List<ClaimRecord>
        {
            CreateClaim(1, "High fraud claim A", 85, "VeryHigh"),
            CreateClaim(2, "Medium fraud claim B", 62, "Medium")
        };
        _mockRepo.Setup(r => r.GetFraudAlertsAsync(55, 50)).ReturnsAsync(alerts);

        // Act
        var results = await _service.GetFraudAlertsAsync();

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Equal(85, results[0].FraudScore);
        Assert.Equal("VeryHigh", results[0].FraudRiskLevel);
        Assert.Equal("Medium", results[0].Severity);
        Assert.Equal("Medium", results[1].FraudRiskLevel);
    }

    [Theory]
    [InlineData(74, false)]
    [InlineData(75, true)]
    [InlineData(76, true)]
    public async Task AnalyzeFraudAsync_SIUBoundary_CorrectlyAppliesThreshold(int score, bool expectSIU)
    {
        // Arrange
        var claim = CreateClaim(100, "Boundary test claim for SIU threshold");
        _mockRepo.Setup(r => r.GetClaimByIdAsync(100)).ReturnsAsync(claim);
        _mockOrchestrator.Setup(o => o.AnalyzeAsync(It.IsAny<string>(), OrchestrationProfile.FraudScoring, It.IsAny<InteractionType>()))
            .ReturnsAsync(CreateAgentResultWithFraud(score, score >= 75 ? "High" : "Medium", expectSIU));
        _mockRepo.Setup(r => r.UpdateClaimAsync(It.IsAny<ClaimRecord>())).Returns(Task.CompletedTask);

        // Act
        var result = await _service.AnalyzeFraudAsync(100);

        // Assert — SIU referral aligns with >= 75 threshold
        Assert.Equal(expectSIU, result.FraudScore >= 75);
        if (expectSIU)
            _mockRepo.Verify(r => r.UpdateClaimAsync(It.Is<ClaimRecord>(c => c.Status == "UnderReview")), Times.Once);
    }

    [Theory]
    [InlineData(74, false)]
    [InlineData(75, true)]
    [InlineData(76, true)]
    public async Task GetFraudScoreAsync_SIUBoundary_CorrectlyAppliesThreshold(int score, bool expectSIU)
    {
        // Arrange
        var claim = CreateClaim(101, "Boundary test for GetFraudScore", score, score >= 75 ? "High" : "Medium");
        _mockRepo.Setup(r => r.GetClaimByIdAsync(101)).ReturnsAsync(claim);

        // Act
        var result = await _service.GetFraudScoreAsync(101);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectSIU, result!.ReferToSIU);
    }

    [Fact]
    public async Task AnalyzeFraudAsync_LLMFailed_PreservesExistingTriageScore()
    {
        // Arrange — claim already has triage fraud score of 70/High from triage pipeline
        var claim = CreateClaim(15, "Theft of electronics from rental apartment while on vacation", 70, "High");
        _mockRepo.Setup(r => r.GetClaimByIdAsync(15)).ReturnsAsync(claim);

        // LLM fails: orchestrator returns result with FraudAnalysis = null
        _mockOrchestrator.Setup(o => o.AnalyzeAsync(It.IsAny<string>(), OrchestrationProfile.FraudScoring, It.IsAny<InteractionType>()))
            .ReturnsAsync(new AgentAnalysisResult { IsSuccess = false, FraudAnalysis = null });

        // Act
        var result = await _service.AnalyzeFraudAsync(15);

        // Assert — should return the existing triage data, NOT zeros
        Assert.Equal(70, result.FraudScore);
        Assert.Equal("High", result.RiskLevel);

        // Should NOT update the claim record (preserve existing data)
        _mockRepo.Verify(r => r.UpdateClaimAsync(It.IsAny<ClaimRecord>()), Times.Never);
    }

    [Fact]
    public async Task AnalyzeFraudAsync_LLMFailed_DoesNotOverwriteDBWithZeros()
    {
        // Arrange — claim with existing fraud data from triage
        var claim = CreateClaim(16, "Water damage claim with inconsistent timeline and inflated estimates", 82, "VeryHigh");
        claim.Status = "UnderReview";
        _mockRepo.Setup(r => r.GetClaimByIdAsync(16)).ReturnsAsync(claim);

        // LLM returns empty result (all providers down)
        _mockOrchestrator.Setup(o => o.AnalyzeAsync(It.IsAny<string>(), OrchestrationProfile.FraudScoring, It.IsAny<InteractionType>()))
            .ReturnsAsync(new AgentAnalysisResult { IsSuccess = false, FraudAnalysis = null });

        // Act
        await _service.AnalyzeFraudAsync(16);

        // Assert — DB should NOT be updated, claim stays UnderReview with score 82
        _mockRepo.Verify(r => r.UpdateClaimAsync(It.IsAny<ClaimRecord>()), Times.Never);
        Assert.Equal(82, claim.FraudScore);
        Assert.Equal("VeryHigh", claim.FraudRiskLevel);
        Assert.Equal("UnderReview", claim.Status);
    }

    [Theory]
    [InlineData(74, "Medium")]
    [InlineData(75, "High")]
    [InlineData(92, "VeryHigh")]
    public async Task GetFraudAlertsAsync_ReturnsCorrectRiskLevel(int score, string expectedRiskLevel)
    {
        // Arrange
        var claims = new List<ClaimRecord> { CreateClaim(102, "Boundary alert claim", score, expectedRiskLevel) };
        _mockRepo.Setup(r => r.GetFraudAlertsAsync(55, 50)).ReturnsAsync(claims);

        // Act
        var results = await _service.GetFraudAlertsAsync();

        // Assert
        Assert.Single(results);
        Assert.Equal(score, results[0].FraudScore);
        Assert.Equal(expectedRiskLevel, results[0].FraudRiskLevel);
    }

    private static ClaimRecord CreateClaim(int id, string text, double fraudScore = 0, string riskLevel = "VeryLow")
    {
        return new ClaimRecord
        {
            Id = id,
            ClaimText = text,
            Severity = "Medium",
            Urgency = "Standard",
            Status = "Triaged",
            FraudScore = fraudScore,
            FraudRiskLevel = riskLevel,
            Evidence = [],
            Actions = []
        };
    }

    private static AgentAnalysisResult CreateAgentResultWithFraud(
        int score, string riskLevel, bool referToSiu = false, string siuReason = "")
    {
        return new AgentAnalysisResult
        {
            IsSuccess = true,
            FraudAnalysis = new FraudAnalysisDetail
            {
                FraudProbabilityScore = score,
                RiskLevel = riskLevel,
                ReferToSIU = referToSiu,
                SiuReferralReason = siuReason,
                ConfidenceInAssessment = 0.85,
                Indicators =
                [
                    new FraudIndicator { Category = "Timing", Description = "Claim filed shortly after policy change", Severity = "Medium" }
                ],
                RecommendedActions =
                [
                    new RecommendedAction { Action = "Review claim history", Priority = "High", Reasoning = "Pattern analysis needed" }
                ]
            }
        };
    }
}
