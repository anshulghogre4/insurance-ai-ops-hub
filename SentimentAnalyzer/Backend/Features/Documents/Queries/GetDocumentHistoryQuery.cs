using MediatR;
using SentimentAnalyzer.API.Models;
using SentimentAnalyzer.API.Services.Documents;

namespace SentimentAnalyzer.API.Features.Documents.Queries;

public record GetDocumentHistoryQuery(
    string? Category = null, int PageSize = 20, int Page = 1)
    : IRequest<PaginatedResponse<DocumentSummary>>;

public class GetDocumentHistoryHandler
    : IRequestHandler<GetDocumentHistoryQuery, PaginatedResponse<DocumentSummary>>
{
    private readonly IDocumentIntelligenceService _service;

    public GetDocumentHistoryHandler(IDocumentIntelligenceService service) => _service = service;

    public async Task<PaginatedResponse<DocumentSummary>> Handle(
        GetDocumentHistoryQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _service.GetDocumentsAsync(
            request.Category, request.PageSize, request.Page);

        return new PaginatedResponse<DocumentSummary>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
