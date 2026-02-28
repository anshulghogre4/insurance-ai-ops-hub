using System.ComponentModel.DataAnnotations;

namespace SentimentAnalyzer.API.Data.Entities;

/// <summary>
/// Represents a synthetic Q&amp;A pair generated from a document chunk for fine-tuning preparation.
/// Each pair links to a specific document and chunk, enabling traceability from training data back to source.
/// </summary>
public class DocumentQAPairRecord
{
    [Key]
    public int Id { get; set; }

    /// <summary>Foreign key to the parent document.</summary>
    public int DocumentId { get; set; }

    /// <summary>Foreign key to the source chunk this Q&amp;A was generated from.</summary>
    public int ChunkId { get; set; }

    /// <summary>The synthetic question generated from the chunk content.</summary>
    [MaxLength(2000)]
    public string Question { get; set; } = string.Empty;

    /// <summary>The synthetic answer generated from the chunk content.</summary>
    [MaxLength(4000)]
    public string Answer { get; set; } = string.Empty;

    /// <summary>Q&amp;A category: factual, inferential, or procedural.</summary>
    [MaxLength(30)]
    public string Category { get; set; } = "factual";

    /// <summary>Confidence score (0.0-1.0) from the generating LLM.</summary>
    public double Confidence { get; set; }

    /// <summary>LLM provider that generated this Q&amp;A pair (e.g., "Groq", "Cerebras").</summary>
    [MaxLength(50)]
    public string LlmProvider { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation property to parent document.</summary>
    public DocumentRecord Document { get; set; } = null!;

    /// <summary>Navigation property to source chunk.</summary>
    public DocumentChunkRecord Chunk { get; set; } = null!;
}
