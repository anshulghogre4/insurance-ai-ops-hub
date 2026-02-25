using MediatR;
using SentimentAnalyzer.API.Models;
using SentimentAnalyzer.API.Services.Documents;

namespace SentimentAnalyzer.API.Features.Documents.Commands;

public record UploadDocumentCommand(
    byte[] FileData, string MimeType, string FileName, string Category = "Other")
    : IRequest<DocumentUploadResult>;

public class UploadDocumentHandler : IRequestHandler<UploadDocumentCommand, DocumentUploadResult>
{
    private readonly IDocumentIntelligenceService _service;

    public UploadDocumentHandler(IDocumentIntelligenceService service) => _service = service;

    public async Task<DocumentUploadResult> Handle(UploadDocumentCommand request, CancellationToken cancellationToken)
    {
        return await _service.UploadAsync(
            request.FileData, request.MimeType, request.FileName, request.Category, cancellationToken);
    }
}
