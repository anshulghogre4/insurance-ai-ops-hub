using MediatR;
using SentimentAnalyzer.API.Models;
using SentimentAnalyzer.API.Services.Documents;

namespace SentimentAnalyzer.API.Features.Documents.Commands;

public record QueryDocumentCommand(string Question, int? DocumentId = null) : IRequest<DocumentQueryResult>;

public class QueryDocumentHandler : IRequestHandler<QueryDocumentCommand, DocumentQueryResult>
{
    private readonly IDocumentIntelligenceService _service;

    public QueryDocumentHandler(IDocumentIntelligenceService service) => _service = service;

    public async Task<DocumentQueryResult> Handle(QueryDocumentCommand request, CancellationToken cancellationToken)
    {
        return await _service.QueryAsync(request.Question, request.DocumentId, cancellationToken);
    }
}
