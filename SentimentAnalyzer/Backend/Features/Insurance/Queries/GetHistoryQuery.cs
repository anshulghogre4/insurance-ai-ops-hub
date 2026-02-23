using MediatR;
using SentimentAnalyzer.API.Data;
using SentimentAnalyzer.API.Models;

namespace SentimentAnalyzer.API.Features.Insurance.Queries;

public record GetHistoryQuery(int Count = 20) : IRequest<List<AnalysisHistoryItem>>;

public class GetHistoryHandler : IRequestHandler<GetHistoryQuery, List<AnalysisHistoryItem>>
{
    private readonly IAnalysisRepository _repository;

    public GetHistoryHandler(IAnalysisRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<AnalysisHistoryItem>> Handle(GetHistoryQuery request, CancellationToken cancellationToken)
    {
        var records = await _repository.GetRecentAsync(Math.Clamp(request.Count, 1, 100));

        return records.Select(r => new AnalysisHistoryItem
        {
            Id = r.Id,
            InputTextPreview = r.InputText.Length > 100 ? r.InputText[..100] + "..." : r.InputText,
            Sentiment = r.Sentiment,
            PurchaseIntentScore = r.PurchaseIntentScore,
            CustomerPersona = r.CustomerPersona,
            InteractionType = r.InteractionType,
            ChurnRisk = r.ChurnRisk,
            CreatedAt = r.CreatedAt
        }).ToList();
    }
}
