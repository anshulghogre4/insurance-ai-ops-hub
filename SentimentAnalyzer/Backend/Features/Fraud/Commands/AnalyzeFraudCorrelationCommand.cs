using MediatR;
using SentimentAnalyzer.API.Models;
using SentimentAnalyzer.API.Services.Fraud;

namespace SentimentAnalyzer.API.Features.Fraud.Commands;

/// <summary>
/// Command to trigger cross-claim fraud correlation analysis for a specific claim.
/// Analyzes the claim against all existing claims for suspicious patterns:
/// date proximity, narrative similarity, shared fraud flags, and severity clustering.
/// </summary>
public record AnalyzeFraudCorrelationCommand(int ClaimId) : IRequest<List<FraudCorrelationResponse>>;

/// <summary>
/// Handler that delegates to the fraud correlation service to perform cross-claim analysis.
/// </summary>
public class AnalyzeFraudCorrelationHandler : IRequestHandler<AnalyzeFraudCorrelationCommand, List<FraudCorrelationResponse>>
{
    private readonly IFraudCorrelationService _correlationService;
    private readonly ILogger<AnalyzeFraudCorrelationHandler> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="AnalyzeFraudCorrelationHandler"/>.
    /// </summary>
    /// <param name="correlationService">The fraud correlation service.</param>
    /// <param name="logger">Logger instance.</param>
    public AnalyzeFraudCorrelationHandler(
        IFraudCorrelationService correlationService,
        ILogger<AnalyzeFraudCorrelationHandler> logger)
    {
        _correlationService = correlationService ?? throw new ArgumentNullException(nameof(correlationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Handles the command by running cross-claim fraud correlation analysis.
    /// </summary>
    public async Task<List<FraudCorrelationResponse>> Handle(
        AnalyzeFraudCorrelationCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Executing fraud correlation analysis command for claim {ClaimId}",
            command.ClaimId);

        var correlations = await _correlationService.AnalyzeCorrelationsAsync(
            command.ClaimId, cancellationToken);

        _logger.LogInformation(
            "Fraud correlation analysis completed for claim {ClaimId}: {Count} correlations discovered",
            command.ClaimId, correlations.Count);

        return correlations;
    }
}
