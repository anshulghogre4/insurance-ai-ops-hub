using System.Text.RegularExpressions;
using SentimentAnalyzer.API.Data.Entities;

namespace SentimentAnalyzer.API.Services.Documents;

/// <summary>
/// Pure C# BM25 implementation for sparse text retrieval.
/// No external dependencies — uses TF-IDF with BM25 k1=1.2, b=0.75 parameters.
/// Catches exact keyword matches (policy numbers, claim IDs, dates) that cosine
/// similarity on dense embeddings may miss.
/// </summary>
public static class BM25Scorer
{
    /// <summary>Term frequency saturation parameter. Higher values increase the effect of term frequency.</summary>
    private const double K1 = 1.2;

    /// <summary>Document length normalization parameter. 0 = no normalization, 1 = full normalization.</summary>
    private const double B = 0.75;

    /// <summary>
    /// Common English stopwords filtered from queries and documents during tokenization.
    /// Insurance-specific terms (policy, claim, coverage) are intentionally NOT stopwords.
    /// </summary>
    private static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "a", "an", "is", "are", "was", "were", "be", "been", "being",
        "have", "has", "had", "do", "does", "did", "will", "would", "shall", "should",
        "may", "might", "must", "can", "could", "am", "not", "no", "nor",
        "and", "but", "or", "yet", "so", "for", "if", "then", "else",
        "at", "by", "from", "in", "into", "of", "on", "to", "with", "as",
        "this", "that", "these", "those", "it", "its",
        "i", "me", "my", "we", "our", "you", "your", "he", "him", "his",
        "she", "her", "they", "them", "their", "what", "which", "who", "whom",
        "how", "when", "where", "why", "all", "each", "every", "both", "few",
        "more", "most", "other", "some", "such", "than", "too", "very",
        "just", "also", "about", "between", "through", "during", "before", "after"
    };

    /// <summary>
    /// Regex pattern for tokenization: splits on non-word characters.
    /// Preserves alphanumeric tokens including hyphens within identifiers (e.g., POL-12345).
    /// </summary>
    private static readonly Regex TokenizerRegex = new(@"[\w]+([-][\w]+)*", RegexOptions.Compiled);

    /// <summary>
    /// Scores a set of document chunks against a query using BM25 ranking.
    /// </summary>
    /// <param name="query">The search query string.</param>
    /// <param name="candidates">The document chunks to score.</param>
    /// <returns>List of chunks with their BM25 scores, sorted descending by score.</returns>
    public static List<(DocumentChunkRecord Chunk, double Score)> Score(
        string query, IReadOnlyList<DocumentChunkRecord> candidates)
    {
        if (string.IsNullOrWhiteSpace(query) || candidates.Count == 0)
        {
            return candidates.Select(c => (c, 0.0)).ToList();
        }

        // Tokenize query (filter stopwords)
        var queryTerms = Tokenize(query);
        if (queryTerms.Count == 0)
        {
            return candidates.Select(c => (c, 0.0)).ToList();
        }

        // Tokenize all candidate documents
        var documentTokens = new List<List<string>>(candidates.Count);
        var totalDocLength = 0.0;

        for (var i = 0; i < candidates.Count; i++)
        {
            var tokens = Tokenize(candidates[i].Content);
            documentTokens.Add(tokens);
            totalDocLength += tokens.Count;
        }

        var n = candidates.Count;
        var avgDl = totalDocLength / n;

        // Compute document frequency (df) for each query term
        var documentFrequency = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var term in queryTerms)
        {
            if (documentFrequency.ContainsKey(term))
                continue;

            var df = 0;
            for (var i = 0; i < n; i++)
            {
                if (documentTokens[i].Any(t => t.Equals(term, StringComparison.OrdinalIgnoreCase)))
                {
                    df++;
                }
            }
            documentFrequency[term] = df;
        }

        // Compute BM25 score for each candidate
        var results = new List<(DocumentChunkRecord Chunk, double Score)>(n);

        for (var i = 0; i < n; i++)
        {
            var docTokens = documentTokens[i];
            var dl = docTokens.Count;
            var score = 0.0;

            // Build term frequency map for this document
            var termFrequency = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var token in docTokens)
            {
                termFrequency.TryGetValue(token, out var count);
                termFrequency[token] = count + 1;
            }

            foreach (var term in queryTerms)
            {
                termFrequency.TryGetValue(term, out var tf);
                if (tf == 0) continue;

                var dfValue = documentFrequency.GetValueOrDefault(term, 0);

                // IDF: log((N - n + 0.5) / (n + 0.5) + 1)
                var idf = Math.Log((n - dfValue + 0.5) / (dfValue + 0.5) + 1.0);

                // BM25 term score: IDF * (tf * (k1 + 1)) / (tf + k1 * (1 - b + b * dl/avgdl))
                var numerator = tf * (K1 + 1.0);
                var denominator = tf + K1 * (1.0 - B + B * dl / avgDl);
                score += idf * (numerator / denominator);
            }

            results.Add((candidates[i], score));
        }

        return results.OrderByDescending(r => r.Score).ToList();
    }

    /// <summary>
    /// Tokenizes text into lowercase terms, filtering stopwords.
    /// Preserves hyphenated identifiers (e.g., POL-12345, CLM-2024-001).
    /// </summary>
    /// <param name="text">The text to tokenize.</param>
    /// <returns>List of lowercase, non-stopword tokens.</returns>
    internal static List<string> Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var matches = TokenizerRegex.Matches(text);
        var tokens = new List<string>(matches.Count);

        foreach (Match match in matches)
        {
            var token = match.Value.ToLowerInvariant();
            if (token.Length > 1 && !Stopwords.Contains(token))
            {
                tokens.Add(token);
            }
            // Single-character tokens are kept only if they are digits (e.g., insurance form codes)
            else if (token.Length == 1 && char.IsDigit(token[0]))
            {
                tokens.Add(token);
            }
        }

        return tokens;
    }
}
