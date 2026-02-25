using SentimentAnalyzer.API.Models;

namespace SentimentAnalyzer.API.Services.Claims;

/// <summary>
/// Routes multimodal evidence (images, audio, PDFs) to the appropriate
/// processing service and extracts entities from the output.
/// </summary>
public interface IMultimodalEvidenceProcessor
{
    /// <summary>
    /// Processes a file attachment: routes by MIME type to vision/STT/OCR,
    /// runs NER on output text, persists evidence record, and returns result.
    /// </summary>
    Task<ClaimEvidenceResponse> ProcessAsync(int claimId, byte[] fileData, string mimeType, string fileName);
}
