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
    /// Average cosine similarity of retrieved chunks to the query embedding.
    /// Measures retrieval relevance (0.0-1.0), NOT answer correctness confidence.
    /// </summary>
    public double Confidence { get; set; }

    public List<DocumentCitation> Citations { get; set; } = [];
    public string LlmProvider { get; set; } = string.Empty;
    public long ElapsedMilliseconds { get; set; }
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
