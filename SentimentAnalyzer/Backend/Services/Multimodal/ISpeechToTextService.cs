namespace SentimentAnalyzer.API.Services.Multimodal;

/// <summary>
/// Transcribes audio input to text using an external speech-to-text provider.
/// Insurance use case: field adjusters dictating claims via voice.
/// </summary>
public interface ISpeechToTextService
{
    /// <summary>
    /// Transcribes audio bytes to text.
    /// </summary>
    /// <param name="audioData">Raw audio bytes (WAV, MP3, or WebM).</param>
    /// <param name="mimeType">MIME type of the audio (e.g., "audio/wav", "audio/webm").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Transcription result with text and confidence.</returns>
    Task<TranscriptionResult> TranscribeAsync(
        byte[] audioData,
        string mimeType = "audio/wav",
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a speech-to-text transcription.
/// </summary>
public class TranscriptionResult
{
    /// <summary>Transcribed text.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Confidence score (0.0 to 1.0).</summary>
    public double Confidence { get; set; }

    /// <summary>Duration of the audio in seconds.</summary>
    public double DurationSeconds { get; set; }

    /// <summary>Provider that performed the transcription.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Whether the transcription succeeded.</summary>
    public bool IsSuccess { get; set; }

    /// <summary>Error message if transcription failed.</summary>
    public string? ErrorMessage { get; set; }
}
