using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SentimentAnalyzer.API.Data.Entities;
using SentimentAnalyzer.API.Services.Embeddings;

namespace SentimentAnalyzer.API.Data;

/// <summary>
/// SQLite implementation of IDocumentRepository for RAG document storage.
/// Vector similarity search is computed in-memory using SIMD cosine similarity.
/// Production: Supabase pgvector replaces in-memory search.
/// </summary>
public class SqliteDocumentRepository : IDocumentRepository
{
    private readonly InsuranceAnalysisDbContext _db;
    private readonly ILogger<SqliteDocumentRepository> _logger;

    public SqliteDocumentRepository(InsuranceAnalysisDbContext db, ILogger<SqliteDocumentRepository> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DocumentRecord> SaveDocumentAsync(DocumentRecord document)
    {
        _db.Documents.Add(document);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Saved document {Id}: {FileName}", document.Id, document.FileName);
        return document;
    }

    public async Task UpdateDocumentAsync(DocumentRecord document)
    {
        document.UpdatedAt = DateTime.UtcNow;
        _db.Documents.Update(document);
        await _db.SaveChangesAsync();
    }

    public async Task<DocumentRecord?> GetDocumentByIdAsync(int documentId)
    {
        return await _db.Documents
            .Include(d => d.Chunks)
            .FirstOrDefaultAsync(d => d.Id == documentId);
    }

    public async Task<(List<DocumentRecord> Items, int TotalCount)> GetDocumentsAsync(
        string? category = null, string? status = null, int pageSize = 20, int page = 1)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);

        var query = _db.Documents.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(d => d.Category == category);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(d => d.Status == status);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task SaveChunksAsync(IEnumerable<DocumentChunkRecord> chunks)
    {
        _db.DocumentChunks.AddRange(chunks);
        await _db.SaveChangesAsync();
    }

    public async Task<List<DocumentChunkRecord>> GetChunksByDocumentIdAsync(int documentId)
    {
        return await _db.DocumentChunks
            .Where(c => c.DocumentId == documentId)
            .OrderBy(c => c.ChunkIndex)
            .ToListAsync();
    }

    public async Task<List<(DocumentChunkRecord Chunk, double Similarity)>> SearchSimilarChunksAsync(
        float[] queryEmbedding, int topK = 5, int? documentId = null)
    {
        // Load chunks from "Ready" documents only (O(N) — fine for <10K chunks in dev)
        var chunksQuery = _db.DocumentChunks
            .Include(c => c.Document)
            .Where(c => c.Document!.Status == "Ready")
            .AsNoTracking()
            .AsQueryable();

        if (documentId.HasValue)
            chunksQuery = chunksQuery.Where(c => c.DocumentId == documentId.Value);

        var chunks = await chunksQuery.ToListAsync();

        _logger.LogInformation("Vector search across {Count} chunks for top-{TopK}", chunks.Count, topK);

        // Compute cosine similarity for each chunk
        var results = new List<(DocumentChunkRecord Chunk, double Similarity)>();
        foreach (var chunk in chunks)
        {
            try
            {
                var embedding = JsonSerializer.Deserialize<float[]>(chunk.EmbeddingJson);
                if (embedding == null || embedding.Length == 0) continue;

                var similarity = CosineSimilarity.Compute(queryEmbedding, embedding);
                results.Add((chunk, similarity));
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize embedding for chunk {ChunkId}", chunk.Id);
            }
        }

        return results
            .OrderByDescending(r => r.Similarity)
            .Take(topK)
            .ToList();
    }

    public async Task DeleteDocumentAsync(int documentId)
    {
        var document = await _db.Documents.FindAsync(documentId);
        if (document != null)
        {
            _db.Documents.Remove(document); // Cascade deletes chunks
            await _db.SaveChangesAsync();
            _logger.LogInformation("Deleted document {Id}", documentId);
        }
    }
}
