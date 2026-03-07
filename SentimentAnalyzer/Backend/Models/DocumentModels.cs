namespace SentimentAnalyzer.API.Models;

/// <summary>Upload result returned after document processing.</summary>
public class DocumentUploadResult
{
    public int DocumentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int PageCount { get; set; }
    public int ChunkCount { get; set; }
    public string EmbeddingProvider { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

/// <summary>Query result with LLM answer and source citations.</summary>
public class DocumentQueryResult
{
    public string Answer { get; set; } = string.Empty;

    /// <summary>
    /// Normalized retrieval relevance score (0.0-1.0).
    /// Average of fused RRF scores normalized to 0-1 scale (top result = 1.0, others proportional).
    /// Measures retrieval quality, NOT answer correctness confidence.
    /// </summary>
    public double Confidence { get; set; }

    public List<DocumentCitation> Citations { get; set; } = [];
    public string LlmProvider { get; set; } = string.Empty;
    public long ElapsedMilliseconds { get; set; }

    /// <summary>Content safety screening result for the LLM answer (null if screening was skipped).</summary>
    public ContentSafetyInfo? AnswerSafety { get; set; }

    /// <summary>Whether the query was reformulated for better retrieval (null if reformulation was skipped).</summary>
    public bool? QueryReformulated { get; set; }

    /// <summary>Answer quality score from the evaluator (0.0-1.0, null if evaluation was skipped).</summary>
    public double? AnswerQualityScore { get; set; }

    /// <summary>Whether the answer is grounded in source documents (null if evaluation was skipped).</summary>
    public bool? IsGrounded { get; set; }

    /// <summary>Cross-document conflicts found during synthesis (empty if single-document or skipped).</summary>
    public List<string> CrossDocConflicts { get; set; } = [];
}

/// <summary>A citation pointing to a specific document chunk.</summary>
public class DocumentCitation
{
    public int DocumentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public string RelevantText { get; set; } = string.Empty;
    public double Similarity { get; set; }
}

/// <summary>Document detail for GET by ID.</summary>
public class DocumentDetailResult
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int PageCount { get; set; }
    public int ChunkCount { get; set; }
    public string EmbeddingProvider { get; set; } = string.Empty;
    public List<ChunkSummary> Chunks { get; set; } = [];
    public DateTime CreatedAt { get; set; }
}

/// <summary>Chunk summary for document detail.</summary>
public class ChunkSummary
{
    public int ChunkIndex { get; set; }
    public string SectionName { get; set; } = string.Empty;
    public int TokenCount { get; set; }
    public string ContentPreview { get; set; } = string.Empty;
    public int? PageNumber { get; set; }
    public int? ParentChunkId { get; set; }
    public int ChunkLevel { get; set; }

    /// <summary>Whether this chunk passed content safety screening.</summary>
    public bool IsSafe { get; set; } = true;

    /// <summary>Pipe-separated safety flags when unsafe (e.g., "Hate|Violence"). Null when safe.</summary>
    public string? SafetyFlags { get; set; }
}

/// <summary>Document list summary.</summary>
public class DocumentSummary
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int PageCount { get; set; }
    public int ChunkCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Request to query documents.</summary>
public class DocumentQueryRequest
{
    public string Question { get; set; } = string.Empty;
    public int? DocumentId { get; set; }
}

/// <summary>SSE progress event during document upload processing.</summary>
public class DocumentProgressEvent
{
    /// <summary>Phase: Uploading, OCR, Chunking, Embedding, Safety, Done, Error.</summary>
    public string Phase { get; set; } = string.Empty;
    /// <summary>Progress percentage 0-100.</summary>
    public int Progress { get; set; }
    /// <summary>Human-readable status message.</summary>
    public string Message { get; set; } = string.Empty;
    /// <summary>Final result (populated only on Done phase).</summary>
    public DocumentUploadResult? Result { get; set; }
    /// <summary>Error detail (populated only on Error phase).</summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>Simplified content safety info for API responses.</summary>
public class ContentSafetyInfo
{
    public bool IsSafe { get; set; } = true;
    public List<string> FlaggedCategories { get; set; } = [];
    public string Provider { get; set; } = string.Empty;
}

/// <summary>
/// Result of synthetic Q&amp;A generation for a document.
/// Used to prepare fine-tuning training data from indexed insurance documents.
/// </summary>
public class SyntheticQAResult
{
    public int DocumentId { get; set; }
    public string DocumentName { get; set; } = string.Empty;
    public int TotalPairsGenerated { get; set; }
    public List<QAPair> Pairs { get; set; } = [];
    public string LlmProvider { get; set; } = string.Empty;
    public long ElapsedMilliseconds { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// A single synthetic Q&amp;A pair for fine-tuning training data.
/// Maps to a specific document chunk for source traceability.
/// </summary>
public class QAPair
{
    public int Id { get; set; }
    public int ChunkId { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    /// <summary>Q&amp;A category: factual, inferential, or procedural.</summary>
    public string Category { get; set; } = "factual";
    /// <summary>Confidence score (0.0-1.0) from the generating LLM.</summary>
    public double Confidence { get; set; }
    /// <summary>Section name from the source chunk (e.g., DECLARATIONS, COVERAGE).</summary>
    public string SectionName { get; set; } = string.Empty;
}
