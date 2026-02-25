using MediatR;
using SentimentAnalyzer.API.Models;
using SentimentAnalyzer.API.Services.Claims;

namespace SentimentAnalyzer.API.Features.Claims.Queries;

/// <summary>
/// Query to retrieve claims history with optional filters and pagination.
/// </summary>
public record GetClaimsHistoryQuery(
    string? Severity = null,
    string? Status = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int PageSize = 20,
    int Page = 1) : IRequest<PaginatedResponse<ClaimTriageResponse>>;

/// <summary>
/// Handler that loads filtered claims from the repository with pagination metadata.
/// </summary>
public class GetClaimsHistoryHandler : IRequestHandler<GetClaimsHistoryQuery, PaginatedResponse<ClaimTriageResponse>>
{
    private readonly IClaimsOrchestrationService _claimsService;

    public GetClaimsHistoryHandler(IClaimsOrchestrationService claimsService)
    {
        _claimsService = claimsService ?? throw new ArgumentNullException(nameof(claimsService));
    }

    public async Task<PaginatedResponse<ClaimTriageResponse>> Handle(GetClaimsHistoryQuery query, CancellationToken cancellationToken)
    {
        return await _claimsService.GetClaimsHistoryAsync(
            query.Severity, query.Status, query.FromDate, query.ToDate, query.PageSize, query.Page);
    }
}
