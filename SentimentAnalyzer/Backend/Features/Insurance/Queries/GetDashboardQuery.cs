using MediatR;
using SentimentAnalyzer.API.Data;
using SentimentAnalyzer.API.Models;

namespace SentimentAnalyzer.API.Features.Insurance.Queries;

public record GetDashboardQuery : IRequest<DashboardData>;

public class GetDashboardHandler : IRequestHandler<GetDashboardQuery, DashboardData>
{
    private readonly IAnalysisRepository _repository;

    public GetDashboardHandler(IAnalysisRepository repository)
    {
        _repository = repository;
    }

    public async Task<DashboardData> Handle(GetDashboardQuery request, CancellationToken cancellationToken)
    {
        var metrics = await _repository.GetMetricsAsync();
        var distribution = await _repository.GetSentimentDistributionAsync();
        var personas = await _repository.GetTopPersonasAsync();

        return new DashboardData
        {
            Metrics = metrics,
            SentimentDistribution = distribution,
            TopPersonas = personas
        };
    }
}
