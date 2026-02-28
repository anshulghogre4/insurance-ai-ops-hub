using SentimentAnalyzer.API.Models;

namespace SentimentAnalyzer.API.Services.Documents;

/// <summary>
/// Service for generating synthetic Q&amp;A pairs from indexed document chunks.
/// Used to prepare fine-tuning training data for insurance domain LLMs.
/// Generates factual, inferential, and procedural question-answer pairs.
/// </summary>
public interface ISyntheticQAService
{
    /// <summary>
    /// Generate synthetic Q&amp;A pairs from all chunks of a document.
    /// Uses the resilient LLM provider chain to create diverse question types.
    /// </summary>
    /// <param name="documentId">ID of the indexed document to generate Q&amp;A from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing generated Q&amp;A pairs with metadata.</returns>
    Task<SyntheticQAResult> GenerateQAPairsAsync(int documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieve previously generated Q&amp;A pairs for a document.
    /// </summary>
    /// <param name="documentId">ID of the document to retrieve Q&amp;A pairs for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing stored Q&amp;A pairs.</returns>
    Task<SyntheticQAResult> GetQAPairsAsync(int documentId, CancellationToken cancellationToken = default);
}
