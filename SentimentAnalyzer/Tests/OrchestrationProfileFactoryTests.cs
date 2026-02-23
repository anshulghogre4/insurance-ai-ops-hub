using SentimentAnalyzer.Agents.Configuration;
using SentimentAnalyzer.Agents.Definitions;
using SentimentAnalyzer.Agents.Orchestration;
using Xunit;

namespace SentimentAnalyzer.Tests;

/// <summary>
/// Tests for OrchestrationProfileFactory — verifies agent subsets,
/// turn counts, and profile-to-agent mapping for all orchestration profiles.
/// </summary>
public class OrchestrationProfileFactoryTests
{
    private readonly OrchestrationProfileFactory _factory = new();

    // ──────────────────────────────────────────
    // SentimentAnalysis profile tests
    // ──────────────────────────────────────────

    [Fact]
    public void SentimentAnalysis_ReturnsAllSevenAgents()
    {
        var agents = _factory.GetAgentNamesForProfile(OrchestrationProfile.SentimentAnalysis);
        Assert.Equal(7, agents.Count);
    }

    [Fact]
    public void SentimentAnalysis_IncludesCTOAndBA()
    {
        var agents = _factory.GetAgentNamesForProfile(OrchestrationProfile.SentimentAnalysis);
        Assert.Contains(AgentDefinitions.CTOAgentName, agents);
        Assert.Contains(AgentDefinitions.BAAgentName, agents);
    }

    [Fact]
    public void SentimentAnalysis_MaxTurns15_MinTurns5()
    {
        Assert.Equal(15, _factory.GetMaxTurnsForProfile(OrchestrationProfile.SentimentAnalysis));
        Assert.Equal(5, _factory.GetMinTurnsForProfile(OrchestrationProfile.SentimentAnalysis));
    }

    // ──────────────────────────────────────────
    // ClaimsTriage profile tests
    // ──────────────────────────────────────────

    [Fact]
    public void ClaimsTriage_ReturnsFourAgents()
    {
        var agents = _factory.GetAgentNamesForProfile(OrchestrationProfile.ClaimsTriage);
        Assert.Equal(4, agents.Count);
    }

    [Fact]
    public void ClaimsTriage_IncludesClaimsAndFraudSpecialists()
    {
        var agents = _factory.GetAgentNamesForProfile(OrchestrationProfile.ClaimsTriage);
        Assert.Contains(AgentDefinitions.ClaimsTriageAgentName, agents);
        Assert.Contains(AgentDefinitions.FraudDetectionAgentName, agents);
    }

    [Fact]
    public void ClaimsTriage_AlwaysIncludesBAAndQA()
    {
        var agents = _factory.GetAgentNamesForProfile(OrchestrationProfile.ClaimsTriage);
        Assert.Contains(AgentDefinitions.BAAgentName, agents);
        Assert.Contains(AgentDefinitions.QAAgentName, agents);
    }

    [Fact]
    public void ClaimsTriage_MaxTurns8_MinTurns3()
    {
        Assert.Equal(8, _factory.GetMaxTurnsForProfile(OrchestrationProfile.ClaimsTriage));
        Assert.Equal(3, _factory.GetMinTurnsForProfile(OrchestrationProfile.ClaimsTriage));
    }

    // ──────────────────────────────────────────
    // FraudScoring profile tests
    // ──────────────────────────────────────────

    [Fact]
    public void FraudScoring_ReturnsFourAgents()
    {
        var agents = _factory.GetAgentNamesForProfile(OrchestrationProfile.FraudScoring);
        Assert.Equal(4, agents.Count);
    }

    [Fact]
    public void FraudScoring_FraudDetectionIsFirst()
    {
        var agents = _factory.GetAgentNamesForProfile(OrchestrationProfile.FraudScoring);
        Assert.Equal(AgentDefinitions.FraudDetectionAgentName, agents[0]);
    }

    [Fact]
    public void FraudScoring_MaxTurns8_MinTurns3()
    {
        Assert.Equal(8, _factory.GetMaxTurnsForProfile(OrchestrationProfile.FraudScoring));
        Assert.Equal(3, _factory.GetMinTurnsForProfile(OrchestrationProfile.FraudScoring));
    }

    // ──────────────────────────────────────────
    // DocumentQuery profile tests
    // ──────────────────────────────────────────

    [Fact]
    public void DocumentQuery_ReturnsThreeAgents()
    {
        var agents = _factory.GetAgentNamesForProfile(OrchestrationProfile.DocumentQuery);
        Assert.Equal(3, agents.Count);
    }

    [Fact]
    public void DocumentQuery_IncludesBAAndDeveloper()
    {
        var agents = _factory.GetAgentNamesForProfile(OrchestrationProfile.DocumentQuery);
        Assert.Contains(AgentDefinitions.BAAgentName, agents);
        Assert.Contains(AgentDefinitions.DeveloperAgentName, agents);
    }

    [Fact]
    public void DocumentQuery_MaxTurns6_MinTurns2()
    {
        Assert.Equal(6, _factory.GetMaxTurnsForProfile(OrchestrationProfile.DocumentQuery));
        Assert.Equal(2, _factory.GetMinTurnsForProfile(OrchestrationProfile.DocumentQuery));
    }

    // ──────────────────────────────────────────
    // CustomerExperience profile tests
    // ──────────────────────────────────────────

    [Fact]
    public void CustomerExperience_ReturnsThreeAgents()
    {
        var agents = _factory.GetAgentNamesForProfile(OrchestrationProfile.CustomerExperience);
        Assert.Equal(3, agents.Count);
    }

    [Fact]
    public void CustomerExperience_IncludesCTOAndBA()
    {
        var agents = _factory.GetAgentNamesForProfile(OrchestrationProfile.CustomerExperience);
        Assert.Contains(AgentDefinitions.CTOAgentName, agents);
        Assert.Contains(AgentDefinitions.BAAgentName, agents);
    }

    [Fact]
    public void CustomerExperience_MaxTurns10_MinTurns3()
    {
        Assert.Equal(10, _factory.GetMaxTurnsForProfile(OrchestrationProfile.CustomerExperience));
        Assert.Equal(3, _factory.GetMinTurnsForProfile(OrchestrationProfile.CustomerExperience));
    }

    // ──────────────────────────────────────────
    // Cross-profile validation
    // ──────────────────────────────────────────

    [Theory]
    [InlineData(OrchestrationProfile.SentimentAnalysis)]
    [InlineData(OrchestrationProfile.ClaimsTriage)]
    [InlineData(OrchestrationProfile.FraudScoring)]
    [InlineData(OrchestrationProfile.DocumentQuery)]
    [InlineData(OrchestrationProfile.CustomerExperience)]
    public void AllProfiles_IncludeBAAgent(OrchestrationProfile profile)
    {
        var agents = _factory.GetAgentNamesForProfile(profile);
        Assert.Contains(AgentDefinitions.BAAgentName, agents);
    }

    [Theory]
    [InlineData(OrchestrationProfile.SentimentAnalysis)]
    [InlineData(OrchestrationProfile.ClaimsTriage)]
    [InlineData(OrchestrationProfile.FraudScoring)]
    [InlineData(OrchestrationProfile.DocumentQuery)]
    [InlineData(OrchestrationProfile.CustomerExperience)]
    public void AllProfiles_IncludeQAAgent(OrchestrationProfile profile)
    {
        var agents = _factory.GetAgentNamesForProfile(profile);
        Assert.Contains(AgentDefinitions.QAAgentName, agents);
    }

    [Theory]
    [InlineData(OrchestrationProfile.SentimentAnalysis)]
    [InlineData(OrchestrationProfile.ClaimsTriage)]
    [InlineData(OrchestrationProfile.FraudScoring)]
    [InlineData(OrchestrationProfile.DocumentQuery)]
    [InlineData(OrchestrationProfile.CustomerExperience)]
    public void AllProfiles_MaxTurnsGreaterThanMinTurns(OrchestrationProfile profile)
    {
        var max = _factory.GetMaxTurnsForProfile(profile);
        var min = _factory.GetMinTurnsForProfile(profile);
        Assert.True(max > min, $"Max turns ({max}) should exceed min turns ({min}) for {profile}");
    }

    [Theory]
    [InlineData(OrchestrationProfile.SentimentAnalysis)]
    [InlineData(OrchestrationProfile.ClaimsTriage)]
    [InlineData(OrchestrationProfile.FraudScoring)]
    [InlineData(OrchestrationProfile.DocumentQuery)]
    [InlineData(OrchestrationProfile.CustomerExperience)]
    public void AllProfiles_ReturnNonEmptyAgentList(OrchestrationProfile profile)
    {
        var agents = _factory.GetAgentNamesForProfile(profile);
        Assert.NotEmpty(agents);
    }

    [Fact]
    public void ClaimsTriage_HasFewerAgentsThanSentimentAnalysis()
    {
        var claimsAgents = _factory.GetAgentNamesForProfile(OrchestrationProfile.ClaimsTriage);
        var sentimentAgents = _factory.GetAgentNamesForProfile(OrchestrationProfile.SentimentAnalysis);
        Assert.True(claimsAgents.Count < sentimentAgents.Count,
            "ClaimsTriage should use fewer agents than SentimentAnalysis for token efficiency");
    }
}
