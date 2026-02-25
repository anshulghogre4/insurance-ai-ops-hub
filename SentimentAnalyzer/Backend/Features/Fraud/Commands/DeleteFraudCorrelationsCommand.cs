using MediatR;
using SentimentAnalyzer.API.Data;

namespace SentimentAnalyzer.API.Features.Fraud.Commands;

/// <summary>
/// Command to delete all fraud correlations associated with a specific claim.
/// Removes correlations where the claim is either the source or the correlated side.
/// </summary>
public record DeleteFraudCorrelationsCommand(int ClaimId) : IRequest;

/// <summary>
/// Handler that delegates to the fraud correlation repository to delete correlations for a claim.
/// </summary>
public class DeleteFraudCorrelationsHandler : IRequestHandler<DeleteFraudCorrelationsCommand>
{
    private readonly IFraudCorrelationRepository _repo;
    private readonly ILogger<DeleteFraudCorrelationsHandler> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="DeleteFraudCorrelationsHandler"/>.
    /// </summary>
    /// <param name="repo">The fraud correlation repository.</param>
    /// <param name="logger">Logger instance.</param>
    public DeleteFraudCorrelationsHandler(
        IFraudCorrelationRepository repo,
        ILogger<DeleteFraudCorrelationsHandler> logger)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Handles the command by deleting all fraud correlations for the given claim.
    /// </summary>
    public async Task Handle(DeleteFraudCorrelationsCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Deleting all fraud correlations for claim {ClaimId}",
            request.ClaimId);

        await _repo.DeleteByClaimIdAsync(request.ClaimId);

        _logger.LogInformation(
            "Successfully deleted fraud correlations for claim {ClaimId}",
            request.ClaimId);
    }
}
