using System.Text.RegularExpressions;

namespace SentimentAnalyzer.API.Services.Documents;

/// <summary>
/// Insurance-aware document chunking service.
/// Phase 1: Split by insurance section headers (DECLARATIONS, COVERAGE, EXCLUSIONS, CONDITIONS, ENDORSEMENTS).
/// Phase 2: Split oversized sections at sentence boundaries with token overlap.
/// Target chunk size: 512 tokens (estimated at chars/4).
/// </summary>
public partial class InsuranceDocumentChunkingService : IDocumentChunkingService
{
    private readonly ILogger<InsuranceDocumentChunkingService> _logger;

    public InsuranceDocumentChunkingService(ILogger<InsuranceDocumentChunkingService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public List<DocumentChunk> ChunkDocument(string text, int targetTokens = 512, int overlapTokens = 64)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var targetChars = targetTokens * 4; // ~4 chars per token heuristic
        var overlapChars = overlapTokens * 4;

        // Phase 1: Split by insurance section headers
        var sections = SplitBySections(text);
        _logger.LogInformation("Document split into {Count} sections: {Sections}",
            sections.Count, string.Join(", ", sections.Select(s => s.Name)));

        // Phase 2: Split oversized sections at sentence boundaries
        var chunks = new List<DocumentChunk>();
        var chunkIndex = 0;

        foreach (var section in sections)
        {
            var sectionChunks = SplitSectionIntoChunks(section.Content, targetChars, overlapChars);
            foreach (var chunkText in sectionChunks)
            {
                chunks.Add(new DocumentChunk
                {
                    Index = chunkIndex++,
                    SectionName = section.Name,
                    Content = chunkText.Trim(),
                    ApproximateTokens = chunkText.Length / 4
                });
            }
        }

        _logger.LogInformation("Document chunked into {Count} chunks (target: {Target} tokens, overlap: {Overlap} tokens)",
            chunks.Count, targetTokens, overlapTokens);

        return chunks;
    }

    /// <summary>Splits text by insurance section headers.</summary>
    private static List<(string Name, string Content)> SplitBySections(string text)
    {
        var sections = new List<(string Name, string Content)>();
        var matches = SectionHeaderRegex().Matches(text);

        if (matches.Count == 0)
        {
            sections.Add(("GENERAL", text));
            return sections;
        }

        // Content before the first header
        var firstMatchStart = matches[0].Index;
        if (firstMatchStart > 0)
        {
            var preamble = text[..firstMatchStart].Trim();
            if (preamble.Length > 0)
                sections.Add(("GENERAL", preamble));
        }

        // Each header defines a section until the next header
        for (var i = 0; i < matches.Count; i++)
        {
            var sectionName = NormalizeSectionName(matches[i].Groups[1].Value);
            var start = matches[i].Index + matches[i].Length;
            var end = i + 1 < matches.Count ? matches[i + 1].Index : text.Length;
            var content = text[start..end].Trim();
            if (content.Length > 0)
                sections.Add((sectionName, content));
        }

        return sections;
    }

    /// <summary>Splits a section into chunks at sentence boundaries with overlap.</summary>
    private static List<string> SplitSectionIntoChunks(string text, int targetChars, int overlapChars)
    {
        if (text.Length <= targetChars)
            return [text];

        var chunks = new List<string>();
        var sentences = SentenceBoundaryRegex().Split(text)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        var currentChunk = "";
        var overlapBuffer = "";

        foreach (var sentence in sentences)
        {
            if (currentChunk.Length + sentence.Length > targetChars && currentChunk.Length > 0)
            {
                chunks.Add(currentChunk.Trim());
                // Start next chunk with overlap from the end of the current chunk
                currentChunk = overlapBuffer + sentence;
            }
            else
            {
                currentChunk += sentence;
            }

            // Track the last overlapChars of text for overlap
            if (currentChunk.Length > overlapChars)
                overlapBuffer = currentChunk[^overlapChars..];
            else
                overlapBuffer = currentChunk;
        }

        if (currentChunk.Trim().Length > 0)
            chunks.Add(currentChunk.Trim());

        return chunks;
    }

    /// <summary>Normalizes varied section header text to standard names.</summary>
    private static string NormalizeSectionName(string header)
    {
        var upper = header.Trim().ToUpperInvariant();
        return upper switch
        {
            _ when upper.Contains("DECLARATION") || upper.Contains("DEC PAGE") => "DECLARATIONS",
            _ when upper.Contains("COVERAGE") || upper.Contains("INSURING") || upper.Contains("COVERED PERIL") => "COVERAGE",
            _ when upper.Contains("EXCLUSION") || upper.Contains("NOT COVERED") || upper.Contains("LIMITATION") => "EXCLUSIONS",
            _ when upper.Contains("CONDITION") => "CONDITIONS",
            _ when upper.Contains("ENDORSEMENT") || upper.Contains("RIDER") || upper.Contains("AMENDMENT") || upper.Contains("SCHEDULE") => "ENDORSEMENTS",
            _ when upper.Contains("DEFINITION") => "DEFINITIONS",
            _ => upper.Length > 50 ? upper[..50] : upper
        };
    }

    /// <summary>Regex for insurance section headers.</summary>
    [GeneratedRegex(
        @"(?:^|\n)\s*(?:SECTION\s+\w+[:\s]*)?(" +
        @"DECLARATIONS?\s*(?:PAGE)?|DEC\s+PAGE|" +
        @"COVERAGES?|INSURING\s+AGREEMENTS?|COVERED\s+PERILS?|" +
        @"EXCLUSIONS?|WHAT\s+IS\s+NOT\s+COVERED|LIMITATIONS?|" +
        @"CONDITIONS?|GENERAL\s+CONDITIONS?|POLICY\s+CONDITIONS?|" +
        @"ENDORSEMENTS?|RIDERS?|AMENDMENTS?|SCHEDULES?|" +
        @"DEFINITIONS?" +
        @")\s*(?:[:\-]|\n)",
        RegexOptions.IgnoreCase)]
    private static partial Regex SectionHeaderRegex();

    /// <summary>
    /// Regex for sentence boundaries. Negative lookbehind excludes common abbreviations
    /// found in insurance documents (Dr., No., Sec., Inc., Corp., Ltd., etc.).
    /// </summary>
    [GeneratedRegex(@"(?<!(?:Dr|Mr|Mrs|Ms|Jr|Sr|No|Sec|Art|Inc|Corp|Ltd|Dept|Est|Approx|Excl|Incl|Cert|Ins|Ref|Fig|St|Ave|Blvd|i\.e|e\.g))\.\s+|(?<=[!?])\s+")]
    private static partial Regex SentenceBoundaryRegex();
}
