namespace SentimentAnalyzer.API.Services.Multimodal;

/// <summary>
/// Extracts named entities from text using NLP models.
/// Insurance use case: extracting policy numbers, claim numbers, names, dates,
/// and monetary amounts from claim descriptions and policy documents.
/// </summary>
public interface IEntityExtractionService
{
    /// <summary>
    /// Extracts named entities from the given text.
    /// </summary>
    /// <param name="text">The text to extract entities from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Extraction result with typed entities.</returns>
    Task<EntityExtractionResult> ExtractEntitiesAsync(
        string text,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a named entity extraction operation.
/// </summary>
public class EntityExtractionResult
{
    /// <summary>Whether the extraction succeeded.</summary>
    public bool IsSuccess { get; set; }

    /// <summary>List of extracted entities.</summary>
    public List<ExtractedEntity> Entities { get; set; } = [];

    /// <summary>Provider that performed the extraction.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Error message if extraction failed.</summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// A single named entity extracted from text.
/// </summary>
public class ExtractedEntity
{
    /// <summary>Entity type (e.g., "PERSON", "ORGANIZATION", "DATE", "MONEY", "LOCATION").</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>The extracted entity value.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Confidence score for this entity (0.0 to 1.0).</summary>
    public double Confidence { get; set; }

    /// <summary>Start character index in the source text.</summary>
    public int StartIndex { get; set; }

    /// <summary>End character index in the source text.</summary>
    public int EndIndex { get; set; }
}
