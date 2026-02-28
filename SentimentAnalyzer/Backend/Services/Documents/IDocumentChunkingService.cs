namespace SentimentAnalyzer.API.Services.Documents;

/// <summary>
/// Splits extracted document text into chunks optimized for RAG retrieval.
/// Insurance-aware: detects section headers (DECLARATIONS, COVERAGE, EXCLUSIONS,
/// CONDITIONS, ENDORSEMENTS) and preserves section boundaries.
/// </summary>
public interface IDocumentChunkingService
{
    /// <summary>Chunks document text using insurance-section-aware splitting.</summary>
    List<DocumentChunk> ChunkDocument(string text, int targetTokens = 512, int overlapTokens = 128);
}

/// <summary>A single chunk extracted from a document.</summary>
public class DocumentChunk
{
    public int Index { get; set; }
    public string SectionName { get; set; } = "GENERAL";
    public string Content { get; set; } = string.Empty;
    public int ApproximateTokens { get; set; }
    /// <summary>Page number where this chunk starts (1-based).</summary>
    public int? PageNumber { get; set; }
    /// <summary>Index of parent chunk for hierarchical retrieval.</summary>
    public int? ParentChunkIndex { get; set; }
    /// <summary>0 = section-level parent, 1 = paragraph child.</summary>
    public int ChunkLevel { get; set; }
}
