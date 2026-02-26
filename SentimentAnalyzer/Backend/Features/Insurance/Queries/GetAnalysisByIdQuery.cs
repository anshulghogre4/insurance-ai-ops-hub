using System.Text.Json;
using MediatR;
using SentimentAnalyzer.API.Data;
using SentimentAnalyzer.API.Models;

namespace SentimentAnalyzer.API.Features.Insurance.Queries;

/// <summary>
/// Query to retrieve a single analysis by its ID, returning the full InsuranceAnalysisResponse.
/// </summary>
public record GetAnalysisByIdQuery(int Id) : IRequest<InsuranceAnalysisResponse?>;

/// <summary>
/// Handler that maps a persisted AnalysisRecord back to the full API response shape.
/// </summary>
public class GetAnalysisByIdHandler : IRequestHandler<GetAnalysisByIdQuery, InsuranceAnalysisResponse?>
{
    private readonly IAnalysisRepository _repository;

    public GetAnalysisByIdHandler(IAnalysisRepository repository)
    {
        _repository = repository;
    }

    public async Task<InsuranceAnalysisResponse?> Handle(GetAnalysisByIdQuery request, CancellationToken cancellationToken)
    {
        var record = await _repository.GetByIdAsync(request.Id);
        if (record is null)
        {
            return null;
        }

        return new InsuranceAnalysisResponse
        {
            InputText = record.InputText,
            Sentiment = record.Sentiment,
            ConfidenceScore = record.ConfidenceScore,
            Explanation = record.Explanation,
            EmotionBreakdown = DeserializeEmotions(record.EmotionBreakdownJson),
            InsuranceAnalysis = new InsuranceAnalysisDetail
            {
                PurchaseIntentScore = record.PurchaseIntentScore,
                CustomerPersona = record.CustomerPersona,
                JourneyStage = record.JourneyStage,
                RiskIndicators = new RiskIndicatorDetail
                {
                    ChurnRisk = record.ChurnRisk,
                    ComplaintEscalationRisk = record.ComplaintEscalationRisk,
                    FraudIndicators = record.FraudIndicators
                },
                PolicyRecommendations = DeserializeRecommendations(record.PolicyRecommendationsJson),
                InteractionType = record.InteractionType,
                KeyTopics = string.IsNullOrWhiteSpace(record.KeyTopics)
                    ? []
                    : record.KeyTopics.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
            },
            Quality = new QualityDetail
            {
                IsValid = record.IsValid,
                QualityScore = record.QualityScore,
                Issues = [],
                Suggestions = [],
                Warnings = []
            }
        };
    }

    private static Dictionary<string, double> DeserializeEmotions(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, double>();
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, double>>(json) ?? new Dictionary<string, double>();
        }
        catch
        {
            return new Dictionary<string, double>();
        }
    }

    private static List<PolicyRecommendationDetail> DeserializeRecommendations(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<PolicyRecommendationDetail>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
