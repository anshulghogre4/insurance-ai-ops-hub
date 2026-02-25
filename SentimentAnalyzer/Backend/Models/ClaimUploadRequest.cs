namespace SentimentAnalyzer.API.Models;

/// <summary>
/// Request model for multipart evidence upload.
/// Used to bind form fields alongside the file in the upload endpoint.
/// </summary>
public class ClaimUploadRequest
{
    /// <summary>The claim ID to attach this evidence to.</summary>
    public int ClaimId { get; set; }

    /// <summary>The uploaded file (image, audio, or PDF).</summary>
    public IFormFile File { get; set; } = null!;
}
