using System.ComponentModel.DataAnnotations;

namespace SentimentAnalyzer.API.Data.Entities;

/// <summary>
/// EF Core entity representing processed multimodal evidence for a claim.
/// Each record represents one piece of evidence (photo, audio, document)
/// processed by a multimodal service.
/// </summary>
public class ClaimEvidenceRecord
{
    [Key]
    public int Id { get; set; }

    /// <summary>Foreign key to the parent claim.</summary>
    public int ClaimId { get; set; }

    /// <summary>Type of evidence: image, audio, document.</summary>
    [MaxLength(20)]
    public string EvidenceType { get; set; } = string.Empty;

    /// <summary>MIME type of the uploaded file (e.g., image/jpeg, audio/wav, application/pdf).</summary>
    [MaxLength(50)]
    public string MimeType { get; set; } = string.Empty;

    /// <summary>Multimodal service that processed this evidence (e.g., AzureVision, Deepgram, OcrSpace).</summary>
    [MaxLength(30)]
    public string Provider { get; set; } = string.Empty;

    /// <summary>Text output from the multimodal service (transcription, OCR text, image description).</summary>
    [MaxLength(20000)]
    public string ProcessedText { get; set; } = string.Empty;

    /// <summary>JSON-serialized list of damage indicators detected (from vision services).</summary>
    [MaxLength(2000)]
    public string DamageIndicatorsJson { get; set; } = "[]";

    /// <summary>JSON-serialized list of extracted entities (from NER). PII-category values are redacted.</summary>
    [MaxLength(5000)]
    public string EntitiesJson { get; set; } = "[]";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation property to parent claim.</summary>
    public ClaimRecord? Claim { get; set; }
}
