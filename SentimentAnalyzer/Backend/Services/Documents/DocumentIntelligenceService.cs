using System.Diagnostics;
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
/// Query flow: question -> embed -> vector search top-5 -> LLM answer with citations.
/// Uses direct IChatCompletionService for single-turn Q&A (not multi-agent, for latency).
/// </summary>
public class DocumentIntelligenceService : IDocumentIntelligenceService
{
    private readonly IDocumentOcrService _ocrService;
    private readonly IDocumentChunkingService _chunkingService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IDocumentRepository _documentRepository;
    private readonly IResilientKernelProvider _kernelProvider;
    private readonly IPIIRedactor _piiRedactor;
    private readonly ILogger<DocumentIntelligenceService> _logger;

    public DocumentIntelligenceService(
        IDocumentOcrService ocrService,
        IDocumentChunkingService chunkingService,
        IEmbeddingService embeddingService,
        IDocumentRepository documentRepository,
        IResilientKernelProvider kernelProvider,
        IPIIRedactor piiRedactor,
        ILogger<DocumentIntelligenceService> logger)
    {
        _ocrService = ocrService ?? throw new ArgumentNullException(nameof(ocrService));
        _chunkingService = chunkingService ?? throw new ArgumentNullException(nameof(chunkingService));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _kernelProvider = kernelProvider ?? throw new ArgumentNullException(nameof(kernelProvider));
        _piiRedactor = piiRedactor ?? throw new ArgumentNullException(nameof(piiRedactor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
                EmbeddingJson = JsonSerializer.Serialize(batchResult.Embeddings[i])
            }).ToList();

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

        // Step 2: Vector search for top-5 most similar chunks
        var searchResults = await _documentRepository.SearchSimilarChunksAsync(
            queryEmbedding.Embedding, topK: 5, documentId: documentId);

        // Filter out low-relevance chunks (minimum similarity threshold)
        const double MinSimilarityThreshold = 0.3;
        searchResults = searchResults
            .Where(r => r.Similarity >= MinSimilarityThreshold)
            .ToList();

        if (searchResults.Count == 0)
        {
            return new DocumentQueryResult
            {
                Answer = "No relevant document content found. Please upload documents first or rephrase your question.",
                Confidence = 0,
                LlmProvider = queryEmbedding.Provider,
                ElapsedMilliseconds = sw.ElapsedMilliseconds
            };
        }

        // Step 3: Assemble context from retrieved chunks
        var contextBuilder = new StringBuilder();
        var citations = new List<DocumentCitation>();

        for (var i = 0; i < searchResults.Count; i++)
        {
            var (chunk, similarity) = searchResults[i];
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
                Similarity = similarity
            });
        }

        // Step 4: PII redaction before external LLM call (mandatory per CLAUDE.md)
        var redactedQuestion = _piiRedactor.Redact(question);

        // Step 5: LLM answer generation (direct kernel, not multi-agent)
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

            var avgSimilarity = searchResults.Average(r => r.Similarity);

            return new DocumentQueryResult
            {
                Answer = answer,
                Confidence = Math.Round(avgSimilarity, 3),
                Citations = citations,
                LlmProvider = _kernelProvider.ActiveProviderName,
                ElapsedMilliseconds = sw.ElapsedMilliseconds
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
                ContentPreview = c.Content.Length > 100 ? c.Content[..100] + "..." : c.Content
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
