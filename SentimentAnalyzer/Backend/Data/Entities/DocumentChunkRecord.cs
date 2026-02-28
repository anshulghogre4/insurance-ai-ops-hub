using System.ComponentModel.DataAnnotations;

namespace SentimentAnalyzer.API.Data.Entities;

/// <summary>
/// EF Core entity representing a chunk of an insurance document with its embedding.
/// Embeddings stored as JSON-serialized float arrays for SQLite compatibility.
/// Production: Supabase pgvector replaces JSON storage.
/// </summary>
public class DocumentChunkRecord
{
    [Key]
    public int Id { get; set; }

    /// <summary>Foreign key to the parent document.</summary>
    public int DocumentId { get; set; }

    /// <summary>Chunk index within the document (0-based).</summary>
    public int ChunkIndex { get; set; }

    /// <summary>Insurance section header this chunk belongs to (DECLARATIONS, COVERAGE, etc.).</summary>
    [MaxLength(50)]
    public string SectionName { get; set; } = "GENERAL";

    /// <summary>The chunk text content (PII-redacted, max ~512 tokens).</summary>
    [MaxLength(3000)]
    public string Content { get; set; } = string.Empty;

    /// <summary>Approximate token count for this chunk.</summary>
    public int TokenCount { get; set; }

    /// <summary>
    /// JSON-serialized embedding vector (float[]).
    /// SQLite stores as TEXT; PostgreSQL can use pgvector.
    /// </summary>
    public string EmbeddingJson { get; set; } = "[]";

    /// <summary>Page number this chunk originates from (1-based).</summary>
    public int? PageNumber { get; set; }

    /// <summary>ID of parent chunk for hierarchical retrieval (null = top-level).</summary>
    public int? ParentChunkId { get; set; }

    /// <summary>Chunk level: 0 = section parent, 1 = paragraph child.</summary>
    public int ChunkLevel { get; set; }

    /// <summary>Whether this chunk passed content safety screening (default true, updated after screening).</summary>
    public bool IsSafe { get; set; } = true;

    /// <summary>Pipe-separated safety flags when unsafe (e.g., "Hate|Violence"). Null when safe.</summary>
    public string? SafetyFlags { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation property to parent document.</summary>
    public DocumentRecord? Document { get; set; }
}
