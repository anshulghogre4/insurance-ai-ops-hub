using System.Text.Json;
using MediatR;
using SentimentAnalyzer.Agents.Models;
using SentimentAnalyzer.Agents.Orchestration;
using SentimentAnalyzer.API.Data;
using SentimentAnalyzer.API.Data.Entities;
using SentimentAnalyzer.Domain.Enums;
using ApiModels = SentimentAnalyzer.API.Models;

namespace SentimentAnalyzer.API.Features.Insurance.Commands;

public record AnalyzeInsuranceCommand(
    string Text,
    string InteractionType = "General",
    string? CustomerId = null) : IRequest<ApiModels.InsuranceAnalysisResponse>;

public class AnalyzeInsuranceHandler : IRequestHandler<AnalyzeInsuranceCommand, ApiModels.InsuranceAnalysisResponse>
{
    private readonly IAnalysisOrchestrator _orchestrator;
    private readonly IAnalysisRepository _repository;
    private readonly ILogger<AnalyzeInsuranceHandler> _logger;

    public AnalyzeInsuranceHandler(
        IAnalysisOrchestrator orchestrator,
        IAnalysisRepository repository,
        ILogger<AnalyzeInsuranceHandler> logger)
    {
        _orchestrator = orchestrator;
        _repository = repository;
        _logger = logger;
    }

    public async Task<ApiModels.InsuranceAnalysisResponse> Handle(AnalyzeInsuranceCommand command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting insurance analysis for interaction type: {Type}", command.InteractionType);

        if (!Enum.TryParse<InteractionType>(command.InteractionType, true, out var interactionType))
        {
            interactionType = InteractionType.General;
        }

        var agentResult = await _orchestrator.AnalyzeAsync(command.Text, interactionType);
        var response = MapToResponse(agentResult);

        // Persist to database (don't block the response on failure)
        try
        {
            await PersistAnalysisAsync(command, response);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist analysis to database. Response still returned.");
        }

        _logger.LogInformation("Insurance analysis completed. Sentiment: {Sentiment}, Purchase Intent: {Intent}",
            response.Sentiment, response.InsuranceAnalysis.PurchaseIntentScore);

        return response;
    }

    private async Task PersistAnalysisAsync(AnalyzeInsuranceCommand command, ApiModels.InsuranceAnalysisResponse response)
    {
        var record = new AnalysisRecord
        {
            InputText = command.Text.Length > 2000 ? command.Text[..2000] : command.Text,
            InteractionType = command.InteractionType,
            CustomerId = command.CustomerId,
            Sentiment = response.Sentiment,
            ConfidenceScore = response.ConfidenceScore,
            Explanation = response.Explanation.Length > 1000 ? response.Explanation[..1000] : response.Explanation,
            PurchaseIntentScore = response.InsuranceAnalysis.PurchaseIntentScore,
            CustomerPersona = response.InsuranceAnalysis.CustomerPersona,
            JourneyStage = response.InsuranceAnalysis.JourneyStage,
            ChurnRisk = response.InsuranceAnalysis.RiskIndicators.ChurnRisk,
            ComplaintEscalationRisk = response.InsuranceAnalysis.RiskIndicators.ComplaintEscalationRisk,
            FraudIndicators = response.InsuranceAnalysis.RiskIndicators.FraudIndicators,
            KeyTopics = string.Join(",", response.InsuranceAnalysis.KeyTopics),
            PolicyRecommendationsJson = JsonSerializer.Serialize(response.InsuranceAnalysis.PolicyRecommendations),
            EmotionBreakdownJson = JsonSerializer.Serialize(response.EmotionBreakdown),
            IsValid = response.Quality.IsValid,
            QualityScore = response.Quality.QualityScore,
            CreatedAt = DateTime.UtcNow
        };

        await _repository.SaveAsync(record);
        _logger.LogInformation("Analysis persisted with ID {Id}", record.Id);
    }

    private static ApiModels.InsuranceAnalysisResponse MapToResponse(AgentAnalysisResult agentResult)
    {
        return new ApiModels.InsuranceAnalysisResponse
        {
            Sentiment = agentResult.Sentiment,
            ConfidenceScore = Math.Clamp(agentResult.ConfidenceScore, 0.0, 1.0),
            Explanation = agentResult.Explanation,
            EmotionBreakdown = agentResult.EmotionBreakdown ?? new Dictionary<string, double>(),
            InsuranceAnalysis = new ApiModels.InsuranceAnalysisDetail
            {
                PurchaseIntentScore = Math.Clamp(agentResult.InsuranceAnalysis?.PurchaseIntentScore ?? 50, 0, 100),
                CustomerPersona = agentResult.InsuranceAnalysis?.CustomerPersona ?? "NewBuyer",
                JourneyStage = agentResult.InsuranceAnalysis?.JourneyStage ?? "Awareness",
                RiskIndicators = new ApiModels.RiskIndicatorDetail
                {
                    ChurnRisk = agentResult.InsuranceAnalysis?.RiskIndicators?.ChurnRisk ?? "Low",
                    ComplaintEscalationRisk = agentResult.InsuranceAnalysis?.RiskIndicators?.ComplaintEscalationRisk ?? "Low",
                    FraudIndicators = agentResult.InsuranceAnalysis?.RiskIndicators?.FraudIndicators ?? "None"
                },
                PolicyRecommendations = agentResult.InsuranceAnalysis?.PolicyRecommendations?
                    .Select(r => new ApiModels.PolicyRecommendationDetail
                    {
                        Product = r.Product,
                        Reasoning = r.Reasoning
                    }).ToList() ?? [],
                InteractionType = agentResult.InsuranceAnalysis?.InteractionType ?? "General",
                KeyTopics = agentResult.InsuranceAnalysis?.KeyTopics ?? []
            },
            Quality = MapQuality(agentResult)
        };
    }

    private static ApiModels.QualityDetail MapQuality(AgentAnalysisResult agentResult)
    {
        var issues = agentResult.Quality?.Issues?
            .Select(i => new ApiModels.QualityIssueDetail
            {
                Severity = i.Severity,
                Field = i.Field,
                Message = i.Message
            }).ToList() ?? [];

        var suggestions = agentResult.Quality?.Suggestions ?? [];

        // Build flattened warnings from issues + suggestions for backward compatibility
        var warnings = new List<string>();
        foreach (var issue in issues)
        {
            warnings.Add($"[{issue.Severity}] {issue.Field}: {issue.Message}");
        }
        warnings.AddRange(suggestions);

        return new ApiModels.QualityDetail
        {
            IsValid = agentResult.Quality?.IsValid ?? agentResult.IsSuccess,
            QualityScore = agentResult.Quality?.QualityScore ?? (agentResult.IsSuccess ? 80 : 0),
            Issues = issues,
            Suggestions = suggestions,
            Warnings = warnings
        };
    }
}
