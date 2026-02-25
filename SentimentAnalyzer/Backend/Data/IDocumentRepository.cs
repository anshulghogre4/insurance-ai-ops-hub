using SentimentAnalyzer.API.Data.Entities;

namespace SentimentAnalyzer.API.Data;

/// <summary>
/// Repository interface for document intelligence (RAG) data access.
/// Abstracts storage for documents, chunks, and vector similarity search.
/// </summary>
public interface IDocumentRepository
{
    Task<DocumentRecord> SaveDocumentAsync(DocumentRecord document);
    Task UpdateDocumentAsync(DocumentRecord document);
    Task<DocumentRecord?> GetDocumentByIdAsync(int documentId);
    Task<(List<DocumentRecord> Items, int TotalCount)> GetDocumentsAsync(
        string? category = null, string? status = null, int pageSize = 20, int page = 1);
    Task SaveChunksAsync(IEnumerable<DocumentChunkRecord> chunks);
    Task<List<DocumentChunkRecord>> GetChunksByDocumentIdAsync(int documentId);

    /// <summary>
    /// Performs vector similarity search across all document chunks.
    /// Loads embeddings from JSON, computes cosine similarity, returns top-K most similar.
    /// </summary>
    Task<List<(DocumentChunkRecord Chunk, double Similarity)>> SearchSimilarChunksAsync(
        float[] queryEmbedding, int topK = 5, int? documentId = null);

    Task DeleteDocumentAsync(int documentId);
}
