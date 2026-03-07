using SentimentAnalyzer.API.Models;

namespace SentimentAnalyzer.API.Services.Documents.RAGQuery;

/// <summary>
/// Synthesizes answers across multiple documents when a query spans several sources.
/// Resolves conflicts between document provisions and highlights cross-document references.
/// </summary>
public interface ICrossDocReasoner
{
    /// <summary>
    /// Synthesizes information from multiple documents into a coherent answer.
    /// Identifies agreements, conflicts, and gaps across document sources.
    /// </summary>
    Task<CrossDocReasoningResult> SynthesizeAsync(
        string question,
        List<DocumentCitation> citations,
        List<string> sourceChunks,
        CancellationToken cancellationToken = default);
}

/// <summary>Result of cross-document reasoning synthesis.</summary>
public class CrossDocReasoningResult
{
    /// <summary>Synthesized answer incorporating all relevant documents.</summary>
    public string SynthesizedAnswer { get; set; } = string.Empty;

    /// <summary>Number of unique documents referenced.</summary>
    public int DocumentCount { get; set; }

    /// <summary>Conflicts or contradictions found between documents.</summary>
    public List<string> Conflicts { get; set; } = [];

    /// <summary>Whether cross-document synthesis was performed (vs. single-doc passthrough).</summary>
    public bool WasSynthesized { get; set; }
}
