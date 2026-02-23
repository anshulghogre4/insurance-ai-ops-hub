using Microsoft.Extensions.Logging;
using Moq;
using SentimentAnalyzer.Agents.Orchestration;
using SentimentAnalyzer.API.Data;
using SentimentAnalyzer.API.Data.Entities;
using SentimentAnalyzer.API.Features.Insurance.Commands;
using SentimentAnalyzer.API.Features.Insurance.Queries;
using SentimentAnalyzer.API.Models;
using SentimentAnalyzer.Domain.Enums;
using Xunit;
using AgentModels = SentimentAnalyzer.Agents.Models;

namespace SentimentAnalyzer.Tests;

/// <summary>
/// Tests for CQRS handlers (MediatR) that replaced the InsuranceAnalysisController.
/// </summary>
public class AnalyzeInsuranceHandlerTests
{
    private readonly Mock<IAnalysisOrchestrator> _mockOrchestrator;
    private readonly Mock<IAnalysisRepository> _mockRepository;
    private readonly Mock<ILogger<AnalyzeInsuranceHandler>> _mockLogger;
    private readonly AnalyzeInsuranceHandler _handler;

    public AnalyzeInsuranceHandlerTests()
    {
        _mockOrchestrator = new Mock<IAnalysisOrchestrator>();
        _mockRepository = new Mock<IAnalysisRepository>();
        _mockLogger = new Mock<ILogger<AnalyzeInsuranceHandler>>();
        _handler = new AnalyzeInsuranceHandler(
            _mockOrchestrator.Object,
            _mockRepository.Object,
            _mockLogger.Object);
    }

    private static AgentModels.AgentAnalysisResult CreateAgentResult(
        string sentiment = "Positive",
        int purchaseIntent = 75,
        string persona = "CoverageFocused")
    {
        return new AgentModels.AgentAnalysisResult
        {
            IsSuccess = true,
            Sentiment = sentiment,
            ConfidenceScore = 0.85,
            Explanation = "Customer shows strong interest in insurance coverage",
            EmotionBreakdown = new Dictionary<string, double>
            {
                { "trust", 0.7 },
                { "satisfaction", 0.6 }
            },
            InsuranceAnalysis = new AgentModels.InsuranceAnalysisDetail
            {
                PurchaseIntentScore = purchaseIntent,
                CustomerPersona = persona,
                JourneyStage = "Decision",
                RiskIndicators = new AgentModels.RiskIndicatorDetail
                {
                    ChurnRisk = "Low",
                    ComplaintEscalationRisk = "Low",
                    FraudIndicators = "None"
                },
                PolicyRecommendations =
                [
                    new AgentModels.PolicyRecommendationDetail
                    {
                        Product = "Health Gold Plan",
                        Reasoning = "Customer is actively comparing health insurance options"
                    }
                ],
                InteractionType = "Email",
                KeyTopics = ["coverage comparison", "family plan", "premium rates"]
            },
            Quality = new AgentModels.QualityMetadata
            {
                IsValid = true,
                QualityScore = 92,
                Suggestions = []
            }
        };
    }

    [Fact]
    public async Task Handle_WithValidCommand_ReturnsInsuranceAnalysisResponse()
    {
        // Arrange
        var command = new AnalyzeInsuranceCommand(
            "I've been comparing health insurance plans and your premium rates are very competitive.",
            "Email");

        _mockOrchestrator.Setup(o => o.AnalyzeAsync(It.IsAny<string>(), It.IsAny<InteractionType>()))
            .ReturnsAsync(CreateAgentResult());

        // Act
        var response = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal("Positive", response.Sentiment);
        Assert.Equal(75, response.InsuranceAnalysis.PurchaseIntentScore);
        Assert.Equal("CoverageFocused", response.InsuranceAnalysis.CustomerPersona);
    }

    [Fact]
    public async Task Handle_PersistsToRepository()
    {
        // Arrange
        var command = new AnalyzeInsuranceCommand("Test text", "General");

        _mockOrchestrator.Setup(o => o.AnalyzeAsync(It.IsAny<string>(), It.IsAny<InteractionType>()))
            .ReturnsAsync(CreateAgentResult());

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockRepository.Verify(r => r.SaveAsync(It.IsAny<AnalysisRecord>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenRepositoryFails_StillReturnsResponse()
    {
        // Arrange
        var command = new AnalyzeInsuranceCommand("Test text", "General");

        _mockOrchestrator.Setup(o => o.AnalyzeAsync(It.IsAny<string>(), It.IsAny<InteractionType>()))
            .ReturnsAsync(CreateAgentResult());
        _mockRepository.Setup(r => r.SaveAsync(It.IsAny<AnalysisRecord>()))
            .ThrowsAsync(new Exception("DB error"));

        // Act
        var response = await _handler.Handle(command, CancellationToken.None);

        // Assert - should still return a valid response
        Assert.Equal("Positive", response.Sentiment);
    }

    [Fact]
    public async Task Handle_WhenOrchestratorThrows_PropagatesException()
    {
        // Arrange
        var command = new AnalyzeInsuranceCommand("Test text", "General");

        _mockOrchestrator.Setup(o => o.AnalyzeAsync(It.IsAny<string>(), It.IsAny<InteractionType>()))
            .ThrowsAsync(new Exception("AI provider unavailable"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ResponseContainsInsuranceAnalysisFields()
    {
        // Arrange
        var command = new AnalyzeInsuranceCommand(
            "I submitted my auto claim three weeks ago and nobody has gotten back to me.",
            "Complaint");

        var agentResult = CreateAgentResult("Negative", 10, "ClaimFrustrated");
        agentResult.InsuranceAnalysis.RiskIndicators.ChurnRisk = "High";
        agentResult.InsuranceAnalysis.RiskIndicators.ComplaintEscalationRisk = "High";

        _mockOrchestrator.Setup(o => o.AnalyzeAsync(It.IsAny<string>(), It.IsAny<InteractionType>()))
            .ReturnsAsync(agentResult);

        // Act
        var response = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(response.InsuranceAnalysis);
        Assert.NotNull(response.InsuranceAnalysis.RiskIndicators);
        Assert.NotNull(response.InsuranceAnalysis.PolicyRecommendations);
        Assert.NotNull(response.InsuranceAnalysis.KeyTopics);
        Assert.Equal("High", response.InsuranceAnalysis.RiskIndicators.ChurnRisk);
        Assert.Equal("ClaimFrustrated", response.InsuranceAnalysis.CustomerPersona);
    }

    [Fact]
    public async Task Handle_ResponseContainsQualityMetadata()
    {
        // Arrange
        var command = new AnalyzeInsuranceCommand("I need renters insurance");

        _mockOrchestrator.Setup(o => o.AnalyzeAsync(It.IsAny<string>(), It.IsAny<InteractionType>()))
            .ReturnsAsync(CreateAgentResult());

        // Act
        var response = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(response.Quality);
        Assert.True(response.Quality.IsValid);
        Assert.True(response.Quality.QualityScore > 0);
    }

    [Theory]
    [InlineData("I want to buy auto insurance for my new car")]
    [InlineData("My claim was denied and I am very frustrated")]
    [InlineData("Can you explain the difference between HO-3 and HO-5 policies?")]
    public async Task Handle_WithVariousInsuranceTexts_ReturnsResponse(string text)
    {
        // Arrange
        var command = new AnalyzeInsuranceCommand(text);

        _mockOrchestrator.Setup(o => o.AnalyzeAsync(It.IsAny<string>(), It.IsAny<InteractionType>()))
            .ReturnsAsync(CreateAgentResult());

        // Act
        var response = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(response);
        Assert.NotEmpty(response.Sentiment);
    }

    [Theory]
    [InlineData("General")]
    [InlineData("Email")]
    [InlineData("Call")]
    [InlineData("Chat")]
    [InlineData("Review")]
    [InlineData("Complaint")]
    public async Task Handle_ParsesValidInteractionTypes(string interactionType)
    {
        // Arrange
        var command = new AnalyzeInsuranceCommand("I need insurance", interactionType);

        _mockOrchestrator.Setup(o => o.AnalyzeAsync(It.IsAny<string>(), It.IsAny<InteractionType>()))
            .ReturnsAsync(CreateAgentResult());

        // Act
        var response = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(response);
    }

    [Fact]
    public async Task Handle_WithInvalidInteractionType_FallsBackToGeneral()
    {
        // Arrange
        var command = new AnalyzeInsuranceCommand("Test text", "InvalidType");

        _mockOrchestrator.Setup(o => o.AnalyzeAsync(It.IsAny<string>(), InteractionType.General))
            .ReturnsAsync(CreateAgentResult());

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockOrchestrator.Verify(o => o.AnalyzeAsync(It.IsAny<string>(), InteractionType.General), Times.Once);
    }

    [Fact]
    public async Task Handle_QualityWithIssuesAndSuggestions_MapsAllFields()
    {
        // Arrange
        var agentResult = CreateAgentResult();
        agentResult.Quality = new AgentModels.QualityMetadata
        {
            IsValid = true,
            QualityScore = 85,
            Issues =
            [
                new AgentModels.QualityIssue { Severity = "warning", Field = "sentiment", Message = "Confidence below threshold" },
                new AgentModels.QualityIssue { Severity = "error", Field = "purchaseIntent", Message = "Score out of expected range" }
            ],
            Suggestions = ["Consider adding more context for better analysis"]
        };

        _mockOrchestrator.Setup(o => o.AnalyzeAsync(It.IsAny<string>(), It.IsAny<InteractionType>()))
            .ReturnsAsync(agentResult);

        var command = new AnalyzeInsuranceCommand(
            "My home policy premium increased significantly after my water damage claim last year.");

        // Act
        var response = await _handler.Handle(command, CancellationToken.None);

        // Assert - structured fields
        Assert.True(response.Quality.IsValid);
        Assert.Equal(85, response.Quality.QualityScore);
        Assert.Equal(2, response.Quality.Issues.Count);
        Assert.Equal("warning", response.Quality.Issues[0].Severity);
        Assert.Equal("sentiment", response.Quality.Issues[0].Field);
        Assert.Equal("Confidence below threshold", response.Quality.Issues[0].Message);
        Assert.Single(response.Quality.Suggestions);

        // Assert - backward-compatible warnings (issues + suggestions flattened)
        Assert.Equal(3, response.Quality.Warnings.Count);
        Assert.Contains("[warning] sentiment: Confidence below threshold", response.Quality.Warnings);
        Assert.Contains("[error] purchaseIntent: Score out of expected range", response.Quality.Warnings);
        Assert.Contains("Consider adding more context for better analysis", response.Quality.Warnings);
    }

    [Fact]
    public async Task Handle_QualityWithOnlyIssues_WarningsContainOnlyIssues()
    {
        // Arrange
        var agentResult = CreateAgentResult();
        agentResult.Quality = new AgentModels.QualityMetadata
        {
            IsValid = false,
            QualityScore = 40,
            Issues =
            [
                new AgentModels.QualityIssue { Severity = "error", Field = "emotionBreakdown", Message = "Missing required emotions" }
            ],
            Suggestions = []
        };

        _mockOrchestrator.Setup(o => o.AnalyzeAsync(It.IsAny<string>(), It.IsAny<InteractionType>()))
            .ReturnsAsync(agentResult);

        var command = new AnalyzeInsuranceCommand(
            "I want to file a complaint about my denied flood insurance claim.");

        // Act
        var response = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(response.Quality.IsValid);
        Assert.Equal(40, response.Quality.QualityScore);
        Assert.Single(response.Quality.Issues);
        Assert.Empty(response.Quality.Suggestions);
        Assert.Single(response.Quality.Warnings);
        Assert.Contains("[error] emotionBreakdown:", response.Quality.Warnings[0]);
    }

    [Fact]
    public async Task Handle_QualityWithOnlySuggestions_WarningsContainOnlySuggestions()
    {
        // Arrange
        var agentResult = CreateAgentResult();
        agentResult.Quality = new AgentModels.QualityMetadata
        {
            IsValid = true,
            QualityScore = 95,
            Issues = [],
            Suggestions = ["Add customer ID for personalized recommendations", "Include policy number for faster lookup"]
        };

        _mockOrchestrator.Setup(o => o.AnalyzeAsync(It.IsAny<string>(), It.IsAny<InteractionType>()))
            .ReturnsAsync(agentResult);

        var command = new AnalyzeInsuranceCommand(
            "I am very happy with the quick settlement of my auto glass repair claim.");

        // Act
        var response = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(response.Quality.IsValid);
        Assert.Equal(95, response.Quality.QualityScore);
        Assert.Empty(response.Quality.Issues);
        Assert.Equal(2, response.Quality.Suggestions.Count);
        Assert.Equal(2, response.Quality.Warnings.Count);
        Assert.Equal("Add customer ID for personalized recommendations", response.Quality.Warnings[0]);
    }

    [Fact]
    public async Task Handle_NullQuality_FallsBackToIsSuccessFlag()
    {
        // Arrange
        var agentResult = CreateAgentResult();
        agentResult.Quality = null;
        agentResult.IsSuccess = true;

        _mockOrchestrator.Setup(o => o.AnalyzeAsync(It.IsAny<string>(), It.IsAny<InteractionType>()))
            .ReturnsAsync(agentResult);

        var command = new AnalyzeInsuranceCommand(
            "Can you walk me through what my renters insurance policy covers?");

        // Act
        var response = await _handler.Handle(command, CancellationToken.None);

        // Assert - should use fallback values
        Assert.True(response.Quality.IsValid);
        Assert.Equal(80, response.Quality.QualityScore); // success fallback = 80
        Assert.Empty(response.Quality.Issues);
        Assert.Empty(response.Quality.Suggestions);
        Assert.Empty(response.Quality.Warnings);
    }

    [Fact]
    public async Task Handle_NullQuality_WhenFailed_FallsBackToZeroScore()
    {
        // Arrange
        var agentResult = CreateAgentResult();
        agentResult.Quality = null;
        agentResult.IsSuccess = false;

        _mockOrchestrator.Setup(o => o.AnalyzeAsync(It.IsAny<string>(), It.IsAny<InteractionType>()))
            .ReturnsAsync(agentResult);

        var command = new AnalyzeInsuranceCommand(
            "My policy was cancelled without any notice from the underwriting department.");

        // Act
        var response = await _handler.Handle(command, CancellationToken.None);

        // Assert - should use failure fallback values
        Assert.False(response.Quality.IsValid);
        Assert.Equal(0, response.Quality.QualityScore); // failure fallback = 0
    }

    [Fact]
    public async Task Handle_EmptyQuality_ReturnsCleanDefaults()
    {
        // Arrange
        var agentResult = CreateAgentResult();
        agentResult.Quality = new AgentModels.QualityMetadata
        {
            IsValid = true,
            QualityScore = 100,
            Issues = [],
            Suggestions = []
        };

        _mockOrchestrator.Setup(o => o.AnalyzeAsync(It.IsAny<string>(), It.IsAny<InteractionType>()))
            .ReturnsAsync(agentResult);

        var command = new AnalyzeInsuranceCommand(
            "I would like to add my teenage driver to my auto insurance policy.");

        // Act
        var response = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(response.Quality.IsValid);
        Assert.Equal(100, response.Quality.QualityScore);
        Assert.Empty(response.Quality.Issues);
        Assert.Empty(response.Quality.Suggestions);
        Assert.Empty(response.Quality.Warnings);
    }

    [Fact]
    public async Task Handle_QualityIssuesNullInAgent_DefaultsToEmptyList()
    {
        // Arrange
        var agentResult = CreateAgentResult();
        agentResult.Quality = new AgentModels.QualityMetadata
        {
            IsValid = true,
            QualityScore = 70,
            Issues = null!,
            Suggestions = null!
        };

        _mockOrchestrator.Setup(o => o.AnalyzeAsync(It.IsAny<string>(), It.IsAny<InteractionType>()))
            .ReturnsAsync(agentResult);

        var command = new AnalyzeInsuranceCommand(
            "I need to update my beneficiary information on my life insurance policy.");

        // Act
        var response = await _handler.Handle(command, CancellationToken.None);

        // Assert - should gracefully handle null lists
        Assert.NotNull(response.Quality);
        Assert.Empty(response.Quality.Issues);
        Assert.Empty(response.Quality.Warnings);
    }
}

public class GetDashboardHandlerTests
{
    private readonly Mock<IAnalysisRepository> _mockRepository;
    private readonly GetDashboardHandler _handler;

    public GetDashboardHandlerTests()
    {
        _mockRepository = new Mock<IAnalysisRepository>();
        _handler = new GetDashboardHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task Handle_ReturnsDashboardData()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetMetricsAsync())
            .ReturnsAsync(new DashboardMetrics { TotalAnalyses = 10, AvgPurchaseIntent = 65 });
        _mockRepository.Setup(r => r.GetSentimentDistributionAsync())
            .ReturnsAsync(new SentimentDistribution { Positive = 50, Negative = 20, Neutral = 20, Mixed = 10 });
        _mockRepository.Setup(r => r.GetTopPersonasAsync())
            .ReturnsAsync([new PersonaCount { Name = "CoverageFocused", Count = 5, Percentage = 50 }]);

        // Act
        var result = await _handler.Handle(new GetDashboardQuery(), CancellationToken.None);

        // Assert
        Assert.Equal(10, result.Metrics.TotalAnalyses);
        Assert.Equal(50, result.SentimentDistribution.Positive);
        Assert.Single(result.TopPersonas);
    }
}

public class GetHistoryHandlerTests
{
    private readonly Mock<IAnalysisRepository> _mockRepository;
    private readonly GetHistoryHandler _handler;

    public GetHistoryHandlerTests()
    {
        _mockRepository = new Mock<IAnalysisRepository>();
        _handler = new GetHistoryHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task Handle_ReturnsHistoryItems()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetRecentAsync(20))
            .ReturnsAsync([
                new AnalysisRecord
                {
                    Id = 1,
                    InputText = "Test insurance text",
                    Sentiment = "Positive",
                    PurchaseIntentScore = 75,
                    CustomerPersona = "CoverageFocused",
                    InteractionType = "Email",
                    ChurnRisk = "Low",
                    CreatedAt = DateTime.UtcNow
                }
            ]);

        // Act
        var result = await _handler.Handle(new GetHistoryQuery(), CancellationToken.None);

        // Assert
        Assert.Single(result);
        Assert.Equal("Positive", result[0].Sentiment);
        Assert.Equal(75, result[0].PurchaseIntentScore);
    }

    [Fact]
    public async Task Handle_ClampsCountTo1_100()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetRecentAsync(It.IsAny<int>()))
            .ReturnsAsync([]);

        // Act - requesting 0 should clamp to 1
        await _handler.Handle(new GetHistoryQuery(0), CancellationToken.None);

        // Assert
        _mockRepository.Verify(r => r.GetRecentAsync(1), Times.Once);
    }

    [Fact]
    public async Task Handle_TruncatesLongInputText()
    {
        // Arrange
        var longText = new string('a', 200);
        _mockRepository.Setup(r => r.GetRecentAsync(20))
            .ReturnsAsync([
                new AnalysisRecord
                {
                    Id = 1,
                    InputText = longText,
                    Sentiment = "Positive",
                    PurchaseIntentScore = 50,
                    CustomerPersona = "NewBuyer",
                    InteractionType = "General",
                    ChurnRisk = "Low",
                    CreatedAt = DateTime.UtcNow
                }
            ]);

        // Act
        var result = await _handler.Handle(new GetHistoryQuery(), CancellationToken.None);

        // Assert - should be truncated to 100 + "..."
        Assert.Equal(103, result[0].InputTextPreview.Length);
        Assert.EndsWith("...", result[0].InputTextPreview);
    }
}

public class GetAnalysisByIdHandlerTests
{
    private readonly Mock<IAnalysisRepository> _mockRepository;
    private readonly GetAnalysisByIdHandler _handler;

    public GetAnalysisByIdHandlerTests()
    {
        _mockRepository = new Mock<IAnalysisRepository>();
        _handler = new GetAnalysisByIdHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task Handle_WithExistingId_ReturnsFullAnalysisResponse()
    {
        // Arrange
        var record = new AnalysisRecord
        {
            Id = 42,
            InputText = "I reported water damage on Jan 15. It's been 3 weeks with no response.",
            InteractionType = "Complaint",
            Sentiment = "Negative",
            ConfidenceScore = 0.92,
            Explanation = "Customer is frustrated with claim processing delay.",
            PurchaseIntentScore = 15,
            CustomerPersona = "ClaimFrustrated",
            JourneyStage = "ActiveClaim",
            ChurnRisk = "High",
            ComplaintEscalationRisk = "High",
            FraudIndicators = "None",
            KeyTopics = "claim delay,water damage,switching providers",
            PolicyRecommendationsJson = "[{\"Product\":\"Priority Claims Service\",\"Reasoning\":\"Customer needs faster response\"}]",
            EmotionBreakdownJson = "{\"frustration\":0.85,\"anger\":0.70,\"anxiety\":0.45}",
            IsValid = true,
            QualityScore = 88,
            CreatedAt = DateTime.UtcNow
        };

        _mockRepository.Setup(r => r.GetByIdAsync(42)).ReturnsAsync(record);

        // Act
        var result = await _handler.Handle(new GetAnalysisByIdQuery(42), CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Negative", result.Sentiment);
        Assert.Equal(0.92, result.ConfidenceScore);
        Assert.Equal("Customer is frustrated with claim processing delay.", result.Explanation);
        Assert.Equal(15, result.InsuranceAnalysis.PurchaseIntentScore);
        Assert.Equal("ClaimFrustrated", result.InsuranceAnalysis.CustomerPersona);
        Assert.Equal("ActiveClaim", result.InsuranceAnalysis.JourneyStage);
        Assert.Equal("High", result.InsuranceAnalysis.RiskIndicators.ChurnRisk);
        Assert.Equal("High", result.InsuranceAnalysis.RiskIndicators.ComplaintEscalationRisk);
        Assert.Equal("None", result.InsuranceAnalysis.RiskIndicators.FraudIndicators);
        Assert.Equal("Complaint", result.InsuranceAnalysis.InteractionType);
        Assert.Equal(3, result.InsuranceAnalysis.KeyTopics.Count);
        Assert.Contains("claim delay", result.InsuranceAnalysis.KeyTopics);
        Assert.Single(result.InsuranceAnalysis.PolicyRecommendations);
        Assert.Equal("Priority Claims Service", result.InsuranceAnalysis.PolicyRecommendations[0].Product);
        Assert.Equal(3, result.EmotionBreakdown.Count);
        Assert.Equal(0.85, result.EmotionBreakdown["frustration"]);
        Assert.True(result.Quality.IsValid);
        Assert.Equal(88, result.Quality.QualityScore);
    }

    [Fact]
    public async Task Handle_WithNonExistentId_ReturnsNull()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((AnalysisRecord?)null);

        // Act
        var result = await _handler.Handle(new GetAnalysisByIdQuery(999), CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_WithEmptyJsonFields_ReturnsDefaults()
    {
        // Arrange
        var record = new AnalysisRecord
        {
            Id = 1,
            InputText = "Test insurance query about coverage options.",
            InteractionType = "General",
            Sentiment = "Neutral",
            ConfidenceScore = 0.5,
            Explanation = "General inquiry.",
            PurchaseIntentScore = 50,
            CustomerPersona = "NewBuyer",
            JourneyStage = "Awareness",
            ChurnRisk = "Low",
            ComplaintEscalationRisk = "Low",
            FraudIndicators = "None",
            KeyTopics = "",
            PolicyRecommendationsJson = "",
            EmotionBreakdownJson = "",
            IsValid = true,
            QualityScore = 70,
            CreatedAt = DateTime.UtcNow
        };

        _mockRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(record);

        // Act
        var result = await _handler.Handle(new GetAnalysisByIdQuery(1), CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.EmotionBreakdown);
        Assert.Empty(result.InsuranceAnalysis.PolicyRecommendations);
        Assert.Empty(result.InsuranceAnalysis.KeyTopics);
    }

    [Fact]
    public async Task Handle_WithMalformedJson_ReturnsDefaults()
    {
        // Arrange
        var record = new AnalysisRecord
        {
            Id = 2,
            InputText = "My auto claim was denied without explanation.",
            InteractionType = "Complaint",
            Sentiment = "Negative",
            ConfidenceScore = 0.9,
            Explanation = "Claim denial frustration.",
            PurchaseIntentScore = 5,
            CustomerPersona = "ClaimFrustrated",
            JourneyStage = "ActiveClaim",
            ChurnRisk = "High",
            ComplaintEscalationRisk = "High",
            FraudIndicators = "None",
            KeyTopics = "claim denial",
            PolicyRecommendationsJson = "not-valid-json",
            EmotionBreakdownJson = "{broken",
            IsValid = true,
            QualityScore = 60,
            CreatedAt = DateTime.UtcNow
        };

        _mockRepository.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(record);

        // Act
        var result = await _handler.Handle(new GetAnalysisByIdQuery(2), CancellationToken.None);

        // Assert - should gracefully handle malformed JSON
        Assert.NotNull(result);
        Assert.Empty(result.EmotionBreakdown);
        Assert.Empty(result.InsuranceAnalysis.PolicyRecommendations);
    }
}
