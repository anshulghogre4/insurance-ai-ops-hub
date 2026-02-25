using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using SentimentAnalyzer.Agents.Configuration;
using SentimentAnalyzer.Agents.Models;
using SentimentAnalyzer.Agents.Orchestration;
using SentimentAnalyzer.API.Data;
using SentimentAnalyzer.API.Data.Entities;
using SentimentAnalyzer.API.Services.Claims;
using SentimentAnalyzer.Domain.Enums;
using Xunit;

namespace SentimentAnalyzer.Tests;

/// <summary>
/// Tests for ClaimsOrchestrationService — the claims triage facade.
/// </summary>
public class ClaimsOrchestrationServiceTests
{
    private readonly Mock<IAnalysisOrchestrator> _mockOrchestrator;
    private readonly Mock<IClaimsRepository> _mockRepo;
    private readonly Mock<IPIIRedactor> _mockPiiRedactor;
    private readonly Mock<ILogger<ClaimsOrchestrationService>> _mockLogger;
    private readonly ClaimsOrchestrationService _service;

    public ClaimsOrchestrationServiceTests()
    {
        _mockOrchestrator = new Mock<IAnalysisOrchestrator>();
        _mockRepo = new Mock<IClaimsRepository>();
        _mockPiiRedactor = new Mock<IPIIRedactor>();
        _mockPiiRedactor.Setup(r => r.Redact(It.IsAny<string>())).Returns((string s) => s); // passthrough
        _mockLogger = new Mock<ILogger<ClaimsOrchestrationService>>();
        _service = new ClaimsOrchestrationService(
            _mockOrchestrator.Object,
            _mockRepo.Object,
            _mockPiiRedactor.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task TriageClaimAsync_WithValidClaim_ReturnsTriagedResponse()
    {
        // Arrange
        var claimText = "Water damage reported Jan 15, 2024. Basement flooding after pipe burst. Policy HO-2024-789456. Estimated damage: $15,000.";
        var agentResult = CreateAgentResultWithTriage();

        _mockOrchestrator.Setup(o => o.AnalyzeAsync(claimText, OrchestrationProfile.ClaimsTriage, It.IsAny<InteractionType>()))
            .ReturnsAsync(agentResult);
        _mockRepo.Setup(r => r.SaveClaimAsync(It.IsAny<ClaimRecord>()))
            .ReturnsAsync((ClaimRecord c) => { c.Id = 1; return c; });
        _mockRepo.Setup(r => r.SaveActionsAsync(It.IsAny<IEnumerable<ClaimActionRecord>>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.TriageClaimAsync(claimText);

        // Assert
        Assert.Equal(1, result.ClaimId);
        Assert.Equal("High", result.Severity);
        Assert.Equal("Urgent", result.Urgency);
        Assert.Equal("Property", result.ClaimType);
        Assert.Equal("Triaged", result.Status);
    }

    [Fact]
    public async Task TriageClaimAsync_CallsOrchestratorWithClaimsTriageProfile()
    {
        // Arrange
        var claimText = "Auto collision on Highway 101. Driver reports whiplash injury.";
        _mockOrchestrator.Setup(o => o.AnalyzeAsync(claimText, OrchestrationProfile.ClaimsTriage, InteractionType.Complaint))
            .ReturnsAsync(CreateAgentResultWithTriage());
        _mockRepo.Setup(r => r.SaveClaimAsync(It.IsAny<ClaimRecord>()))
            .ReturnsAsync((ClaimRecord c) => { c.Id = 2; return c; });

        // Act
        await _service.TriageClaimAsync(claimText, InteractionType.Complaint);

        // Assert
        _mockOrchestrator.Verify(o => o.AnalyzeAsync(claimText, OrchestrationProfile.ClaimsTriage, InteractionType.Complaint), Times.Once);
    }

    [Fact]
    public async Task TriageClaimAsync_PersistsClaimToDatabase()
    {
        // Arrange
        var claimText = "Hail damage to roof, multiple shingles missing. Policy HO-2024-111222.";
        _mockOrchestrator.Setup(o => o.AnalyzeAsync(It.IsAny<string>(), OrchestrationProfile.ClaimsTriage, It.IsAny<InteractionType>()))
            .ReturnsAsync(CreateAgentResultWithTriage());
        _mockRepo.Setup(r => r.SaveClaimAsync(It.IsAny<ClaimRecord>()))
            .ReturnsAsync((ClaimRecord c) => { c.Id = 3; return c; });

        // Act
        await _service.TriageClaimAsync(claimText);

        // Assert
        _mockRepo.Verify(r => r.SaveClaimAsync(It.Is<ClaimRecord>(c =>
            c.ClaimText == claimText &&
            c.Severity == "High" &&
            c.Status == "Triaged"
        )), Times.Once);
    }

    [Fact]
    public async Task TriageClaimAsync_SavesRecommendedActions()
    {
        // Arrange
        var claimText = "Fire damage to kitchen. Estimated $50,000 in repairs.";
        _mockOrchestrator.Setup(o => o.AnalyzeAsync(It.IsAny<string>(), OrchestrationProfile.ClaimsTriage, It.IsAny<InteractionType>()))
            .ReturnsAsync(CreateAgentResultWithTriage());
        _mockRepo.Setup(r => r.SaveClaimAsync(It.IsAny<ClaimRecord>()))
            .ReturnsAsync((ClaimRecord c) => { c.Id = 4; return c; });

        // Act
        await _service.TriageClaimAsync(claimText);

        // Assert
        _mockRepo.Verify(r => r.SaveActionsAsync(It.Is<IEnumerable<ClaimActionRecord>>(
            actions => actions.Any(a => a.Action == "Assign field adjuster")
        )), Times.Once);
    }

    [Fact]
    public async Task TriageClaimAsync_WithNullTriageResult_ReturnsDefaults()
    {
        // Arrange
        var claimText = "Minor fender bender in parking lot.";
        var agentResult = new AgentAnalysisResult { IsSuccess = true, Sentiment = "Neutral" };
        _mockOrchestrator.Setup(o => o.AnalyzeAsync(It.IsAny<string>(), OrchestrationProfile.ClaimsTriage, It.IsAny<InteractionType>()))
            .ReturnsAsync(agentResult);
        _mockRepo.Setup(r => r.SaveClaimAsync(It.IsAny<ClaimRecord>()))
            .ReturnsAsync((ClaimRecord c) => { c.Id = 5; return c; });

        // Act
        var result = await _service.TriageClaimAsync(claimText);

        // Assert
        Assert.Equal("Medium", result.Severity);
        Assert.Equal("Standard", result.Urgency);
        Assert.Equal("VeryLow", result.FraudRiskLevel);
    }

    [Fact]
    public async Task TriageClaimAsync_TruncatesLongClaimText()
    {
        // Arrange
        var longText = new string('A', 6000); // Over 5000 limit
        _mockOrchestrator.Setup(o => o.AnalyzeAsync(It.IsAny<string>(), OrchestrationProfile.ClaimsTriage, It.IsAny<InteractionType>()))
            .ReturnsAsync(CreateAgentResultWithTriage());
        _mockRepo.Setup(r => r.SaveClaimAsync(It.IsAny<ClaimRecord>()))
            .ReturnsAsync((ClaimRecord c) => { c.Id = 6; return c; });

        // Act
        await _service.TriageClaimAsync(longText);

        // Assert
        _mockRepo.Verify(r => r.SaveClaimAsync(It.Is<ClaimRecord>(c =>
            c.ClaimText.Length == 5000
        )), Times.Once);
    }

    [Fact]
    public async Task TriageClaimAsync_RedactsPIIBeforeStoringToDatabase()
    {
        // Arrange
        var claimText = "Policy HO-2024-789456. SSN 123-45-6789. Contact at john@example.com.";
        var redactedText = "Policy [POLICY-REDACTED]. SSN [SSN-REDACTED]. Contact at [EMAIL-REDACTED].";
        _mockPiiRedactor.Setup(r => r.Redact(claimText)).Returns(redactedText);
        _mockOrchestrator.Setup(o => o.AnalyzeAsync(It.IsAny<string>(), OrchestrationProfile.ClaimsTriage, It.IsAny<InteractionType>()))
            .ReturnsAsync(CreateAgentResultWithTriage());
        _mockRepo.Setup(r => r.SaveClaimAsync(It.IsAny<ClaimRecord>()))
            .ReturnsAsync((ClaimRecord c) => { c.Id = 7; return c; });

        // Act
        await _service.TriageClaimAsync(claimText);

        // Assert — DB receives PII-redacted text, not raw
        _mockRepo.Verify(r => r.SaveClaimAsync(It.Is<ClaimRecord>(c =>
            c.ClaimText == redactedText
        )), Times.Once);
        _mockPiiRedactor.Verify(r => r.Redact(claimText), Times.Once);
    }

    [Fact]
    public async Task GetClaimAsync_ExistingClaim_ReturnsResponse()
    {
        // Arrange
        _mockRepo.Setup(r => r.GetClaimByIdAsync(1))
            .ReturnsAsync(new ClaimRecord
            {
                Id = 1,
                ClaimText = "Water damage claim",
                Severity = "High",
                Urgency = "Urgent",
                Status = "Triaged",
                Evidence = [],
                Actions = []
            });

        // Act
        var result = await _service.GetClaimAsync(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.ClaimId);
        Assert.Equal("High", result.Severity);
    }

    [Fact]
    public async Task GetClaimAsync_NonExistentClaim_ReturnsNull()
    {
        // Arrange
        _mockRepo.Setup(r => r.GetClaimByIdAsync(999)).ReturnsAsync((ClaimRecord?)null);

        // Act
        var result = await _service.GetClaimAsync(999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task TriageClaimAsync_TruncatesFraudFlagsJsonToMaxLength_ValidJson()
    {
        // Arrange — generate fraud flags that exceed 2000-char limit when serialized
        var longFlags = Enumerable.Range(1, 100)
            .Select(i => $"Suspicious indicator #{i}: extremely detailed fraud flag description text that makes the JSON very long")
            .ToList();

        var agentResult = new AgentAnalysisResult
        {
            IsSuccess = true,
            ClaimTriage = new ClaimTriageDetail
            {
                Severity = "High",
                Urgency = "Urgent",
                ClaimType = "Property",
                FraudFlags = longFlags,
                RecommendedActions = []
            }
        };

        _mockOrchestrator.Setup(o => o.AnalyzeAsync(It.IsAny<string>(), OrchestrationProfile.ClaimsTriage, It.IsAny<InteractionType>()))
            .ReturnsAsync(agentResult);

        ClaimRecord? savedClaim = null;
        _mockRepo.Setup(r => r.SaveClaimAsync(It.IsAny<ClaimRecord>()))
            .Callback<ClaimRecord>(c => savedClaim = c)
            .ReturnsAsync((ClaimRecord c) => { c.Id = 10; return c; });

        // Act
        await _service.TriageClaimAsync("Fire damage claim with many fraud indicators");

        // Assert — FraudFlagsJson is <= 2000 chars AND is valid JSON (data-level truncation)
        Assert.NotNull(savedClaim);
        Assert.True(savedClaim!.FraudFlagsJson.Length <= 2000,
            $"FraudFlagsJson should be <= 2000 chars but was {savedClaim.FraudFlagsJson.Length}");
        // Verify it's valid parseable JSON (not a chopped string)
        var parsed = System.Text.Json.JsonSerializer.Deserialize<List<string>>(savedClaim.FraudFlagsJson);
        Assert.NotNull(parsed);
        Assert.True(parsed!.Count > 0, "Should retain some flags after truncation");
        Assert.True(parsed.Count < 100, "Should have fewer flags than original 100");
    }

    [Fact]
    public async Task TriageClaimAsync_TruncatesActionFieldsToMaxLength()
    {
        // Arrange — agent returns oversized action text
        var longAction = new string('A', 600); // Exceeds MaxLength(500)
        var longReasoning = new string('R', 1200); // Exceeds MaxLength(1000)

        var agentResult = new AgentAnalysisResult
        {
            IsSuccess = true,
            ClaimTriage = new ClaimTriageDetail
            {
                Severity = "Medium",
                Urgency = "Standard",
                ClaimType = "Auto",
                RecommendedActions =
                [
                    new RecommendedAction { Action = longAction, Priority = "High", Reasoning = longReasoning }
                ],
                FraudFlags = []
            }
        };

        _mockOrchestrator.Setup(o => o.AnalyzeAsync(It.IsAny<string>(), OrchestrationProfile.ClaimsTriage, It.IsAny<InteractionType>()))
            .ReturnsAsync(agentResult);
        _mockRepo.Setup(r => r.SaveClaimAsync(It.IsAny<ClaimRecord>()))
            .ReturnsAsync((ClaimRecord c) => { c.Id = 11; return c; });

        List<ClaimActionRecord>? savedActions = null;
        _mockRepo.Setup(r => r.SaveActionsAsync(It.IsAny<IEnumerable<ClaimActionRecord>>()))
            .Callback<IEnumerable<ClaimActionRecord>>(a => savedActions = a.ToList())
            .Returns(Task.CompletedTask);

        // Act
        await _service.TriageClaimAsync("Auto collision requiring detailed action plan");

        // Assert — action fields truncated to MaxLength
        Assert.NotNull(savedActions);
        Assert.Single(savedActions!);
        Assert.True(savedActions[0].Action.Length <= 500,
            $"Action should be <= 500 chars but was {savedActions[0].Action.Length}");
        Assert.True(savedActions[0].Reasoning.Length <= 1000,
            $"Reasoning should be <= 1000 chars but was {savedActions[0].Reasoning.Length}");
    }

    private static AgentAnalysisResult CreateAgentResultWithTriage()
    {
        return new AgentAnalysisResult
        {
            IsSuccess = true,
            Sentiment = "Negative",
            ConfidenceScore = 0.88,
            Explanation = "Policyholder reports significant property damage",
            ClaimTriage = new ClaimTriageDetail
            {
                Severity = "High",
                Urgency = "Urgent",
                ClaimType = "Property",
                EstimatedLossRange = "$10,000-$25,000",
                PreliminaryFraudRisk = "Low",
                RecommendedActions =
                [
                    new RecommendedAction { Action = "Assign field adjuster", Priority = "High", Reasoning = "Large loss claim" }
                ],
                FraudFlags = []
            }
        };
    }
}
