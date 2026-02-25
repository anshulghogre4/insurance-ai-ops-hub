using MediatR;
using SentimentAnalyzer.API.Models;
using SentimentAnalyzer.API.Services.Fraud;

namespace SentimentAnalyzer.API.Features.Fraud.Queries;

/// <summary>
/// Query to retrieve previously stored fraud correlations for a specific claim.
/// Supports pagination. Returns correlations where the claim is either the source or the correlated side.
/// </summary>
public record GetFraudCorrelationsQuery(int ClaimId, int Page = 1, int PageSize = 20)
    : IRequest<PaginatedResponse<FraudCorrelationResponse>>;

/// <summary>
/// Handler that delegates to the fraud correlation service to retrieve stored correlations with pagination.
/// </summary>
public class GetFraudCorrelationsHandler : IRequestHandler<GetFraudCorrelationsQuery, PaginatedResponse<FraudCorrelationResponse>>
{
    private readonly IFraudCorrelationService _correlationService;
    private readonly ILogger<GetFraudCorrelationsHandler> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="GetFraudCorrelationsHandler"/>.
    /// </summary>
    /// <param name="correlationService">The fraud correlation service.</param>
    /// <param name="logger">Logger instance.</param>
    public GetFraudCorrelationsHandler(
        IFraudCorrelationService correlationService,
        ILogger<GetFraudCorrelationsHandler> logger)
    {
        _correlationService = correlationService ?? throw new ArgumentNullException(nameof(correlationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Handles the query by retrieving stored fraud correlations for the given claim with pagination.
    /// </summary>
    public async Task<PaginatedResponse<FraudCorrelationResponse>> Handle(
        GetFraudCorrelationsQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Retrieving fraud correlations for claim {ClaimId} (page {Page}, size {PageSize})",
            query.ClaimId, query.Page, query.PageSize);

        var result = await _correlationService.GetCorrelationsAsync(query.ClaimId, query.Page, query.PageSize);

        _logger.LogInformation(
            "Retrieved {Count} fraud correlations for claim {ClaimId} (total: {Total})",
            result.Items.Count, query.ClaimId, result.TotalCount);

        return result;
    }
}
