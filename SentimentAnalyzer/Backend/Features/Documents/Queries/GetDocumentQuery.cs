using MediatR;
using SentimentAnalyzer.API.Models;
using SentimentAnalyzer.API.Services.Documents;

namespace SentimentAnalyzer.API.Features.Documents.Queries;

public record GetDocumentQuery(int DocumentId) : IRequest<DocumentDetailResult?>;

public class GetDocumentHandler : IRequestHandler<GetDocumentQuery, DocumentDetailResult?>
{
    private readonly IDocumentIntelligenceService _service;

    public GetDocumentHandler(IDocumentIntelligenceService service) => _service = service;

    public async Task<DocumentDetailResult?> Handle(GetDocumentQuery request, CancellationToken cancellationToken)
    {
        return await _service.GetDocumentByIdAsync(request.DocumentId);
    }
}
