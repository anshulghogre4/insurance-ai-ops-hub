using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SentimentAnalyzer.API.Data;
using SentimentAnalyzer.API.Data.Entities;
using SentimentAnalyzer.API.Models;
using SentimentAnalyzer.Agents.Orchestration;
using SentimentAnalyzer.API.Services.Embeddings;
using SentimentAnalyzer.API.Services.Multimodal;
using SentimentAnalyzer.API.Services.Providers;

namespace SentimentAnalyzer.API.Services.Documents;

/// <summary>
/// RAG facade for insurance document intelligence.
/// Upload flow: file -> OCR -> PII redact (via OCR service) -> chunk -> embed -> store.
/// Query flow: question -> embed -> vector search top-20 -> BM25 sparse score -> RRF fusion -> top-5 -> LLM answer with citations.
/// Uses direct IChatCompletionService for single-turn Q&A (not multi-agent, for latency).
/// Hybrid retrieval (dense + sparse) catches exact keyword matches (policy numbers, claim IDs)
/// that cosine similarity alone may miss.
/// </summary>
public class DocumentIntelligenceService : IDocumentIntelligenceService
{
    private readonly IDocumentOcrService _ocrService;
    private readonly IDocumentChunkingService _chunkingService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IDocumentRepository _documentRepository;
    private readonly IResilientKernelProvider _kernelProvider;
    private readonly IPIIRedactor _piiRedactor;
    private readonly IHybridRetrievalService _hybridRetrieval;
    private readonly ILogger<DocumentIntelligenceService> _logger;
    private readonly IContentSafetyService? _contentSafety;

    /// <summary>
    /// Initializes the Document Intelligence RAG service.
    /// </summary>
    /// <param name="ocrService">Resilient OCR provider for text extraction.</param>
    /// <param name="chunkingService">Insurance-aware document chunking service.</param>
    /// <param name="embeddingService">Resilient embedding provider for vector generation.</param>
    /// <param name="documentRepository">Repository for document and chunk persistence.</param>
    /// <param name="kernelProvider">Resilient kernel provider for LLM access with automatic fallback.</param>
    /// <param name="piiRedactor">PII redaction service — mandatory before external AI calls.</param>
    /// <param name="hybridRetrieval">Hybrid retrieval service for BM25 + vector fusion.</param>
    /// <param name="logger">Structured logger for this service.</param>
    /// <param name="contentSafety">Optional content safety screening service. Null disables screening (non-blocking).</param>
    public DocumentIntelligenceService(
        IDocumentOcrService ocrService,
        IDocumentChunkingService chunkingService,
        IEmbeddingService embeddingService,
        IDocumentRepository documentRepository,
        IResilientKernelProvider kernelProvider,
        IPIIRedactor piiRedactor,
        IHybridRetrievalService hybridRetrieval,
        ILogger<DocumentIntelligenceService> logger,
        IContentSafetyService? contentSafety = null)
    {
        _ocrService = ocrService ?? throw new ArgumentNullException(nameof(ocrService));
        _chunkingService = chunkingService ?? throw new ArgumentNullException(nameof(chunkingService));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _kernelProvider = kernelProvider ?? throw new ArgumentNullException(nameof(kernelProvider));
        _piiRedactor = piiRedactor ?? throw new ArgumentNullException(nameof(piiRedactor));
        _hybridRetrieval = hybridRetrieval ?? throw new ArgumentNullException(nameof(hybridRetrieval));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _contentSafety = contentSafety;
    }

    public async Task<DocumentUploadResult> UploadAsync(
        byte[] fileData, string mimeType, string fileName,
        string category = "Other", CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting document upload: {FileName} ({MimeType}, {Size} bytes)",
            fileName, mimeType, fileData.Length);

        // Step 1: Create document record
        var document = new DocumentRecord
        {
            FileName = fileName,
            MimeType = mimeType,
            Category = category,
            Status = "Processing"
        };
        await _documentRepository.SaveDocumentAsync(document);

        try
        {
            // Step 2: OCR extraction (PII redaction is handled by OcrSpaceService)
            var ocrResult = await _ocrService.ExtractTextAsync(fileData, mimeType, cancellationToken);
            if (!ocrResult.IsSuccess || string.IsNullOrWhiteSpace(ocrResult.ExtractedText))
            {
                document.Status = "Failed";
                document.ErrorMessage = ocrResult.ErrorMessage ?? "OCR extraction returned empty text.";
                await _documentRepository.UpdateDocumentAsync(document);
                return MapToUploadResult(document);
            }

            document.ExtractedText = ocrResult.ExtractedText;
            document.PageCount = ocrResult.PageCount;

            // Step 3: Chunk the document
            var chunks = _chunkingService.ChunkDocument(ocrResult.ExtractedText);
            if (chunks.Count == 0)
            {
                document.Status = "Failed";
                document.ErrorMessage = "Document produced zero chunks after processing.";
                await _documentRepository.UpdateDocumentAsync(document);
                return MapToUploadResult(document);
            }

            // Step 4: Generate embeddings for all chunks (batch call)
            var chunkTexts = chunks.Select(c => c.Content).ToArray();
            var batchResult = await _embeddingService.GenerateBatchEmbeddingsAsync(chunkTexts, "document", cancellationToken);

            if (!batchResult.IsSuccess || batchResult.Count != chunks.Count)
            {
                document.Status = "Failed";
                document.ErrorMessage = batchResult.ErrorMessage ?? "Embedding generation failed or returned wrong count.";
                await _documentRepository.UpdateDocumentAsync(document);
                return MapToUploadResult(document);
            }

            // Step 5: Create chunk records with embeddings
            var chunkRecords = chunks.Select((chunk, i) => new DocumentChunkRecord
            {
                DocumentId = document.Id,
                ChunkIndex = chunk.Index,
                SectionName = chunk.SectionName,
                Content = chunk.Content,
                TokenCount = chunk.ApproximateTokens,
                EmbeddingJson = JsonSerializer.Serialize(batchResult.Embeddings[i]),
                PageNumber = chunk.PageNumber,
                ParentChunkId = chunk.ParentChunkIndex.HasValue
                    ? chunks[chunk.ParentChunkIndex.Value].Index
                    : null,
                ChunkLevel = chunk.ChunkLevel
            }).ToList();

            // Step 5b: Content safety screening — flag but NEVER reject (insurance evidence may contain violent/harmful content)
            await ScreenChunksSafetyAsync(chunkRecords, cancellationToken);

            await _documentRepository.SaveChunksAsync(chunkRecords);

            // Step 6: Update document as ready
            document.ChunkCount = chunks.Count;
            document.EmbeddingProvider = batchResult.Provider;
            document.EmbeddingDimensions = batchResult.Dimension;
            document.Status = "Ready";
            await _documentRepository.UpdateDocumentAsync(document);

            _logger.LogInformation("Document {Id} processed: {Chunks} chunks, {Provider} embeddings ({Dim}-dim)",
                document.Id, chunks.Count, batchResult.Provider, batchResult.Dimension);

            return MapToUploadResult(document);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Document upload failed for {FileName}", fileName);
            document.Status = "Failed";
            document.ErrorMessage = $"Processing error: {ex.Message}";
            await _documentRepository.UpdateDocumentAsync(document);
            return MapToUploadResult(document);
        }
    }

    /// <summary>Upload with SSE progress events for real-time UI feedback.</summary>
    public async IAsyncEnumerable<DocumentProgressEvent> UploadWithProgressAsync(
        byte[] fileData, string mimeType, string fileName,
        string category = "Other", [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting streaming document upload: {FileName} ({MimeType}, {Size} bytes)",
            fileName, mimeType, fileData.Length);

        yield return new DocumentProgressEvent
        {
            Phase = "Uploading", Progress = 5,
            Message = $"Receiving {fileName} ({fileData.Length / 1024} KB)..."
        };

        // Step 1: Create document record
        var document = new DocumentRecord
        {
            FileName = fileName,
            MimeType = mimeType,
            Category = category,
            Status = "Processing"
        };
        await _documentRepository.SaveDocumentAsync(document);

        yield return new DocumentProgressEvent
        {
            Phase = "Uploading", Progress = 10,
            Message = "Document registered. Starting OCR extraction..."
        };

        // Step 2: OCR extraction
        yield return new DocumentProgressEvent
        {
            Phase = "OCR", Progress = 15,
            Message = "Extracting text with OCR..."
        };

        var ocrResult = await _ocrService.ExtractTextAsync(fileData, mimeType, cancellationToken);
        if (!ocrResult.IsSuccess || string.IsNullOrWhiteSpace(ocrResult.ExtractedText))
        {
            document.Status = "Failed";
            document.ErrorMessage = ocrResult.ErrorMessage ?? "OCR extraction returned empty text.";
            await _documentRepository.UpdateDocumentAsync(document);
            yield return new DocumentProgressEvent
            {
                Phase = "Error", Progress = 0,
                Message = "OCR extraction failed.",
                ErrorMessage = document.ErrorMessage
            };
            yield break;
        }

        document.ExtractedText = ocrResult.ExtractedText;
        document.PageCount = ocrResult.PageCount;

        yield return new DocumentProgressEvent
        {
            Phase = "OCR", Progress = 30,
            Message = $"Extracted text from {ocrResult.PageCount} pages."
        };

        // Step 3: Chunking
        yield return new DocumentProgressEvent
        {
            Phase = "Chunking", Progress = 35,
            Message = "Splitting into insurance sections..."
        };

        var chunks = _chunkingService.ChunkDocument(ocrResult.ExtractedText);
        if (chunks.Count == 0)
        {
            document.Status = "Failed";
            document.ErrorMessage = "Document produced zero chunks after processing.";
            await _documentRepository.UpdateDocumentAsync(document);
            yield return new DocumentProgressEvent
            {
                Phase = "Error", Progress = 0,
                Message = "Chunking failed.",
                ErrorMessage = document.ErrorMessage
            };
            yield break;
        }

        var parentCount = chunks.Count(c => c.ChunkLevel == 0 && chunks.Any(ch => ch.ParentChunkIndex == c.Index));
        var childCount = chunks.Count(c => c.ChunkLevel == 1);

        yield return new DocumentProgressEvent
        {
            Phase = "Chunking", Progress = 45,
            Message = $"Created {chunks.Count} chunks ({parentCount} sections, {childCount} sub-chunks)."
        };

        // Step 4: Embedding generation
        yield return new DocumentProgressEvent
        {
            Phase = "Embedding", Progress = 50,
            Message = $"Generating vector embeddings for {chunks.Count} chunks..."
        };

        var chunkTexts = chunks.Select(c => c.Content).ToArray();
        var batchResult = await _embeddingService.GenerateBatchEmbeddingsAsync(chunkTexts, "document", cancellationToken);

        if (!batchResult.IsSuccess || batchResult.Count != chunks.Count)
        {
            document.Status = "Failed";
            document.ErrorMessage = batchResult.ErrorMessage ?? "Embedding generation failed or returned wrong count.";
            await _documentRepository.UpdateDocumentAsync(document);
            yield return new DocumentProgressEvent
            {
                Phase = "Error", Progress = 0,
                Message = "Embedding generation failed.",
                ErrorMessage = document.ErrorMessage
            };
            yield break;
        }

        yield return new DocumentProgressEvent
        {
            Phase = "Embedding", Progress = 75,
            Message = $"Embeddings generated via {batchResult.Provider} ({batchResult.Dimension}-dim)."
        };

        // Step 5: Create chunk records with embeddings
        var chunkRecords = chunks.Select((chunk, i) => new DocumentChunkRecord
        {
            DocumentId = document.Id,
            ChunkIndex = chunk.Index,
            SectionName = chunk.SectionName,
            Content = chunk.Content,
            TokenCount = chunk.ApproximateTokens,
            EmbeddingJson = JsonSerializer.Serialize(batchResult.Embeddings[i]),
            PageNumber = chunk.PageNumber,
            ParentChunkId = chunk.ParentChunkIndex.HasValue
                ? chunks[chunk.ParentChunkIndex.Value].Index
                : null,
            ChunkLevel = chunk.ChunkLevel
        }).ToList();

        // Step 5b: Content safety screening — flag but NEVER reject
        yield return new DocumentProgressEvent
        {
            Phase = "Safety", Progress = 80,
            Message = $"Screening {chunkRecords.Count} chunks for content safety..."
        };

        var safetyScreened = await ScreenChunksSafetyAsync(chunkRecords, cancellationToken);
        var flaggedCount = chunkRecords.Count(c => !c.IsSafe);

        yield return new DocumentProgressEvent
        {
            Phase = "Safety", Progress = 88,
            Message = safetyScreened
                ? (flaggedCount > 0
                    ? $"Safety screening complete: {flaggedCount} of {chunkRecords.Count} chunks flagged (retained for evidence)."
                    : $"Safety screening complete: all {chunkRecords.Count} chunks passed.")
                : $"Safety screening skipped (service unavailable). {chunkRecords.Count} chunks proceed without screening."
        };

        // Step 5c: Store chunks
        yield return new DocumentProgressEvent
        {
            Phase = "Safety", Progress = 92,
            Message = "Storing document index..."
        };

        await _documentRepository.SaveChunksAsync(chunkRecords);

        // Step 6: Update document as ready
        document.ChunkCount = chunks.Count;
        document.EmbeddingProvider = batchResult.Provider;
        document.EmbeddingDimensions = batchResult.Dimension;
        document.Status = "Ready";
        await _documentRepository.UpdateDocumentAsync(document);

        yield return new DocumentProgressEvent
        {
            Phase = "Done", Progress = 100,
            Message = "Document ready for queries.",
            Result = MapToUploadResult(document)
        };

        _logger.LogInformation("Streaming upload complete: Document {Id}, {Chunks} chunks, {Provider}",
            document.Id, chunks.Count, batchResult.Provider);
    }

    public async Task<DocumentQueryResult> QueryAsync(
        string question, int? documentId = null, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("Document query: {QuestionLength} chars, documentId: {DocId}",
            question.Length, documentId?.ToString() ?? "all");

        // Step 1: Embed the question
        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(question, "query", cancellationToken);
        if (!queryEmbedding.IsSuccess)
        {
            return new DocumentQueryResult
            {
                Answer = "Unable to process your question. Embedding generation failed.",
                Confidence = 0,
                ElapsedMilliseconds = sw.ElapsedMilliseconds
            };
        }

        // Step 2: Vector search for top-20 candidates (wider net for hybrid retrieval)
        var vectorResults = await _documentRepository.SearchSimilarChunksAsync(
            queryEmbedding.Embedding, topK: 20, documentId: documentId);

        // Filter out low-relevance chunks before BM25.
        // Voyage AI asymmetric embeddings (input_type: "document" vs "query") produce
        // naturally lower cosine similarity scores than symmetric models.
        // Empirical testing: exact-match queries score ~0.27, so 0.15 is a safe floor.
        const double MinSimilarityThreshold = 0.15;
        vectorResults = vectorResults
            .Where(r => r.Similarity >= MinSimilarityThreshold)
            .ToList();

        if (vectorResults.Count == 0)
        {
            return new DocumentQueryResult
            {
                Answer = "No relevant document content found. Please upload documents first or rephrase your question.",
                Confidence = 0,
                LlmProvider = queryEmbedding.Provider,
                ElapsedMilliseconds = sw.ElapsedMilliseconds
            };
        }

        // Step 3: BM25 sparse scoring on the same pre-filtered candidates
        var candidateChunks = vectorResults.Select(r => r.Chunk).ToList();
        var bm25Results = BM25Scorer.Score(question, candidateChunks);

        // Step 4: Reciprocal Rank Fusion (RRF) to merge dense + sparse results
        var vectorResultsForFusion = vectorResults
            .Select(r => (r.Chunk, Score: r.Similarity))
            .ToList();
        var fusedResults = _hybridRetrieval.FuseResults(vectorResultsForFusion, bm25Results, topK: 5);

        _logger.LogInformation(
            "Hybrid RAG: {VectorCount} vector candidates -> {BM25Count} BM25 scored -> {FusedCount} fused results",
            vectorResults.Count, bm25Results.Count, fusedResults.Count);

        // Step 5: Assemble context from fused results
        var contextBuilder = new StringBuilder();
        var citations = new List<DocumentCitation>();

        for (var i = 0; i < fusedResults.Count; i++)
        {
            var (chunk, score) = fusedResults[i];
            var docName = chunk.Document?.FileName ?? $"Document #{chunk.DocumentId}";

            contextBuilder.AppendLine($"[{i + 1}] Document: \"{docName}\", Section: {chunk.SectionName}");
            contextBuilder.AppendLine(chunk.Content);
            contextBuilder.AppendLine();

            citations.Add(new DocumentCitation
            {
                DocumentId = chunk.DocumentId,
                FileName = docName,
                SectionName = chunk.SectionName,
                ChunkIndex = chunk.ChunkIndex,
                RelevantText = chunk.Content.Length > 200 ? chunk.Content[..200] + "..." : chunk.Content,
                Similarity = score
            });
        }

        // Step 6: PII redaction before external LLM call (mandatory per CLAUDE.md)
        var redactedQuestion = _piiRedactor.Redact(question);

        // Step 7: LLM answer generation (direct kernel, not multi-agent)
        var systemPrompt = """
            You are an insurance document Q&A assistant. Answer the question using ONLY the provided document excerpts.
            If the answer is not in the excerpts, say "I could not find this information in the indexed documents."
            Always cite which document and section your answer comes from using the reference numbers [1], [2], etc.
            Be precise, factual, and concise. Never fabricate policy terms or coverage details.
            """;

        var userPrompt = $"""
            DOCUMENT EXCERPTS:
            {contextBuilder}

            QUESTION: {redactedQuestion}

            Provide your answer with citations.
            """;

        try
        {
            var chatService = _kernelProvider.GetKernel().GetRequiredService<IChatCompletionService>();
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(systemPrompt);
            chatHistory.AddUserMessage(userPrompt);

            var response = await chatService.GetChatMessageContentAsync(chatHistory, cancellationToken: cancellationToken);
            var answer = response.Content ?? "No answer generated.";

            // Step 8: Content safety screening on the LLM answer
            var answerSafety = await ScreenAnswerSafetyAsync(answer, cancellationToken);
            if (answerSafety != null && !answerSafety.IsSafe)
            {
                answer = $"[Content Warning: This response has been flagged for potentially sensitive content] {answer}";
            }

            var avgScore = fusedResults.Average(r => r.Score);

            return new DocumentQueryResult
            {
                Answer = answer,
                Confidence = Math.Round(avgScore, 3),
                Citations = citations,
                LlmProvider = _kernelProvider.ActiveProviderName,
                ElapsedMilliseconds = sw.ElapsedMilliseconds,
                AnswerSafety = answerSafety
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLM answer generation failed for document query");
            return new DocumentQueryResult
            {
                Answer = "Unable to generate an answer. The AI service encountered an error.",
                Confidence = 0,
                Citations = citations,
                ElapsedMilliseconds = sw.ElapsedMilliseconds
            };
        }
    }

    public async Task<DocumentDetailResult?> GetDocumentByIdAsync(int documentId)
    {
        var document = await _documentRepository.GetDocumentByIdAsync(documentId);
        if (document == null) return null;

        return new DocumentDetailResult
        {
            Id = document.Id,
            FileName = document.FileName,
            MimeType = document.MimeType,
            Category = document.Category,
            Status = document.Status,
            PageCount = document.PageCount,
            ChunkCount = document.ChunkCount,
            EmbeddingProvider = document.EmbeddingProvider,
            Chunks = document.Chunks.Select(c => new ChunkSummary
            {
                ChunkIndex = c.ChunkIndex,
                SectionName = c.SectionName,
                TokenCount = c.TokenCount,
                ContentPreview = c.Content.Length > 100 ? c.Content[..100] + "..." : c.Content,
                PageNumber = c.PageNumber,
                ParentChunkId = c.ParentChunkId,
                ChunkLevel = c.ChunkLevel,
                IsSafe = c.IsSafe,
                SafetyFlags = c.SafetyFlags
            }).ToList(),
            CreatedAt = document.CreatedAt
        };
    }

    public async Task<(List<DocumentSummary> Items, int TotalCount)> GetDocumentsAsync(
        string? category = null, int pageSize = 20, int page = 1)
    {
        var (items, totalCount) = await _documentRepository.GetDocumentsAsync(category, status: null, pageSize, page);

        var summaries = items.Select(d => new DocumentSummary
        {
            Id = d.Id,
            FileName = d.FileName,
            MimeType = d.MimeType,
            Category = d.Category,
            Status = d.Status,
            PageCount = d.PageCount,
            ChunkCount = d.ChunkCount,
            CreatedAt = d.CreatedAt
        }).ToList();

        return (summaries, totalCount);
    }

    /// <summary>
    /// Screens chunk content through Azure Content Safety, setting IsSafe and SafetyFlags on each record.
    /// Insurance evidence documents may contain violent/harmful content that is legitimate evidence,
    /// so chunks are flagged but NEVER rejected. Non-blocking: if content safety is unavailable
    /// or rate-limited, chunks proceed without screening.
    /// </summary>
    /// <param name="chunkRecords">The chunk records to screen (mutated in place).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if screening was performed; false if skipped (service unavailable).</returns>
    private async Task<bool> ScreenChunksSafetyAsync(
        List<DocumentChunkRecord> chunkRecords, CancellationToken cancellationToken)
    {
        if (_contentSafety == null)
        {
            _logger.LogInformation("Content safety screening skipped for {ChunkCount} chunks — service not configured",
                chunkRecords.Count);
            return false;
        }

        var screenedCount = 0;
        var flaggedCount = 0;

        foreach (var chunk in chunkRecords)
        {
            try
            {
                var safetyResult = await _contentSafety.AnalyzeTextAsync(chunk.Content, cancellationToken);

                if (!safetyResult.IsSuccess)
                {
                    // Content Safety unavailable or rate-limited — proceed without screening (non-blocking)
                    _logger.LogWarning(
                        "Content safety screening failed for chunk {ChunkIndex}: {Error} — proceeding without screening",
                        chunk.ChunkIndex, safetyResult.ErrorMessage);
                    continue;
                }

                screenedCount++;
                chunk.IsSafe = safetyResult.IsSafe;

                if (!safetyResult.IsSafe)
                {
                    chunk.SafetyFlags = string.Join("|", safetyResult.FlaggedCategories);
                    flaggedCount++;
                    _logger.LogWarning(
                        "Chunk {ChunkIndex} flagged by Content Safety: {Flags} (retained as insurance evidence)",
                        chunk.ChunkIndex, chunk.SafetyFlags);
                }
            }
            catch (Exception ex)
            {
                // Non-blocking: any failure during screening should not halt document processing
                _logger.LogWarning(ex,
                    "Content safety screening exception for chunk {ChunkIndex} — proceeding without screening",
                    chunk.ChunkIndex);
            }
        }

        _logger.LogInformation(
            "Content safety screening complete: {Screened}/{Total} chunks screened, {Flagged} flagged",
            screenedCount, chunkRecords.Count, flaggedCount);

        return screenedCount > 0;
    }

    /// <summary>
    /// Screens the LLM-generated answer through Azure Content Safety.
    /// Returns a ContentSafetyInfo model for the API response, or null if screening was skipped.
    /// Non-blocking: if content safety is unavailable, returns null (not screened).
    /// </summary>
    /// <param name="answer">The LLM-generated answer text to screen.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>ContentSafetyInfo if screened; null if screening was skipped or unavailable.</returns>
    private async Task<ContentSafetyInfo?> ScreenAnswerSafetyAsync(
        string answer, CancellationToken cancellationToken)
    {
        if (_contentSafety == null)
        {
            _logger.LogInformation("Answer content safety screening skipped — service not configured");
            return null;
        }

        try
        {
            var safetyResult = await _contentSafety.AnalyzeTextAsync(answer, cancellationToken);

            if (!safetyResult.IsSuccess)
            {
                _logger.LogWarning(
                    "Answer content safety screening failed: {Error} — returning unscreened",
                    safetyResult.ErrorMessage);
                return null;
            }

            var safetyInfo = new ContentSafetyInfo
            {
                IsSafe = safetyResult.IsSafe,
                FlaggedCategories = safetyResult.FlaggedCategories,
                Provider = safetyResult.Provider
            };

            if (!safetyResult.IsSafe)
            {
                _logger.LogWarning(
                    "Document query answer flagged by Content Safety: {Categories}",
                    string.Join(", ", safetyResult.FlaggedCategories));
            }

            return safetyInfo;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Answer content safety screening exception — returning unscreened");
            return null;
        }
    }

    private static DocumentUploadResult MapToUploadResult(DocumentRecord document)
    {
        return new DocumentUploadResult
        {
            DocumentId = document.Id,
            FileName = document.FileName,
            Status = document.Status,
            PageCount = document.PageCount,
            ChunkCount = document.ChunkCount,
            EmbeddingProvider = document.EmbeddingProvider,
            ErrorMessage = document.ErrorMessage
        };
    }
}
