using SentimentAnalyzer.API.Models;

namespace SentimentAnalyzer.API.Services.Documents;

/// <summary>
/// RAG facade for insurance document intelligence.
/// Upload: OCR -> PII redact -> chunk -> embed -> store.
/// Query: embed question -> vector search top-5 -> LLM answer with citations.
/// </summary>
public interface IDocumentIntelligenceService
{
    Task<DocumentUploadResult> UploadAsync(
        byte[] fileData, string mimeType, string fileName,
        string category = "Other", CancellationToken cancellationToken = default);

    Task<DocumentQueryResult> QueryAsync(
        string question, int? documentId = null, CancellationToken cancellationToken = default);

    Task<DocumentDetailResult?> GetDocumentByIdAsync(int documentId);

    Task<(List<DocumentSummary> Items, int TotalCount)> GetDocumentsAsync(
        string? category = null, int pageSize = 20, int page = 1);
}
