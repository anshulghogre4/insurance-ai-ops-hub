namespace SentimentAnalyzer.API.Services.Documents;

/// <summary>
/// Splits extracted document text into chunks optimized for RAG retrieval.
/// Insurance-aware: detects section headers (DECLARATIONS, COVERAGE, EXCLUSIONS,
/// CONDITIONS, ENDORSEMENTS) and preserves section boundaries.
/// </summary>
public interface IDocumentChunkingService
{
    /// <summary>Chunks document text using insurance-section-aware splitting.</summary>
    List<DocumentChunk> ChunkDocument(string text, int targetTokens = 512, int overlapTokens = 64);
}

/// <summary>A single chunk extracted from a document.</summary>
public class DocumentChunk
{
    public int Index { get; set; }
    public string SectionName { get; set; } = "GENERAL";
    public string Content { get; set; } = string.Empty;
    public int ApproximateTokens { get; set; }
}
