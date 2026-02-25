using System.ComponentModel.DataAnnotations;

namespace SentimentAnalyzer.API.Data.Entities;

/// <summary>
/// EF Core entity representing an uploaded insurance document.
/// Stores document metadata and OCR-extracted text (PII-redacted).
/// </summary>
public class DocumentRecord
{
    [Key]
    public int Id { get; set; }

    /// <summary>Original filename of the uploaded document.</summary>
    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>MIME type (e.g., application/pdf, image/png).</summary>
    [MaxLength(50)]
    public string MimeType { get; set; } = string.Empty;

    /// <summary>Document category: Policy, Claim, Endorsement, Correspondence, Other.</summary>
    [MaxLength(30)]
    public string Category { get; set; } = "Other";

    /// <summary>Full OCR-extracted text (PII-redacted).</summary>
    public string ExtractedText { get; set; } = string.Empty;

    /// <summary>Number of pages extracted by OCR.</summary>
    public int PageCount { get; set; }

    /// <summary>Total number of chunks generated from this document.</summary>
    public int ChunkCount { get; set; }

    /// <summary>Embedding provider used (Voyage, Ollama).</summary>
    [MaxLength(50)]
    public string EmbeddingProvider { get; set; } = string.Empty;

    /// <summary>Embedding dimensionality (1024 for Voyage/mxbai, 768 for nomic).</summary>
    public int EmbeddingDimensions { get; set; }

    /// <summary>Processing status: Uploading, Processing, Ready, Failed.</summary>
    [MaxLength(20)]
    public string Status { get; set; } = "Uploading";

    /// <summary>Error message if processing failed.</summary>
    [MaxLength(1000)]
    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Navigation property for document chunks.</summary>
    public List<DocumentChunkRecord> Chunks { get; set; } = [];
}
