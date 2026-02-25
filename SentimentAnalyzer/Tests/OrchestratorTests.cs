using SentimentAnalyzer.Agents.Configuration;
using SentimentAnalyzer.Agents.Definitions;
using SentimentAnalyzer.Agents.Orchestration;
using SentimentAnalyzer.Domain.Enums;
using Xunit;

namespace SentimentAnalyzer.Tests;

/// <summary>
/// Unit tests for InsuranceAnalysisOrchestrator utility methods.
/// Tests JSON extraction, normalization, parsing, profile routing, and default result creation.
/// Covers 0% → 60%+ of the orchestrator's static utility logic.
/// </summary>
public class OrchestratorTests
{
    // ===== NormalizeForJsonExtraction =====

    [Fact]
    public void NormalizeForJsonExtraction_WithJsonFence_StripsFence()
    {
        // Arrange
        var input = "```json\n{\"sentiment\": \"Negative\"}\n```";

        // Act
        var result = InsuranceAnalysisOrchestrator.NormalizeForJsonExtraction(input);

        // Assert
        Assert.Equal("{\"sentiment\": \"Negative\"}", result);
    }

    [Fact]
    public void NormalizeForJsonExtraction_WithJSONUppercaseFence_StripsFence()
    {
        // Arrange
        var input = "```JSON\n{\"key\": \"value\"}\n```";

        // Act
        var result = InsuranceAnalysisOrchestrator.NormalizeForJsonExtraction(input);

        // Assert
        Assert.Contains("\"key\"", result);
        Assert.DoesNotContain("```", result);
    }

    [Fact]
    public void NormalizeForJsonExtraction_WithPlainFence_StripsFence()
    {
        // Arrange
        var input = "```\n{\"data\": true}\n```";

        // Act
        var result = InsuranceAnalysisOrchestrator.NormalizeForJsonExtraction(input);

        // Assert
        Assert.Contains("\"data\"", result);
        Assert.DoesNotContain("```", result);
    }

    [Fact]
    public void NormalizeForJsonExtraction_WithNoFence_ReturnsOriginal()
    {
        // Arrange
        var input = "{\"sentiment\": \"Positive\"}";

        // Act
        var result = InsuranceAnalysisOrchestrator.NormalizeForJsonExtraction(input);

        // Assert
        Assert.Equal(input, result);
    }

    // ===== ExtractJson =====

    [Fact]
    public void ExtractJson_WithCleanJson_ReturnsJson()
    {
        // Arrange
        var input = "{\"sentiment\": \"Negative\", \"confidenceScore\": 0.92}";

        // Act
        var result = InsuranceAnalysisOrchestrator.ExtractJson(input);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("sentiment", result);
    }

    [Fact]
    public void ExtractJson_WithMarkdownFencedJson_ExtractsJson()
    {
        // Arrange
        var input = "Here is the result:\n```json\n{\"sentiment\": \"Positive\", \"confidenceScore\": 0.85}\n```\nThat's the analysis.";

        // Act
        var result = InsuranceAnalysisOrchestrator.ExtractJson(input);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Positive", result);
    }

    [Fact]
    public void ExtractJson_WithNestedBraces_HandlesCorrectly()
    {
        // Arrange — JSON with nested objects
        var input = "{\"sentiment\": \"Negative\", \"emotionBreakdown\": {\"anger\": 0.8, \"frustration\": 0.9}}";

        // Act
        var result = InsuranceAnalysisOrchestrator.ExtractJson(input);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("emotionBreakdown", result);
        Assert.Contains("anger", result);
    }

    [Fact]
    public void ExtractJson_WithBracesInsideStringLiterals_HandlesCorrectly()
    {
        // Arrange — braces inside string values should not confuse the parser
        var input = "{\"explanation\": \"The customer said {frustrated} about {claim}\", \"sentiment\": \"Negative\"}";

        // Act
        var result = InsuranceAnalysisOrchestrator.ExtractJson(input);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("sentiment", result);
    }

    [Fact]
    public void ExtractJson_WithNoJson_ReturnsNull()
    {
        // Arrange
        var input = "This is just plain text with no JSON at all.";

        // Act
        var result = InsuranceAnalysisOrchestrator.ExtractJson(input);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ExtractJson_WithInvalidJson_ReturnsNull()
    {
        // Arrange — malformed JSON (missing closing brace)
        var input = "{\"sentiment\": \"Negative\", \"confidenceScore\": ";

        // Act
        var result = InsuranceAnalysisOrchestrator.ExtractJson(input);

        // Assert
        Assert.Null(result);
    }

    // ===== ExtractLastJson =====

    [Fact]
    public void ExtractLastJson_WithMultipleJsonBlocks_ReturnsLastWithSentiment()
    {
        // Arrange — multiple JSON blocks, only the last has sentiment+confidenceScore
        var input = """
            [BA Agent]: {"domain": "claims"}
            [Developer Agent]: {"sentiment": "Negative", "confidenceScore": 0.88, "explanation": "Claim denial"}
            """;

        // Act
        var result = InsuranceAnalysisOrchestrator.ExtractLastJson(input);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Negative", result);
        Assert.Contains("confidenceScore", result);
    }

    [Fact]
    public void ExtractLastJson_WithNoSentimentField_ReturnsNull()
    {
        // Arrange — JSON without required sentiment + confidenceScore fields
        var input = "{\"domain\": \"claims\", \"type\": \"property\"}";

        // Act
        var result = InsuranceAnalysisOrchestrator.ExtractLastJson(input);

        // Assert
        Assert.Null(result);
    }

    // ===== ExtractLastJsonForProfile =====

    [Fact]
    public void ExtractLastJsonForProfile_ClaimsTriage_FindsTriageJson()
    {
        // Arrange
        var input = """
            [Claims Triage]: {"claimTriage": {"severity": "High", "urgency": "Immediate"}}
            """;

        // Act
        var result = InsuranceAnalysisOrchestrator.ExtractLastJsonForProfile(input, OrchestrationProfile.ClaimsTriage);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("claimTriage", result);
        Assert.Contains("High", result);
    }

    [Fact]
    public void ExtractLastJsonForProfile_FraudScoring_FindsFraudJson()
    {
        // Arrange
        var input = """
            [Fraud Agent]: {"fraudAnalysis": {"fraudProbabilityScore": 82, "riskLevel": "High"}}
            """;

        // Act
        var result = InsuranceAnalysisOrchestrator.ExtractLastJsonForProfile(input, OrchestrationProfile.FraudScoring);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("fraudAnalysis", result);
        Assert.Contains("82", result);
    }

    [Fact]
    public void ExtractLastJsonForProfile_WithIrrelevantJson_ReturnsNull()
    {
        // Arrange — JSON has no claims triage fields
        var input = "{\"domain\": \"general\", \"type\": \"inquiry\"}";

        // Act
        var result = InsuranceAnalysisOrchestrator.ExtractLastJsonForProfile(input, OrchestrationProfile.ClaimsTriage);

        // Assert
        Assert.Null(result);
    }

    // ===== IsValidJson =====

    [Fact]
    public void IsValidJson_WithValidJson_ReturnsTrue()
    {
        Assert.True(InsuranceAnalysisOrchestrator.IsValidJson("{\"key\": \"value\"}"));
    }

    [Fact]
    public void IsValidJson_WithInvalidJson_ReturnsFalse()
    {
        Assert.False(InsuranceAnalysisOrchestrator.IsValidJson("{invalid json}"));
    }

    // ===== BuildProfileUserMessage =====

    [Fact]
    public void BuildProfileUserMessage_ClaimsTriage_ContainsTriageInstructions()
    {
        // Act
        var message = InsuranceAnalysisOrchestrator.BuildProfileUserMessage(
            "Water damage to basement, pipe burst Jan 15",
            OrchestrationProfile.ClaimsTriage,
            InteractionType.Complaint);

        // Assert
        Assert.Contains("Triage the following insurance claim", message);
        Assert.Contains("severity", message);
        Assert.Contains("urgency", message);
        Assert.Contains("Water damage", message);
        Assert.Contains("Complaint", message);
    }

    [Fact]
    public void BuildProfileUserMessage_FraudScoring_ContainsFraudInstructions()
    {
        // Act
        var message = InsuranceAnalysisOrchestrator.BuildProfileUserMessage(
            "Claim filed 2 days after policy purchase for total loss",
            OrchestrationProfile.FraudScoring,
            InteractionType.General);

        // Assert
        Assert.Contains("fraud analysis", message);
        Assert.Contains("fraudProbabilityScore", message);
        Assert.Contains("referToSIU", message);
        Assert.Contains("total loss", message);
    }

    // ===== ResolveAgentDefinition =====

    [Fact]
    public void ResolveAgentDefinition_KnownAgent_ReturnsPrompt()
    {
        // Act
        var (name, prompt) = InsuranceAnalysisOrchestrator.ResolveAgentDefinition(AgentDefinitions.CTOAgentName);

        // Assert
        Assert.Equal(AgentDefinitions.CTOAgentName, name);
        Assert.NotNull(prompt);
        Assert.NotEmpty(prompt);
    }

    [Fact]
    public void ResolveAgentDefinition_UnknownAgent_ReturnsNullPrompt()
    {
        // Act
        var (name, prompt) = InsuranceAnalysisOrchestrator.ResolveAgentDefinition("NonExistentAgent");

        // Assert
        Assert.Equal("NonExistentAgent", name);
        Assert.Null(prompt);
    }

    [Theory]
    [InlineData(nameof(AgentDefinitions.BAAgentName))]
    [InlineData(nameof(AgentDefinitions.QAAgentName))]
    [InlineData(nameof(AgentDefinitions.ClaimsTriageAgentName))]
    [InlineData(nameof(AgentDefinitions.FraudDetectionAgentName))]
    public void ResolveAgentDefinition_AllKnownAgents_HavePrompts(string agentFieldName)
    {
        // Map field names to actual values
        var agentName = agentFieldName switch
        {
            nameof(AgentDefinitions.BAAgentName) => AgentDefinitions.BAAgentName,
            nameof(AgentDefinitions.QAAgentName) => AgentDefinitions.QAAgentName,
            nameof(AgentDefinitions.ClaimsTriageAgentName) => AgentDefinitions.ClaimsTriageAgentName,
            nameof(AgentDefinitions.FraudDetectionAgentName) => AgentDefinitions.FraudDetectionAgentName,
            _ => throw new ArgumentException($"Unknown agent field: {agentFieldName}")
        };

        // Act
        var (_, prompt) = InsuranceAnalysisOrchestrator.ResolveAgentDefinition(agentName);

        // Assert
        Assert.NotNull(prompt);
    }

    // ===== CreateDefaultResult =====

    [Fact]
    public void CreateDefaultResult_ReturnsNeutralSentiment()
    {
        // Act
        var result = InsuranceAnalysisOrchestrator.CreateDefaultResult(
            "I want to file a claim for hail damage to my vehicle",
            InteractionType.Complaint,
            "All providers failed");

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsSuccess);
        Assert.Equal("Neutral", result.Sentiment);
        Assert.Contains("All providers failed", result.Explanation);
    }

    [Fact]
    public void CreateDefaultResult_SetsDefaultConfidenceAndExplanation()
    {
        // Act
        var result = InsuranceAnalysisOrchestrator.CreateDefaultResult(
            "Policy inquiry about coverage limits",
            InteractionType.General,
            "Timed out");

        // Assert
        Assert.Equal(0.0, result.ConfidenceScore);
        Assert.NotNull(result.Explanation);
    }
}
