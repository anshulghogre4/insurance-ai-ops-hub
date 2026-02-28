using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SentimentAnalyzer.Agents.Orchestration;
using SentimentAnalyzer.API.Data;
using SentimentAnalyzer.API.Data.Entities;
using SentimentAnalyzer.API.Models;

namespace SentimentAnalyzer.API.Services.Documents;

/// <summary>
/// Generates synthetic Q&amp;A pairs from indexed document chunks using the resilient LLM provider chain.
/// Each chunk produces 2-3 question-answer pairs categorized as factual, inferential, or procedural.
/// PII is redacted before any external LLM call per security policy.
/// Results are persisted to DocumentQAPairs for fine-tuning export.
/// </summary>
public class SyntheticQAService : ISyntheticQAService
{
    private readonly InsuranceAnalysisDbContext _dbContext;
    private readonly IResilientKernelProvider _kernelProvider;
    private readonly IPIIRedactor _piiRedactor;
    private readonly ILogger<SyntheticQAService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    /// <summary>Regex to strip markdown code fences wrapping JSON output from LLMs.</summary>
    private static readonly Regex MarkdownFenceRegex = new(
        @"```(?:json|JSON)?\s*\n?",
        RegexOptions.Compiled);

    /// <summary>
    /// Initializes the Synthetic Q&amp;A generation service.
    /// </summary>
    /// <param name="dbContext">Database context for document and Q&amp;A pair persistence.</param>
    /// <param name="kernelProvider">Resilient kernel provider for LLM access with automatic fallback.</param>
    /// <param name="piiRedactor">PII redaction service - mandatory before external AI calls.</param>
    /// <param name="logger">Structured logger for this service.</param>
    public SyntheticQAService(
        InsuranceAnalysisDbContext dbContext,
        IResilientKernelProvider kernelProvider,
        IPIIRedactor piiRedactor,
        ILogger<SyntheticQAService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _kernelProvider = kernelProvider ?? throw new ArgumentNullException(nameof(kernelProvider));
        _piiRedactor = piiRedactor ?? throw new ArgumentNullException(nameof(piiRedactor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<SyntheticQAResult> GenerateQAPairsAsync(
        int documentId, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("Starting synthetic Q&A generation for document {DocumentId}", documentId);

        // Load document with chunks
        var document = await _dbContext.Documents
            .Include(d => d.Chunks)
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);

        if (document == null)
        {
            return new SyntheticQAResult
            {
                DocumentId = documentId,
                ErrorMessage = $"Document {documentId} not found.",
                ElapsedMilliseconds = sw.ElapsedMilliseconds
            };
        }

        if (document.Status != "Ready")
        {
            return new SyntheticQAResult
            {
                DocumentId = documentId,
                DocumentName = document.FileName,
                ErrorMessage = $"Document is not ready for Q&A generation. Current status: {document.Status}",
                ElapsedMilliseconds = sw.ElapsedMilliseconds
            };
        }

        // Filter chunks: skip empty content and parent-level (level 0) chunks that have children
        // Parent chunks are summary sections; child chunks (level 1) contain the actual detail content
        var eligibleChunks = document.Chunks
            .Where(c => !string.IsNullOrWhiteSpace(c.Content))
            .Where(c => c.ChunkLevel != 0 || !document.Chunks.Any(child => child.ParentChunkId == c.ChunkIndex))
            .OrderBy(c => c.ChunkIndex)
            .ToList();

        if (eligibleChunks.Count == 0)
        {
            return new SyntheticQAResult
            {
                DocumentId = documentId,
                DocumentName = document.FileName,
                ErrorMessage = "No eligible chunks found for Q&A generation.",
                ElapsedMilliseconds = sw.ElapsedMilliseconds
            };
        }

        // Remove existing pairs before regeneration to prevent duplicates
        var existingPairs = _dbContext.DocumentQAPairs
            .Where(p => p.DocumentId == documentId);
        _dbContext.DocumentQAPairs.RemoveRange(existingPairs);

        _logger.LogInformation(
            "Document {DocumentId} has {TotalChunks} chunks, {EligibleChunks} eligible for Q&A generation",
            documentId, document.Chunks.Count, eligibleChunks.Count);

        var allPairs = new List<DocumentQAPairRecord>();
        var failedChunks = 0;

        foreach (var chunk in eligibleChunks)
        {
            try
            {
                var pairs = await GenerateQAPairsForChunkAsync(
                    document, chunk, cancellationToken);
                allPairs.AddRange(pairs);
            }
            catch (Exception ex)
            {
                failedChunks++;
                _logger.LogWarning(ex,
                    "Failed to generate Q&A pairs for chunk {ChunkIndex} of document {DocumentId}",
                    chunk.ChunkIndex, documentId);
            }
        }

        // Persist all generated pairs
        if (allPairs.Count > 0)
        {
            _dbContext.DocumentQAPairs.AddRange(allPairs);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        sw.Stop();

        _logger.LogInformation(
            "Synthetic Q&A generation complete for document {DocumentId}: {PairCount} pairs from {ChunkCount} chunks ({FailedChunks} failed) in {Elapsed}ms",
            documentId, allPairs.Count, eligibleChunks.Count, failedChunks, sw.ElapsedMilliseconds);

        var result = new SyntheticQAResult
        {
            DocumentId = documentId,
            DocumentName = document.FileName,
            TotalPairsGenerated = allPairs.Count,
            Pairs = allPairs.Select(p => MapToQAPair(p, document.Chunks)).ToList(),
            LlmProvider = _kernelProvider.ActiveProviderName,
            ElapsedMilliseconds = sw.ElapsedMilliseconds,
            ErrorMessage = failedChunks > 0
                ? $"{failedChunks} of {eligibleChunks.Count} chunks failed during generation."
                : null
        };

        return result;
    }

    /// <inheritdoc />
    public async Task<SyntheticQAResult> GetQAPairsAsync(
        int documentId, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        var document = await _dbContext.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);

        if (document == null)
        {
            return new SyntheticQAResult
            {
                DocumentId = documentId,
                ErrorMessage = $"Document {documentId} not found.",
                ElapsedMilliseconds = sw.ElapsedMilliseconds
            };
        }

        var pairRecords = await _dbContext.DocumentQAPairs
            .Include(p => p.Chunk)
            .Where(p => p.DocumentId == documentId)
            .OrderBy(p => p.ChunkId)
            .ThenBy(p => p.Id)
            .ToListAsync(cancellationToken);

        // Load chunks for section name mapping
        var chunks = await _dbContext.DocumentChunks
            .Where(c => c.DocumentId == documentId)
            .ToListAsync(cancellationToken);

        sw.Stop();

        return new SyntheticQAResult
        {
            DocumentId = documentId,
            DocumentName = document.FileName,
            TotalPairsGenerated = pairRecords.Count,
            Pairs = pairRecords.Select(p => MapToQAPair(p, chunks)).ToList(),
            LlmProvider = pairRecords.FirstOrDefault()?.LlmProvider ?? string.Empty,
            ElapsedMilliseconds = sw.ElapsedMilliseconds
        };
    }

    /// <summary>
    /// Generates Q&amp;A pairs for a single document chunk by sending PII-redacted content to the LLM.
    /// </summary>
    private async Task<List<DocumentQAPairRecord>> GenerateQAPairsForChunkAsync(
        DocumentRecord document, DocumentChunkRecord chunk, CancellationToken cancellationToken)
    {
        // PII redaction before external LLM call (mandatory per CLAUDE.md)
        var redactedContent = _piiRedactor.Redact(chunk.Content);

        var prompt = "You are an insurance domain expert creating training data for fine-tuning.\n" +
            "Given this insurance document excerpt, generate 2-3 question-answer pairs.\n\n" +
            "Requirements:\n" +
            "- Questions should be natural, as a policyholder or adjuster would ask\n" +
            "- Answers must be grounded in the provided text only\n" +
            "- Each pair must have a category: \"factual\" (direct from text), \"inferential\" (requires reasoning), or \"procedural\" (how-to steps)\n" +
            "- Rate your confidence 0.0-1.0 for each pair\n\n" +
            $"Document section: {_piiRedactor.Redact(chunk.SectionName ?? "GENERAL")}\n" +
            "Content:\n" +
            $"{redactedContent}\n\n" +
            "Respond in JSON format only, no markdown code fences:\n" +
            "[{\"question\": \"...\", \"answer\": \"...\", \"category\": \"factual|inferential|procedural\", \"confidence\": 0.95}]";

        var kernel = _kernelProvider.GetKernel();
        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(
            "You are an insurance domain expert. Generate Q&A training data. Return ONLY a valid JSON array. No markdown code fences.");
        chatHistory.AddUserMessage(prompt);

        var response = await chatService.GetChatMessageContentAsync(
            chatHistory, cancellationToken: cancellationToken);

        var responseText = response.Content ?? string.Empty;

        _logger.LogDebug(
            "LLM response for chunk {ChunkIndex} of document {DocumentId}: {ResponseLength} chars",
            chunk.ChunkIndex, document.Id, responseText.Length);

        // Parse the JSON response (may be wrapped in markdown fences)
        var parsedPairs = ParseQAPairsFromLlmResponse(responseText);

        // Convert to entity records
        var providerName = _kernelProvider.ActiveProviderName;
        return parsedPairs.Select(p => new DocumentQAPairRecord
        {
            DocumentId = document.Id,
            ChunkId = chunk.Id,
            Question = TruncateString(p.Question, 2000),
            Answer = TruncateString(p.Answer, 4000),
            Category = ValidateCategory(p.Category),
            Confidence = Math.Clamp(p.Confidence, 0.0, 1.0),
            LlmProvider = providerName
        }).ToList();
    }

    /// <summary>
    /// Parses Q&amp;A pairs from an LLM response string.
    /// Handles markdown-wrapped JSON, array responses, and malformed output gracefully.
    /// </summary>
    private List<LlmQAPair> ParseQAPairsFromLlmResponse(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            _logger.LogWarning("Empty LLM response for Q&A generation");
            return [];
        }

        // Strip markdown code fences if present
        var cleaned = MarkdownFenceRegex.Replace(responseText, "").Trim();

        // Try parsing as a JSON array first
        try
        {
            // Find the array bounds — LLM may include text before/after the JSON
            var arrayStart = cleaned.IndexOf('[');
            var arrayEnd = cleaned.LastIndexOf(']');

            if (arrayStart >= 0 && arrayEnd > arrayStart)
            {
                var jsonArray = cleaned[arrayStart..(arrayEnd + 1)];
                var pairs = JsonSerializer.Deserialize<List<LlmQAPair>>(jsonArray, JsonOptions);
                if (pairs != null && pairs.Count > 0)
                {
                    return pairs;
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse Q&A JSON array from LLM response, trying object fallback");
        }

        // Fallback: try parsing as a single object wrapped in array
        try
        {
            var objectStart = cleaned.IndexOf('{');
            var objectEnd = cleaned.LastIndexOf('}');

            if (objectStart >= 0 && objectEnd > objectStart)
            {
                var jsonObject = cleaned[objectStart..(objectEnd + 1)];
                var singlePair = JsonSerializer.Deserialize<LlmQAPair>(jsonObject, JsonOptions);
                if (singlePair != null && !string.IsNullOrWhiteSpace(singlePair.Question))
                {
                    return [singlePair];
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse single Q&A object from LLM response");
        }

        _logger.LogWarning("Could not extract any Q&A pairs from LLM response: {Preview}",
            responseText.Length > 200 ? responseText[..200] + "..." : responseText);
        return [];
    }

    /// <summary>Maps a DocumentQAPairRecord entity to the API QAPair model.</summary>
    private static QAPair MapToQAPair(DocumentQAPairRecord record, IEnumerable<DocumentChunkRecord> chunks)
    {
        var sectionName = chunks.FirstOrDefault(c => c.Id == record.ChunkId)?.SectionName ?? "GENERAL";

        return new QAPair
        {
            Id = record.Id,
            ChunkId = record.ChunkId,
            Question = record.Question,
            Answer = record.Answer,
            Category = record.Category,
            Confidence = record.Confidence,
            SectionName = sectionName
        };
    }

    /// <summary>Validates category is one of the allowed values, defaulting to "factual".</summary>
    private static string ValidateCategory(string category)
    {
        return category?.ToLowerInvariant() switch
        {
            "factual" => "factual",
            "inferential" => "inferential",
            "procedural" => "procedural",
            _ => "factual"
        };
    }

    /// <summary>Truncates a string to the specified max length to respect DB column limits.</summary>
    private static string TruncateString(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    /// <summary>
    /// Internal DTO for deserializing Q&amp;A pairs from LLM JSON responses.
    /// </summary>
    private sealed class LlmQAPair
    {
        public string Question { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;
        public string Category { get; set; } = "factual";
        public double Confidence { get; set; } = 0.8;
    }
}
