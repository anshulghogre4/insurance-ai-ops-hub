using MediatR;
using SentimentAnalyzer.API.Data;

namespace SentimentAnalyzer.API.Features.Documents.Commands;

public record DeleteDocumentCommand(int DocumentId) : IRequest;

public class DeleteDocumentHandler : IRequestHandler<DeleteDocumentCommand>
{
    private readonly IDocumentRepository _repository;

    public DeleteDocumentHandler(IDocumentRepository repository) => _repository = repository;

    public async Task Handle(DeleteDocumentCommand request, CancellationToken cancellationToken)
    {
        await _repository.DeleteDocumentAsync(request.DocumentId);
    }
}
