using MediatR;
using SentimentAnalyzer.API.Models;
using SentimentAnalyzer.API.Services.Claims;

namespace SentimentAnalyzer.API.Features.Claims.Commands;

/// <summary>
/// Command to upload and process multimodal evidence for a claim.
/// </summary>
public record UploadClaimEvidenceCommand(int ClaimId, byte[] FileData, string MimeType, string FileName) : IRequest<ClaimEvidenceResponse>;

/// <summary>
/// Handler that routes evidence to appropriate multimodal service.
/// </summary>
public class UploadClaimEvidenceHandler : IRequestHandler<UploadClaimEvidenceCommand, ClaimEvidenceResponse>
{
    private readonly IMultimodalEvidenceProcessor _evidenceProcessor;
    private readonly ILogger<UploadClaimEvidenceHandler> _logger;

    public UploadClaimEvidenceHandler(IMultimodalEvidenceProcessor evidenceProcessor, ILogger<UploadClaimEvidenceHandler> logger)
    {
        _evidenceProcessor = evidenceProcessor ?? throw new ArgumentNullException(nameof(evidenceProcessor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ClaimEvidenceResponse> Handle(UploadClaimEvidenceCommand command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing evidence upload for claim {ClaimId}: {MimeType}, {Size} bytes",
            command.ClaimId, command.MimeType, command.FileData.Length);

        var result = await _evidenceProcessor.ProcessAsync(
            command.ClaimId, command.FileData, command.MimeType, command.FileName);

        _logger.LogInformation("Evidence processed for claim {ClaimId}: type={Type}, provider={Provider}",
            command.ClaimId, result.EvidenceType, result.Provider);

        return result;
    }
}
