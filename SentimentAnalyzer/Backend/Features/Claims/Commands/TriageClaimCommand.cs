using MediatR;
using SentimentAnalyzer.API.Models;
using SentimentAnalyzer.API.Services.Claims;
using SentimentAnalyzer.Domain.Enums;

namespace SentimentAnalyzer.API.Features.Claims.Commands;

/// <summary>
/// Command to submit a claim for triage assessment.
/// </summary>
public record TriageClaimCommand(string Text, string InteractionType = "Complaint") : IRequest<ClaimTriageResponse>;

/// <summary>
/// Handler that validates input and delegates to the claims orchestration service.
/// </summary>
public class TriageClaimHandler : IRequestHandler<TriageClaimCommand, ClaimTriageResponse>
{
    private readonly IClaimsOrchestrationService _claimsService;
    private readonly ILogger<TriageClaimHandler> _logger;

    public TriageClaimHandler(IClaimsOrchestrationService claimsService, ILogger<TriageClaimHandler> logger)
    {
        _claimsService = claimsService ?? throw new ArgumentNullException(nameof(claimsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ClaimTriageResponse> Handle(TriageClaimCommand command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing triage claim command, interaction type: {Type}", command.InteractionType);

        if (!Enum.TryParse<InteractionType>(command.InteractionType, true, out var interactionType))
        {
            interactionType = InteractionType.Complaint;
        }

        var result = await _claimsService.TriageClaimAsync(command.Text, interactionType);

        _logger.LogInformation("Claim triaged: ClaimId={ClaimId}, Severity={Severity}",
            result.ClaimId, result.Severity);

        return result;
    }
}
