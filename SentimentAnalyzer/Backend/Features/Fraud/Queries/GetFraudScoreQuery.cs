using MediatR;
using SentimentAnalyzer.API.Models;
using SentimentAnalyzer.API.Services.Fraud;

namespace SentimentAnalyzer.API.Features.Fraud.Queries;

/// <summary>
/// Query to retrieve fraud score for a specific claim.
/// </summary>
public record GetFraudScoreQuery(int ClaimId) : IRequest<FraudAnalysisResponse?>;

/// <summary>
/// Handler that loads fraud score from the repository.
/// </summary>
public class GetFraudScoreHandler : IRequestHandler<GetFraudScoreQuery, FraudAnalysisResponse?>
{
    private readonly IFraudAnalysisService _fraudService;

    public GetFraudScoreHandler(IFraudAnalysisService fraudService)
    {
        _fraudService = fraudService ?? throw new ArgumentNullException(nameof(fraudService));
    }

    public async Task<FraudAnalysisResponse?> Handle(GetFraudScoreQuery query, CancellationToken cancellationToken)
    {
        return await _fraudService.GetFraudScoreAsync(query.ClaimId);
    }
}
