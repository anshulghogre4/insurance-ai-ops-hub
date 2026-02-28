namespace SentimentAnalyzer.API.Services.Multimodal;

/// <summary>
/// Translates text between languages for multilingual claims processing.
/// Insurance use case: translating non-English policyholder communications,
/// claims descriptions, and policy documents to English for AI analysis.
/// </summary>
public interface ITranslationService
{
    /// <summary>
    /// Translates text to the target language (default: English).
    /// </summary>
    Task<TranslationResult> TranslateAsync(
        string text,
        string targetLanguage = "en",
        string? sourceLanguage = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Detects the language of the input text.
    /// </summary>
    Task<LanguageDetectionResult> DetectLanguageAsync(
        string text,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a translation operation.
/// </summary>
public class TranslationResult
{
    /// <summary>Whether the translation succeeded.</summary>
    public bool IsSuccess { get; set; }

    /// <summary>The translated text.</summary>
    public string TranslatedText { get; set; } = string.Empty;

    /// <summary>Detected source language code (e.g., "es", "fr").</summary>
    public string DetectedSourceLanguage { get; set; } = string.Empty;

    /// <summary>Confidence of source language detection (0.0-1.0).</summary>
    public double Confidence { get; set; }

    /// <summary>Provider that performed the translation.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Error message if translation failed.</summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Result of a language detection operation.
/// </summary>
public class LanguageDetectionResult
{
    /// <summary>Whether the detection succeeded.</summary>
    public bool IsSuccess { get; set; }

    /// <summary>Detected language code (e.g., "en", "es", "fr").</summary>
    public string DetectedLanguage { get; set; } = string.Empty;

    /// <summary>Human-readable language name (e.g., "English", "Spanish").</summary>
    public string LanguageName { get; set; } = string.Empty;

    /// <summary>Confidence of detection (0.0-1.0).</summary>
    public double Confidence { get; set; }

    /// <summary>Provider that performed the detection.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Error message if detection failed.</summary>
    public string? ErrorMessage { get; set; }
}
