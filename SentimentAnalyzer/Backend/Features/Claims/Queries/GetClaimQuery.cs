using MediatR;
using SentimentAnalyzer.API.Models;
using SentimentAnalyzer.API.Services.Claims;

namespace SentimentAnalyzer.API.Features.Claims.Queries;

/// <summary>
/// Query to retrieve a claim by its ID.
/// </summary>
public record GetClaimQuery(int ClaimId) : IRequest<ClaimTriageResponse?>;

/// <summary>
/// Handler that loads a claim from the repository.
/// </summary>
public class GetClaimHandler : IRequestHandler<GetClaimQuery, ClaimTriageResponse?>
{
    private readonly IClaimsOrchestrationService _claimsService;

    public GetClaimHandler(IClaimsOrchestrationService claimsService)
    {
        _claimsService = claimsService ?? throw new ArgumentNullException(nameof(claimsService));
    }

    public async Task<ClaimTriageResponse?> Handle(GetClaimQuery query, CancellationToken cancellationToken)
    {
        return await _claimsService.GetClaimAsync(query.ClaimId);
    }
}
