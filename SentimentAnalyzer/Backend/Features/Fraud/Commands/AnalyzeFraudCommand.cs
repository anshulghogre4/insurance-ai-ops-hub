using MediatR;
using SentimentAnalyzer.API.Models;
using SentimentAnalyzer.API.Services.Fraud;

namespace SentimentAnalyzer.API.Features.Fraud.Commands;

/// <summary>
/// Command to perform fraud analysis on an existing claim.
/// </summary>
public record AnalyzeFraudCommand(int ClaimId) : IRequest<FraudAnalysisResponse>;

/// <summary>
/// Handler that delegates to the fraud analysis service.
/// </summary>
public class AnalyzeFraudHandler : IRequestHandler<AnalyzeFraudCommand, FraudAnalysisResponse>
{
    private readonly IFraudAnalysisService _fraudService;
    private readonly ILogger<AnalyzeFraudHandler> _logger;

    public AnalyzeFraudHandler(IFraudAnalysisService fraudService, ILogger<AnalyzeFraudHandler> logger)
    {
        _fraudService = fraudService ?? throw new ArgumentNullException(nameof(fraudService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<FraudAnalysisResponse> Handle(AnalyzeFraudCommand command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting fraud analysis for claim {ClaimId}", command.ClaimId);

        var result = await _fraudService.AnalyzeFraudAsync(command.ClaimId);

        _logger.LogInformation("Fraud analysis completed for claim {ClaimId}: Score={Score}, Risk={Risk}",
            command.ClaimId, result.FraudScore, result.RiskLevel);

        return result;
    }
}
