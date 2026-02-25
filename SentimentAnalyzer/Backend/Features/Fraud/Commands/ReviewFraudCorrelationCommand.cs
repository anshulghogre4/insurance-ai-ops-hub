using MediatR;
using SentimentAnalyzer.API.Data;

namespace SentimentAnalyzer.API.Features.Fraud.Commands;

/// <summary>
/// Command to review (confirm/dismiss) a fraud correlation.
/// Updates the review status, reviewer identity, and optional dismissal reason.
/// </summary>
public record ReviewFraudCorrelationCommand(
    int CorrelationId,
    string Status,
    string? ReviewedBy,
    string? DismissalReason) : IRequest<bool>;

/// <summary>
/// Handler for reviewing fraud correlation status.
/// Returns true if the correlation was found and updated, false if not found.
/// </summary>
public class ReviewFraudCorrelationHandler : IRequestHandler<ReviewFraudCorrelationCommand, bool>
{
    private readonly IFraudCorrelationRepository _repo;
    private readonly ILogger<ReviewFraudCorrelationHandler> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="ReviewFraudCorrelationHandler"/>.
    /// </summary>
    /// <param name="repo">The fraud correlation repository.</param>
    /// <param name="logger">Logger instance.</param>
    public ReviewFraudCorrelationHandler(
        IFraudCorrelationRepository repo,
        ILogger<ReviewFraudCorrelationHandler> logger)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Handles the review command by looking up the correlation and updating its status.
    /// </summary>
    /// <param name="request">The review command with correlation ID, new status, reviewer, and optional dismissal reason.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the correlation was found and updated; false if not found.</returns>
    public async Task<bool> Handle(ReviewFraudCorrelationCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Reviewing fraud correlation {CorrelationId}: status={Status}, reviewer={ReviewedBy}",
            request.CorrelationId, request.Status, request.ReviewedBy ?? "unknown");

        var record = await _repo.GetByIdAsync(request.CorrelationId);
        if (record is null)
        {
            _logger.LogWarning(
                "Fraud correlation {CorrelationId} not found for review",
                request.CorrelationId);
            return false;
        }

        await _repo.UpdateCorrelationStatusAsync(
            request.CorrelationId,
            request.Status,
            request.ReviewedBy,
            request.DismissalReason);

        _logger.LogInformation(
            "Fraud correlation {CorrelationId} reviewed successfully: status={Status}",
            request.CorrelationId, request.Status);

        return true;
    }
}
