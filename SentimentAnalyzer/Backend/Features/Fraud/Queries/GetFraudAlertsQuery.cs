using MediatR;
using SentimentAnalyzer.API.Models;
using SentimentAnalyzer.API.Services.Fraud;

namespace SentimentAnalyzer.API.Features.Fraud.Queries;

/// <summary>
/// Query to retrieve claims flagged as fraud alerts.
/// </summary>
public record GetFraudAlertsQuery(double MinFraudScore = 55, int PageSize = 50) : IRequest<List<FraudAnalysisResponse>>;

/// <summary>
/// Handler that loads fraud alerts from the repository.
/// </summary>
public class GetFraudAlertsHandler : IRequestHandler<GetFraudAlertsQuery, List<FraudAnalysisResponse>>
{
    private readonly IFraudAnalysisService _fraudService;

    public GetFraudAlertsHandler(IFraudAnalysisService fraudService)
    {
        _fraudService = fraudService ?? throw new ArgumentNullException(nameof(fraudService));
    }

    public async Task<List<FraudAnalysisResponse>> Handle(GetFraudAlertsQuery query, CancellationToken cancellationToken)
    {
        return await _fraudService.GetFraudAlertsAsync(query.MinFraudScore, query.PageSize);
    }
}
