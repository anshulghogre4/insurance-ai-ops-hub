using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;
using SentimentAnalyzer.Agents.Orchestration;
using SentimentAnalyzer.API.Data;
using SentimentAnalyzer.API.Data.Entities;
using SentimentAnalyzer.API.Models;
using SentimentAnalyzer.API.Services.Documents;
using SentimentAnalyzer.API.Services.Embeddings;
using SentimentAnalyzer.API.Services.Multimodal;

namespace SentimentAnalyzer.Tests;

/// <summary>
/// Unit tests for DocumentIntelligenceService — the RAG facade that orchestrates
/// OCR extraction, document chunking, embedding generation, vector storage, and
/// LLM-powered question answering with citations.
/// </summary>
public class DocumentIntelligenceServiceTests
{
    private readonly Mock<IDocumentOcrService> _mockOcrService;
    private readonly Mock<IDocumentChunkingService> _mockChunkingService;
    private readonly Mock<IEmbeddingService> _mockEmbeddingService;
    private readonly Mock<IDocumentRepository> _mockDocumentRepository;
    private readonly Mock<IResilientKernelProvider> _mockKernelProvider;
    private readonly Mock<IChatCompletionService> _mockChatCompletion;
    private readonly Mock<IPIIRedactor> _mockPiiRedactor;
    private readonly Mock<IHybridRetrievalService> _mockHybridRetrieval;
    private readonly Mock<ILogger<DocumentIntelligenceService>> _mockLogger;
    private readonly DocumentIntelligenceService _sut;

    /// <summary>
    /// Sample insurance policy text used across upload tests.
    /// Realistic insurance content per project test data guidelines.
    /// </summary>
    private const string SamplePolicyText =
        "DECLARATIONS\nPolicy Number: [POLICY-REDACTED]\nInsured: [NAME-REDACTED]\n" +
        "COVERAGE\nDwelling Coverage: $350,000\nPersonal Property: $175,000\n" +
        "EXCLUSIONS\nFlood damage is excluded unless endorsement HO-340 is attached.";

    public DocumentIntelligenceServiceTests()
    {
        _mockOcrService = new Mock<IDocumentOcrService>();
        _mockChunkingService = new Mock<IDocumentChunkingService>();
        _mockEmbeddingService = new Mock<IEmbeddingService>();
        _mockDocumentRepository = new Mock<IDocumentRepository>();
        _mockKernelProvider = new Mock<IResilientKernelProvider>();
        _mockChatCompletion = new Mock<IChatCompletionService>();
        _mockPiiRedactor = new Mock<IPIIRedactor>();
        _mockHybridRetrieval = new Mock<IHybridRetrievalService>();
        _mockLogger = new Mock<ILogger<DocumentIntelligenceService>>();

        // Default PII redaction: pass-through (no PII detected in test data)
        _mockPiiRedactor.Setup(p => p.Redact(It.IsAny<string>())).Returns<string>(s => s);

        // Build a test Kernel with the mocked IChatCompletionService
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(_mockChatCompletion.Object);
        var kernel = builder.Build();

        _mockKernelProvider.Setup(p => p.GetKernel()).Returns(kernel);
        _mockKernelProvider.Setup(p => p.ActiveProviderName).Returns("Groq");

        // Repository SaveDocumentAsync assigns an Id to the document
        _mockDocumentRepository
            .Setup(r => r.SaveDocumentAsync(It.IsAny<DocumentRecord>()))
            .ReturnsAsync((DocumentRecord doc) =>
            {
                doc.Id = 42;
                return doc;
            });

        _sut = new DocumentIntelligenceService(
            _mockOcrService.Object,
            _mockChunkingService.Object,
            _mockEmbeddingService.Object,
            _mockDocumentRepository.Object,
            _mockKernelProvider.Object,
            _mockPiiRedactor.Object,
            _mockHybridRetrieval.Object,
            _mockLogger.Object);
    }

    // ────────────────────────────────────────────────────────────
    // UploadAsync Tests
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task UploadAsync_SuccessfulFlow_ReturnsReadyStatus()
    {
        // Arrange
        var fileData = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // %PDF magic bytes
        var fileName = "homeowners-policy-HO2024.pdf";
        var mimeType = "application/pdf";

        _mockOcrService
            .Setup(o => o.ExtractTextAsync(fileData, mimeType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult
            {
                IsSuccess = true,
                ExtractedText = SamplePolicyText,
                PageCount = 3,
                Confidence = 0.95,
                Provider = "OCR.space"
            });

        var chunks = new List<DocumentChunk>
        {
            new() { Index = 0, SectionName = "DECLARATIONS", Content = "Policy Number: [POLICY-REDACTED]", ApproximateTokens = 24 },
            new() { Index = 1, SectionName = "COVERAGE", Content = "Dwelling Coverage: $350,000", ApproximateTokens = 30 },
            new() { Index = 2, SectionName = "EXCLUSIONS", Content = "Flood damage is excluded unless endorsement HO-340 is attached.", ApproximateTokens = 28 }
        };

        _mockChunkingService
            .Setup(c => c.ChunkDocument(SamplePolicyText, It.IsAny<int>(), It.IsAny<int>()))
            .Returns(chunks);

        _mockEmbeddingService
            .Setup(e => e.GenerateBatchEmbeddingsAsync(
                It.Is<string[]>(texts => texts.Length == 3),
                "document",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BatchEmbeddingResult
            {
                IsSuccess = true,
                Embeddings = new float[][] { new float[1024], new float[1024], new float[1024] },
                Provider = "VoyageAI",
                TotalTokensUsed = 82
            });

        // Act
        var result = await _sut.UploadAsync(fileData, mimeType, fileName, "Policy");

        // Assert
        Assert.Equal("Ready", result.Status);
        Assert.Equal(3, result.ChunkCount);
        Assert.Equal(fileName, result.FileName);
        Assert.Equal("VoyageAI", result.EmbeddingProvider);
        Assert.Null(result.ErrorMessage);

        _mockDocumentRepository.Verify(r => r.SaveChunksAsync(It.IsAny<IEnumerable<DocumentChunkRecord>>()), Times.Once);
        _mockDocumentRepository.Verify(r => r.UpdateDocumentAsync(It.Is<DocumentRecord>(d => d.Status == "Ready")), Times.Once);
    }

    [Fact]
    public async Task UploadAsync_OcrFails_ReturnsFailedStatus()
    {
        // Arrange
        var fileData = new byte[] { 0xFF, 0xD8, 0xFF }; // JPEG header
        var fileName = "damaged-claim-photo.jpg";
        var mimeType = "image/jpeg";

        _mockOcrService
            .Setup(o => o.ExtractTextAsync(fileData, mimeType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult
            {
                IsSuccess = false,
                ExtractedText = string.Empty,
                PageCount = 0,
                ErrorMessage = "OCR.space returned error: Unable to parse image content."
            });

        // Act
        var result = await _sut.UploadAsync(fileData, mimeType, fileName, "Claim");

        // Assert
        Assert.Equal("Failed", result.Status);
        Assert.Contains("Unable to parse image content", result.ErrorMessage);
        Assert.Equal(0, result.ChunkCount);

        // Embedding and chunk storage should never be called
        _mockEmbeddingService.Verify(
            e => e.GenerateBatchEmbeddingsAsync(It.IsAny<string[]>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mockDocumentRepository.Verify(r => r.SaveChunksAsync(It.IsAny<IEnumerable<DocumentChunkRecord>>()), Times.Never);
    }

    [Fact]
    public async Task UploadAsync_EmbeddingFails_ReturnsFailedStatus()
    {
        // Arrange
        var fileData = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        var fileName = "auto-policy-renewal.pdf";
        var mimeType = "application/pdf";

        _mockOcrService
            .Setup(o => o.ExtractTextAsync(fileData, mimeType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult
            {
                IsSuccess = true,
                ExtractedText = "Auto insurance policy renewal document with comprehensive coverage.",
                PageCount = 1,
                Confidence = 0.92,
                Provider = "OCR.space"
            });

        var chunks = new List<DocumentChunk>
        {
            new() { Index = 0, SectionName = "GENERAL", Content = "Auto insurance policy renewal document with comprehensive coverage.", ApproximateTokens = 40 }
        };

        _mockChunkingService
            .Setup(c => c.ChunkDocument(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(chunks);

        _mockEmbeddingService
            .Setup(e => e.GenerateBatchEmbeddingsAsync(
                It.IsAny<string[]>(), "document", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BatchEmbeddingResult
            {
                IsSuccess = false,
                Embeddings = [],
                ErrorMessage = "Voyage AI rate limit exceeded (429). Ollama fallback also failed: connection refused."
            });

        // Act
        var result = await _sut.UploadAsync(fileData, mimeType, fileName, "Policy");

        // Assert
        Assert.Equal("Failed", result.Status);
        Assert.Contains("rate limit", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, result.ChunkCount);

        // Chunks should NOT be saved when embedding fails
        _mockDocumentRepository.Verify(r => r.SaveChunksAsync(It.IsAny<IEnumerable<DocumentChunkRecord>>()), Times.Never);
    }

    [Fact]
    public async Task UploadAsync_ZeroChunks_ReturnsFailedStatus()
    {
        // Arrange
        var fileData = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        var fileName = "blank-claim-form.pdf";
        var mimeType = "application/pdf";

        _mockOcrService
            .Setup(o => o.ExtractTextAsync(fileData, mimeType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult
            {
                IsSuccess = true,
                ExtractedText = "---",  // Non-whitespace but content too minimal for chunking
                PageCount = 1,
                Confidence = 0.10,
                Provider = "OCR.space"
            });

        _mockChunkingService
            .Setup(c => c.ChunkDocument(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(new List<DocumentChunk>());

        // Act
        var result = await _sut.UploadAsync(fileData, mimeType, fileName, "Claim");

        // Assert
        Assert.Equal("Failed", result.Status);
        Assert.Contains("zero chunks", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, result.ChunkCount);

        // Embedding should never be called when there are no chunks
        _mockEmbeddingService.Verify(
            e => e.GenerateBatchEmbeddingsAsync(It.IsAny<string[]>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ────────────────────────────────────────────────────────────
    // QueryAsync Tests
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task QueryAsync_WithMatchingChunks_ReturnsAnswerWithCitations()
    {
        // Arrange
        var question = "What is the dwelling coverage amount for the homeowners policy?";
        var queryEmbedding = new float[1024];
        queryEmbedding[0] = 0.5f;

        _mockEmbeddingService
            .Setup(e => e.GenerateEmbeddingAsync(question, "query", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                IsSuccess = true,
                Embedding = queryEmbedding,
                Provider = "VoyageAI",
                TokensUsed = 15
            });

        var chunk1 = new DocumentChunkRecord
        {
            Id = 1,
            DocumentId = 42,
            ChunkIndex = 0,
            SectionName = "DECLARATIONS",
            Content = "Dwelling Coverage: $350,000. Personal Property: $175,000.",
            TokenCount = 30,
            Document = new DocumentRecord { Id = 42, FileName = "homeowners-policy-HO2024.pdf" }
        };
        var chunk2 = new DocumentChunkRecord
        {
            Id = 2,
            DocumentId = 42,
            ChunkIndex = 1,
            SectionName = "COVERAGE",
            Content = "Coverage A - Dwelling covers the structure up to the stated limit.",
            TokenCount = 28,
            Document = new DocumentRecord { Id = 42, FileName = "homeowners-policy-HO2024.pdf" }
        };

        var chunkResults = new List<(DocumentChunkRecord Chunk, double Similarity)>
        {
            (chunk1, 0.92),
            (chunk2, 0.85)
        };

        _mockDocumentRepository
            .Setup(r => r.SearchSimilarChunksAsync(queryEmbedding, 20, null))
            .ReturnsAsync(chunkResults);

        // Hybrid retrieval returns fused results
        var fusedResults = new List<(DocumentChunkRecord Chunk, double Score)>
        {
            (chunk1, 0.033),
            (chunk2, 0.032)
        };
        _mockHybridRetrieval
            .Setup(h => h.FuseResults(
                It.IsAny<List<(DocumentChunkRecord Chunk, double Score)>>(),
                It.IsAny<List<(DocumentChunkRecord Chunk, double Score)>>(),
                5))
            .Returns(fusedResults);

        var llmAnswer = "The dwelling coverage amount is $350,000 per the declarations section [1].";
        _mockChatCompletion
            .Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatMessageContent>
            {
                new(AuthorRole.Assistant, llmAnswer)
            });

        // Act
        var result = await _sut.QueryAsync(question);

        // Assert
        Assert.Equal(llmAnswer, result.Answer);
        Assert.True(result.Confidence > 0, "Confidence should be positive when chunks are found.");
        Assert.Equal(2, result.Citations.Count);
        Assert.Equal("Groq", result.LlmProvider);

        var firstCitation = result.Citations[0];
        Assert.Equal(42, firstCitation.DocumentId);
        Assert.Equal("homeowners-policy-HO2024.pdf", firstCitation.FileName);
        Assert.Equal("DECLARATIONS", firstCitation.SectionName);
    }

    [Fact]
    public async Task QueryAsync_EmbeddingFails_ReturnsErrorMessage()
    {
        // Arrange
        var question = "Does the policy cover water damage from burst pipes?";

        _mockEmbeddingService
            .Setup(e => e.GenerateEmbeddingAsync(question, "query", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                IsSuccess = false,
                Embedding = [],
                ErrorMessage = "All embedding providers exhausted."
            });

        // Act
        var result = await _sut.QueryAsync(question);

        // Assert
        Assert.Contains("Embedding generation failed", result.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, result.Confidence);
        Assert.Empty(result.Citations);

        // Vector search should never be called when embedding fails (and thus LLM is also skipped)
        _mockDocumentRepository.Verify(
            r => r.SearchSimilarChunksAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<int?>()),
            Times.Never);
        _mockChatCompletion.Verify(
            c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(), It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task QueryAsync_NoChunksFound_ReturnsNoContentMessage()
    {
        // Arrange
        var question = "What are the cyber liability exclusions?";
        var queryEmbedding = new float[1024];

        _mockEmbeddingService
            .Setup(e => e.GenerateEmbeddingAsync(question, "query", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                IsSuccess = true,
                Embedding = queryEmbedding,
                Provider = "VoyageAI",
                TokensUsed = 12
            });

        _mockDocumentRepository
            .Setup(r => r.SearchSimilarChunksAsync(queryEmbedding, 20, null))
            .ReturnsAsync(new List<(DocumentChunkRecord Chunk, double Similarity)>());

        // Act
        var result = await _sut.QueryAsync(question);

        // Assert
        Assert.Contains("No relevant document content found", result.Answer);
        Assert.Equal(0, result.Confidence);
        Assert.Empty(result.Citations);

        // LLM should never be called when no chunks are retrieved
        _mockChatCompletion.Verify(
            c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(), It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task QueryAsync_RedactsPiiBeforeLlmCall()
    {
        // Arrange — PII redactor transforms input to verify it's used before LLM call
        var rawQuestion = "What coverage applies to policyholder John Smith, SSN 123-45-6789?";
        var redactedQuestion = "What coverage applies to policyholder [NAME-REDACTED], SSN [SSN-REDACTED]?";

        _mockPiiRedactor
            .Setup(p => p.Redact(rawQuestion))
            .Returns(redactedQuestion);

        var queryEmbedding = new float[1024];
        queryEmbedding[0] = 0.5f;

        _mockEmbeddingService
            .Setup(e => e.GenerateEmbeddingAsync(rawQuestion, "query", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                IsSuccess = true,
                Embedding = queryEmbedding,
                Provider = "VoyageAI",
                TokensUsed = 18
            });

        var piiChunk = new DocumentChunkRecord
        {
            Id = 1, DocumentId = 42, ChunkIndex = 0,
            SectionName = "COVERAGE",
            Content = "Dwelling Coverage: $350,000 for the insured property.",
            TokenCount = 25,
            Document = new DocumentRecord { Id = 42, FileName = "policy.pdf" }
        };

        var chunkResults = new List<(DocumentChunkRecord Chunk, double Similarity)>
        {
            (piiChunk, 0.88)
        };

        _mockDocumentRepository
            .Setup(r => r.SearchSimilarChunksAsync(queryEmbedding, 20, null))
            .ReturnsAsync(chunkResults);

        // Hybrid retrieval returns fused results
        _mockHybridRetrieval
            .Setup(h => h.FuseResults(
                It.IsAny<List<(DocumentChunkRecord Chunk, double Score)>>(),
                It.IsAny<List<(DocumentChunkRecord Chunk, double Score)>>(),
                5))
            .Returns(new List<(DocumentChunkRecord Chunk, double Score)> { (piiChunk, 0.033) });

        string? capturedPrompt = null;
        _mockChatCompletion
            .Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(),
                It.IsAny<CancellationToken>()))
            .Callback<ChatHistory, PromptExecutionSettings?, Kernel?, CancellationToken>((history, _, _, _) =>
            {
                // Capture the user message sent to LLM to verify PII was redacted
                capturedPrompt = history.LastOrDefault(m => m.Role == AuthorRole.User)?.Content;
            })
            .ReturnsAsync(new List<ChatMessageContent>
            {
                new(AuthorRole.Assistant, "The dwelling coverage is $350,000 [1].")
            });

        // Act
        var result = await _sut.QueryAsync(rawQuestion);

        // Assert — PII redactor was called exactly once with the raw question
        _mockPiiRedactor.Verify(p => p.Redact(rawQuestion), Times.Once);

        // Assert — LLM received the redacted question, NOT the raw PII
        Assert.NotNull(capturedPrompt);
        Assert.Contains("[SSN-REDACTED]", capturedPrompt);
        Assert.Contains("[NAME-REDACTED]", capturedPrompt);
        Assert.DoesNotContain("123-45-6789", capturedPrompt);
        Assert.DoesNotContain("John Smith", capturedPrompt);
    }

    [Fact]
    public async Task QueryAsync_FiltersLowSimilarityChunks()
    {
        // Arrange — return chunks with mixed similarity scores, some below 0.15 threshold
        var question = "What are the coverage limits?";
        var queryEmbedding = new float[1024];

        _mockEmbeddingService
            .Setup(e => e.GenerateEmbeddingAsync(question, "query", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                IsSuccess = true,
                Embedding = queryEmbedding,
                Provider = "VoyageAI",
                TokensUsed = 10
            });

        var coverageChunk = new DocumentChunkRecord
        {
            Id = 1, DocumentId = 42, ChunkIndex = 0,
            SectionName = "COVERAGE",
            Content = "Dwelling coverage: $350,000.",
            TokenCount = 20,
            Document = new DocumentRecord { Id = 42, FileName = "policy.pdf" }
        };

        var chunkResults = new List<(DocumentChunkRecord Chunk, double Similarity)>
        {
            (coverageChunk, 0.85),
            (new DocumentChunkRecord
            {
                Id = 2, DocumentId = 42, ChunkIndex = 1,
                SectionName = "DECLARATIONS",
                Content = "Policy period: 01/01/2025 to 01/01/2026.",
                TokenCount = 20,
                Document = new DocumentRecord { Id = 42, FileName = "policy.pdf" }
            }, 0.12),  // Below 0.15 threshold — should be filtered out
            (new DocumentChunkRecord
            {
                Id = 3, DocumentId = 42, ChunkIndex = 2,
                SectionName = "CONDITIONS",
                Content = "Random unrelated content.",
                TokenCount = 15,
                Document = new DocumentRecord { Id = 42, FileName = "policy.pdf" }
            }, 0.05)   // Below 0.15 threshold — should be filtered out
        };

        _mockDocumentRepository
            .Setup(r => r.SearchSimilarChunksAsync(queryEmbedding, 20, null))
            .ReturnsAsync(chunkResults);

        // After similarity filter, only coverageChunk remains → hybrid retrieval gets 1 candidate
        _mockHybridRetrieval
            .Setup(h => h.FuseResults(
                It.IsAny<List<(DocumentChunkRecord Chunk, double Score)>>(),
                It.IsAny<List<(DocumentChunkRecord Chunk, double Score)>>(),
                5))
            .Returns(new List<(DocumentChunkRecord Chunk, double Score)> { (coverageChunk, 0.033) });

        _mockChatCompletion
            .Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatMessageContent>
            {
                new(AuthorRole.Assistant, "The dwelling coverage is $350,000 [1].")
            });

        // Act
        var result = await _sut.QueryAsync(question);

        // Assert — only the chunk above threshold should appear in citations
        Assert.Single(result.Citations);
        Assert.Equal("COVERAGE", result.Citations[0].SectionName);
    }

    // ────────────────────────────────────────────────────────────
    // GetDocumentByIdAsync Tests
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetDocumentByIdAsync_ExistingDocument_ReturnsDetailResult()
    {
        // Arrange
        var documentId = 42;
        var document = new DocumentRecord
        {
            Id = documentId,
            FileName = "workers-comp-claim-WC2024.pdf",
            MimeType = "application/pdf",
            Category = "Claim",
            Status = "Ready",
            PageCount = 5,
            ChunkCount = 3,
            EmbeddingProvider = "VoyageAI",
            CreatedAt = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc),
            Chunks = new List<DocumentChunkRecord>
            {
                new()
                {
                    Id = 1, DocumentId = documentId, ChunkIndex = 0,
                    SectionName = "DECLARATIONS", Content = "Workers compensation policy declarations section with employer details and classification codes.",
                    TokenCount = 45
                },
                new()
                {
                    Id = 2, DocumentId = documentId, ChunkIndex = 1,
                    SectionName = "COVERAGE", Content = "Part One - Workers Compensation Insurance: statutory limits per state requirements.",
                    TokenCount = 38
                },
                new()
                {
                    Id = 3, DocumentId = documentId, ChunkIndex = 2,
                    SectionName = "CONDITIONS", Content = "Employer must report all injuries within 30 days of occurrence to maintain coverage eligibility.",
                    TokenCount = 32
                }
            }
        };

        _mockDocumentRepository
            .Setup(r => r.GetDocumentByIdAsync(documentId))
            .ReturnsAsync(document);

        // Act
        var result = await _sut.GetDocumentByIdAsync(documentId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(documentId, result.Id);
        Assert.Equal("workers-comp-claim-WC2024.pdf", result.FileName);
        Assert.Equal("application/pdf", result.MimeType);
        Assert.Equal("Claim", result.Category);
        Assert.Equal("Ready", result.Status);
        Assert.Equal(5, result.PageCount);
        Assert.Equal(3, result.ChunkCount);
        Assert.Equal("VoyageAI", result.EmbeddingProvider);
        Assert.Equal(3, result.Chunks.Count);

        var firstChunk = result.Chunks[0];
        Assert.Equal(0, firstChunk.ChunkIndex);
        Assert.Equal("DECLARATIONS", firstChunk.SectionName);
        Assert.Equal(45, firstChunk.TokenCount);
        Assert.Contains("Workers compensation", firstChunk.ContentPreview);
    }

    [Fact]
    public async Task GetDocumentByIdAsync_NonExistent_ReturnsNull()
    {
        // Arrange
        var nonExistentId = 9999;

        _mockDocumentRepository
            .Setup(r => r.GetDocumentByIdAsync(nonExistentId))
            .ReturnsAsync((DocumentRecord?)null);

        // Act
        var result = await _sut.GetDocumentByIdAsync(nonExistentId);

        // Assert
        Assert.Null(result);
    }

    // ────────────────────────────────────────────────────────────
    // GetDocumentsAsync Tests
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetDocumentsAsync_ReturnsPagedSummaries()
    {
        // Arrange
        var documents = new List<DocumentRecord>
        {
            new()
            {
                Id = 1, FileName = "homeowners-policy-HO2024.pdf", Category = "Policy",
                Status = "Ready", ChunkCount = 8, CreatedAt = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc)
            },
            new()
            {
                Id = 2, FileName = "auto-claim-CLM2024.pdf", Category = "Claim",
                Status = "Ready", ChunkCount = 5, CreatedAt = new DateTime(2026, 1, 12, 0, 0, 0, DateTimeKind.Utc)
            },
            new()
            {
                Id = 3, FileName = "commercial-endorsement-E001.pdf", Category = "Endorsement",
                Status = "Ready", ChunkCount = 3, CreatedAt = new DateTime(2026, 1, 14, 0, 0, 0, DateTimeKind.Utc)
            }
        };

        _mockDocumentRepository
            .Setup(r => r.GetDocumentsAsync("Policy", null, 20, 1))
            .ReturnsAsync((documents.Where(d => d.Category == "Policy").ToList(), 1));

        _mockDocumentRepository
            .Setup(r => r.GetDocumentsAsync(null, null, 10, 1))
            .ReturnsAsync((documents, 3));

        // Act — query with category filter
        var (filteredItems, filteredTotal) = await _sut.GetDocumentsAsync(category: "Policy", pageSize: 20, page: 1);

        // Assert — filtered
        Assert.Single(filteredItems);
        Assert.Equal(1, filteredTotal);
        Assert.Equal("homeowners-policy-HO2024.pdf", filteredItems[0].FileName);
        Assert.Equal("Policy", filteredItems[0].Category);
        Assert.Equal("Ready", filteredItems[0].Status);
        Assert.Equal(8, filteredItems[0].ChunkCount);

        // Act — query without filter
        var (allItems, allTotal) = await _sut.GetDocumentsAsync(category: null, pageSize: 10, page: 1);

        // Assert — all documents
        Assert.Equal(3, allItems.Count);
        Assert.Equal(3, allTotal);
        Assert.All(allItems, item =>
        {
            Assert.False(string.IsNullOrEmpty(item.FileName));
            Assert.Equal("Ready", item.Status);
            Assert.True(item.ChunkCount > 0);
        });
    }

    // ────────────────────────────────────────────────────────────
    // Hybrid RAG (BM25 + RRF Fusion) Tests
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task QueryAsync_UsesHybridRetrieval_WhenBM25Available()
    {
        // Arrange — verify that IHybridRetrievalService.FuseResults is called
        // with both vector and BM25 results during query processing
        var question = "What is the deductible for claim CLM-2024-0789?";
        var queryEmbedding = new float[1024];
        queryEmbedding[0] = 0.3f;

        _mockEmbeddingService
            .Setup(e => e.GenerateEmbeddingAsync(question, "query", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                IsSuccess = true,
                Embedding = queryEmbedding,
                Provider = "VoyageAI",
                TokensUsed = 14
            });

        var chunk1 = new DocumentChunkRecord
        {
            Id = 10, DocumentId = 5, ChunkIndex = 0,
            SectionName = "CLAIMS",
            Content = "Claim CLM-2024-0789 filed for auto collision. Deductible: $500.",
            TokenCount = 35,
            Document = new DocumentRecord { Id = 5, FileName = "auto-claim-report.pdf" }
        };
        var chunk2 = new DocumentChunkRecord
        {
            Id = 11, DocumentId = 5, ChunkIndex = 1,
            SectionName = "COVERAGE",
            Content = "Comprehensive coverage with $250 deductible for non-collision losses.",
            TokenCount = 30,
            Document = new DocumentRecord { Id = 5, FileName = "auto-claim-report.pdf" }
        };

        // Vector search returns top-20 candidates
        _mockDocumentRepository
            .Setup(r => r.SearchSimilarChunksAsync(queryEmbedding, 20, null))
            .ReturnsAsync(new List<(DocumentChunkRecord Chunk, double Similarity)>
            {
                (chunk1, 0.78),
                (chunk2, 0.65)
            });

        // Hybrid retrieval fuses vector + BM25 results
        var fusedResults = new List<(DocumentChunkRecord Chunk, double Score)>
        {
            (chunk1, 0.033), // appears in both vector and BM25 (exact claim ID match)
            (chunk2, 0.016)  // appears only in vector results
        };
        _mockHybridRetrieval
            .Setup(h => h.FuseResults(
                It.IsAny<List<(DocumentChunkRecord Chunk, double Score)>>(),
                It.IsAny<List<(DocumentChunkRecord Chunk, double Score)>>(),
                5))
            .Returns(fusedResults);

        _mockChatCompletion
            .Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatMessageContent>
            {
                new(AuthorRole.Assistant, "The deductible for claim CLM-2024-0789 is $500 [1].")
            });

        // Act
        var result = await _sut.QueryAsync(question);

        // Assert — hybrid retrieval was called with vector + BM25 results
        _mockHybridRetrieval.Verify(
            h => h.FuseResults(
                It.Is<List<(DocumentChunkRecord Chunk, double Score)>>(v => v.Count == 2),
                It.Is<List<(DocumentChunkRecord Chunk, double Score)>>(b => b.Count == 2),
                5),
            Times.Once);

        // Assert — vector search was called with expanded topK of 20
        _mockDocumentRepository.Verify(
            r => r.SearchSimilarChunksAsync(queryEmbedding, 20, null),
            Times.Once);

        Assert.Equal(2, result.Citations.Count);
        Assert.Contains("$500", result.Answer);
    }

    [Fact]
    public async Task QueryAsync_KeywordQueryBM25Boost_ExactPolicyNumberMatchRankedFirst()
    {
        // Arrange — a keyword-heavy query that cosine similarity might miss
        // but BM25 catches due to exact term matching on policy number
        var question = "POL-55555";
        var queryEmbedding = new float[1024];
        queryEmbedding[0] = 0.1f;

        _mockEmbeddingService
            .Setup(e => e.GenerateEmbeddingAsync(question, "query", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                IsSuccess = true,
                Embedding = queryEmbedding,
                Provider = "VoyageAI",
                TokensUsed = 5
            });

        // Chunk with the exact policy number (BM25 should rank this high)
        var exactMatchChunk = new DocumentChunkRecord
        {
            Id = 20, DocumentId = 10, ChunkIndex = 0,
            SectionName = "DECLARATIONS",
            Content = "Policy Number: POL-55555. Insured: [NAME-REDACTED]. Effective: 01/01/2025.",
            TokenCount = 28,
            Document = new DocumentRecord { Id = 10, FileName = "commercial-policy.pdf" }
        };

        // Chunk that is semantically similar but lacks the exact policy number
        var semanticChunk = new DocumentChunkRecord
        {
            Id = 21, DocumentId = 10, ChunkIndex = 1,
            SectionName = "COVERAGE",
            Content = "Commercial general liability coverage with $1M per occurrence limit.",
            TokenCount = 30,
            Document = new DocumentRecord { Id = 10, FileName = "commercial-policy.pdf" }
        };

        // Vector search: semantic chunk ranks higher (cosine similarity)
        _mockDocumentRepository
            .Setup(r => r.SearchSimilarChunksAsync(queryEmbedding, 20, null))
            .ReturnsAsync(new List<(DocumentChunkRecord Chunk, double Similarity)>
            {
                (semanticChunk, 0.45),  // Vector ranks this first
                (exactMatchChunk, 0.30) // Vector ranks this second
            });

        // After RRF fusion: BM25 boosts the exact match chunk to the top
        var fusedResults = new List<(DocumentChunkRecord Chunk, double Score)>
        {
            (exactMatchChunk, 0.033),  // RRF: high BM25 rank + lower vector rank
            (semanticChunk, 0.017)     // RRF: low BM25 rank + higher vector rank
        };
        _mockHybridRetrieval
            .Setup(h => h.FuseResults(
                It.IsAny<List<(DocumentChunkRecord Chunk, double Score)>>(),
                It.IsAny<List<(DocumentChunkRecord Chunk, double Score)>>(),
                5))
            .Returns(fusedResults);

        _mockChatCompletion
            .Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatMessageContent>
            {
                new(AuthorRole.Assistant, "Policy POL-55555 is a commercial general liability policy effective 01/01/2025 [1].")
            });

        // Act
        var result = await _sut.QueryAsync(question);

        // Assert — the exact policy number match should be the first citation
        // (thanks to BM25 boosting via hybrid retrieval)
        Assert.Equal(2, result.Citations.Count);
        Assert.Equal("DECLARATIONS", result.Citations[0].SectionName);
        Assert.Equal(10, result.Citations[0].DocumentId); // exactMatchChunk's DocumentId
        Assert.Contains("POL-55555", result.Answer);

        // Verify hybrid retrieval was invoked
        _mockHybridRetrieval.Verify(
            h => h.FuseResults(
                It.IsAny<List<(DocumentChunkRecord Chunk, double Score)>>(),
                It.IsAny<List<(DocumentChunkRecord Chunk, double Score)>>(),
                5),
            Times.Once);
    }

    // ────────────────────────────────────────────────────────────
    // Content Safety RAG Integration — Upload Screening Tests
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a DocumentIntelligenceService with an optional IContentSafetyService.
    /// The AI Expert agent is adding IContentSafetyService? as a nullable constructor
    /// parameter; this helper lets us test with and without it.
    /// </summary>
    private DocumentIntelligenceService CreateServiceWithContentSafety(
        IContentSafetyService? contentSafety)
    {
        return new DocumentIntelligenceService(
            _mockOcrService.Object,
            _mockChunkingService.Object,
            _mockEmbeddingService.Object,
            _mockDocumentRepository.Object,
            _mockKernelProvider.Object,
            _mockPiiRedactor.Object,
            _mockHybridRetrieval.Object,
            _mockLogger.Object,
            contentSafety);
    }

    /// <summary>
    /// Configures mock services for a standard successful upload pipeline:
    /// OCR extraction -> chunking -> embedding -> repository save.
    /// Returns the list of chunks used for verification.
    /// </summary>
    private List<DocumentChunk> SetupSuccessfulUploadPipeline(
        string extractedText = "DECLARATIONS\nPolicy Number: [POLICY-REDACTED]\n" +
            "Insured: [NAME-REDACTED]\nCOVERAGE\nDwelling Coverage: $350,000\n" +
            "EXCLUSIONS\nFlood damage excluded unless endorsement HO-340 attached.")
    {
        _mockOcrService
            .Setup(o => o.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult
            {
                IsSuccess = true,
                ExtractedText = extractedText,
                PageCount = 2,
                Confidence = 0.94,
                Provider = "PdfPig"
            });

        var chunks = new List<DocumentChunk>
        {
            new() { Index = 0, SectionName = "DECLARATIONS", Content = "Policy Number: [POLICY-REDACTED]. Insured: [NAME-REDACTED]. Effective Date: 01/01/2026.", ApproximateTokens = 30 },
            new() { Index = 1, SectionName = "COVERAGE", Content = "Dwelling Coverage: $350,000. Other Structures: $35,000. Personal Property: $175,000.", ApproximateTokens = 35 },
            new() { Index = 2, SectionName = "EXCLUSIONS", Content = "Flood damage is excluded unless endorsement HO-340 is attached to the policy.", ApproximateTokens = 28 }
        };

        _mockChunkingService
            .Setup(c => c.ChunkDocument(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(chunks);

        _mockEmbeddingService
            .Setup(e => e.GenerateBatchEmbeddingsAsync(
                It.Is<string[]>(texts => texts.Length == 3),
                "document",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BatchEmbeddingResult
            {
                IsSuccess = true,
                Embeddings = new float[][] { new float[1024], new float[1024], new float[1024] },
                Provider = "VoyageAI",
                TotalTokensUsed = 93
            });

        return chunks;
    }

    /// <summary>
    /// Collects all SSE progress events from UploadWithProgressAsync into a list.
    /// </summary>
    private static async Task<List<DocumentProgressEvent>> CollectProgressEvents(
        IAsyncEnumerable<DocumentProgressEvent> stream)
    {
        var events = new List<DocumentProgressEvent>();
        await foreach (var evt in stream)
        {
            events.Add(evt);
        }
        return events;
    }

    [Fact]
    public async Task UploadWithProgress_ScreensChunks_AllSafe()
    {
        // Arrange — content safety returns safe for all chunks
        var mockContentSafety = new Mock<IContentSafetyService>();
        mockContentSafety
            .Setup(cs => cs.AnalyzeTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContentSafetyResult
            {
                IsSuccess = true,
                IsSafe = true,
                Provider = "AzureContentSafety",
                FlaggedCategories = []
            });

        SetupSuccessfulUploadPipeline();
        var sut = CreateServiceWithContentSafety(mockContentSafety.Object);

        // Capture saved chunks for verification
        List<DocumentChunkRecord>? savedChunks = null;
        _mockDocumentRepository
            .Setup(r => r.SaveChunksAsync(It.IsAny<IEnumerable<DocumentChunkRecord>>()))
            .Callback<IEnumerable<DocumentChunkRecord>>(chunks => savedChunks = chunks.ToList())
            .Returns(Task.CompletedTask);

        var fileData = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // %PDF magic bytes

        // Act
        var events = await CollectProgressEvents(
            sut.UploadWithProgressAsync(fileData, "application/pdf", "homeowners-policy-HO2025.pdf", "Policy"));

        // Assert — all chunks marked safe
        Assert.NotNull(savedChunks);
        Assert.Equal(3, savedChunks.Count);
        Assert.All(savedChunks, chunk =>
        {
            Assert.True(chunk.IsSafe, $"Chunk '{chunk.SectionName}' should be safe.");
            Assert.Null(chunk.SafetyFlags);
        });

        // Content safety called once per chunk
        mockContentSafety.Verify(
            cs => cs.AnalyzeTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));

        // Upload completed successfully
        var doneEvent = events.Last();
        Assert.Equal("Done", doneEvent.Phase);
        Assert.Equal(100, doneEvent.Progress);
    }

    [Fact]
    public async Task UploadWithProgress_ScreensChunks_SomeFlagged()
    {
        // Arrange — content safety flags the exclusions chunk with Violence|Hate
        // (e.g., claim narrative describing an assault incident)
        var mockContentSafety = new Mock<IContentSafetyService>();

        // Safe for first two chunks
        mockContentSafety
            .Setup(cs => cs.AnalyzeTextAsync(
                It.Is<string>(t => t.Contains("Policy Number") || t.Contains("Dwelling Coverage")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContentSafetyResult
            {
                IsSuccess = true,
                IsSafe = true,
                Provider = "AzureContentSafety",
                FlaggedCategories = []
            });

        // Flagged for the exclusions chunk (simulating violent claim narrative content)
        mockContentSafety
            .Setup(cs => cs.AnalyzeTextAsync(
                It.Is<string>(t => t.Contains("Flood damage")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContentSafetyResult
            {
                IsSuccess = true,
                IsSafe = false,
                Provider = "AzureContentSafety",
                ViolenceSeverity = 4,
                HateSeverity = 3,
                FlaggedCategories = ["Violence", "Hate"]
            });

        SetupSuccessfulUploadPipeline();
        var sut = CreateServiceWithContentSafety(mockContentSafety.Object);

        List<DocumentChunkRecord>? savedChunks = null;
        _mockDocumentRepository
            .Setup(r => r.SaveChunksAsync(It.IsAny<IEnumerable<DocumentChunkRecord>>()))
            .Callback<IEnumerable<DocumentChunkRecord>>(chunks => savedChunks = chunks.ToList())
            .Returns(Task.CompletedTask);

        var fileData = new byte[] { 0x25, 0x50, 0x44, 0x46 };

        // Act
        var events = await CollectProgressEvents(
            sut.UploadWithProgressAsync(fileData, "application/pdf", "auto-claim-incident-report.pdf", "Claim"));

        // Assert — flagged chunk has IsSafe=false and SafetyFlags set
        Assert.NotNull(savedChunks);
        Assert.Equal(3, savedChunks.Count);

        var declarationsChunk = savedChunks.First(c => c.SectionName == "DECLARATIONS");
        Assert.True(declarationsChunk.IsSafe);
        Assert.Null(declarationsChunk.SafetyFlags);

        var coverageChunk = savedChunks.First(c => c.SectionName == "COVERAGE");
        Assert.True(coverageChunk.IsSafe);
        Assert.Null(coverageChunk.SafetyFlags);

        var exclusionsChunk = savedChunks.First(c => c.SectionName == "EXCLUSIONS");
        Assert.False(exclusionsChunk.IsSafe, "Flagged chunk should have IsSafe=false.");
        Assert.Equal("Violence|Hate", exclusionsChunk.SafetyFlags);

        // Document is still saved (flag, never reject)
        var doneEvent = events.Last();
        Assert.Equal("Done", doneEvent.Phase);
        Assert.NotNull(doneEvent.Result);
        Assert.Equal("Ready", doneEvent.Result.Status);
        Assert.Equal(3, doneEvent.Result.ChunkCount);
    }

    [Fact]
    public async Task UploadWithProgress_ContentSafetyUnavailable_ProceedsWithoutScreening()
    {
        // Arrange — null content safety service (Azure key not configured)
        SetupSuccessfulUploadPipeline();
        var sut = CreateServiceWithContentSafety(contentSafety: null);

        List<DocumentChunkRecord>? savedChunks = null;
        _mockDocumentRepository
            .Setup(r => r.SaveChunksAsync(It.IsAny<IEnumerable<DocumentChunkRecord>>()))
            .Callback<IEnumerable<DocumentChunkRecord>>(chunks => savedChunks = chunks.ToList())
            .Returns(Task.CompletedTask);

        var fileData = new byte[] { 0x25, 0x50, 0x44, 0x46 };

        // Act
        var events = await CollectProgressEvents(
            sut.UploadWithProgressAsync(fileData, "application/pdf", "liability-endorsement-LE2025.pdf", "Endorsement"));

        // Assert — upload succeeds, all chunks default to IsSafe=true
        Assert.NotNull(savedChunks);
        Assert.Equal(3, savedChunks.Count);
        Assert.All(savedChunks, chunk =>
        {
            Assert.True(chunk.IsSafe, "Chunks should default to IsSafe=true when content safety is unavailable.");
            Assert.Null(chunk.SafetyFlags);
        });

        var doneEvent = events.Last();
        Assert.Equal("Done", doneEvent.Phase);
        Assert.Equal("Ready", doneEvent.Result!.Status);
    }

    [Fact]
    public async Task UploadWithProgress_ContentSafetyThrows_ProceedsGracefully()
    {
        // Arrange — content safety throws an exception (Azure service outage)
        var mockContentSafety = new Mock<IContentSafetyService>();
        mockContentSafety
            .Setup(cs => cs.AnalyzeTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Azure Content Safety endpoint unreachable (503 Service Unavailable)"));

        SetupSuccessfulUploadPipeline();
        var sut = CreateServiceWithContentSafety(mockContentSafety.Object);

        List<DocumentChunkRecord>? savedChunks = null;
        _mockDocumentRepository
            .Setup(r => r.SaveChunksAsync(It.IsAny<IEnumerable<DocumentChunkRecord>>()))
            .Callback<IEnumerable<DocumentChunkRecord>>(chunks => savedChunks = chunks.ToList())
            .Returns(Task.CompletedTask);

        var fileData = new byte[] { 0x25, 0x50, 0x44, 0x46 };

        // Act — should NOT throw; content safety failure is non-blocking
        var events = await CollectProgressEvents(
            sut.UploadWithProgressAsync(fileData, "application/pdf", "workers-comp-WC2025-renewal.pdf", "Policy"));

        // Assert — upload still succeeds, chunks default to safe
        Assert.NotNull(savedChunks);
        Assert.Equal(3, savedChunks.Count);
        Assert.All(savedChunks, chunk =>
        {
            Assert.True(chunk.IsSafe, "Chunks should default to IsSafe=true when content safety throws.");
            Assert.Null(chunk.SafetyFlags);
        });

        var doneEvent = events.Last();
        Assert.Equal("Done", doneEvent.Phase);
        Assert.Equal("Ready", doneEvent.Result!.Status);
    }

    [Fact]
    public async Task UploadWithProgress_EmitsSafetyPhase()
    {
        // Arrange — content safety available and returns safe
        var mockContentSafety = new Mock<IContentSafetyService>();
        mockContentSafety
            .Setup(cs => cs.AnalyzeTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContentSafetyResult
            {
                IsSuccess = true,
                IsSafe = true,
                Provider = "AzureContentSafety",
                FlaggedCategories = []
            });

        SetupSuccessfulUploadPipeline();
        var sut = CreateServiceWithContentSafety(mockContentSafety.Object);

        _mockDocumentRepository
            .Setup(r => r.SaveChunksAsync(It.IsAny<IEnumerable<DocumentChunkRecord>>()))
            .Returns(Task.CompletedTask);

        var fileData = new byte[] { 0x25, 0x50, 0x44, 0x46 };

        // Act
        var events = await CollectProgressEvents(
            sut.UploadWithProgressAsync(fileData, "application/pdf", "commercial-property-CP2025.pdf", "Policy"));

        // Assert — SSE stream includes a "Safety" phase event
        var safetyEvents = events.Where(e => e.Phase == "Safety").ToList();
        Assert.NotEmpty(safetyEvents);
        Assert.Contains(safetyEvents, e => e.Message.Contains("Safety", StringComparison.OrdinalIgnoreCase)
            || e.Message.Contains("screen", StringComparison.OrdinalIgnoreCase)
            || e.Message.Contains("content", StringComparison.OrdinalIgnoreCase)
            || e.Progress > 0);

        // Verify complete phase sequence: Uploading -> OCR -> Chunking -> Embedding -> Safety -> Done
        var phases = events.Select(e => e.Phase).Distinct().ToList();
        Assert.Contains("Uploading", phases);
        Assert.Contains("OCR", phases);
        Assert.Contains("Chunking", phases);
        Assert.Contains("Embedding", phases);
        Assert.Contains("Safety", phases);
        Assert.Contains("Done", phases);
    }

    // ────────────────────────────────────────────────────────────
    // Content Safety RAG Integration — Query Screening Tests
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Configures mock services for a standard successful query pipeline:
    /// embedding -> vector search -> hybrid retrieval -> LLM answer.
    /// Returns the expected LLM answer string.
    /// </summary>
    private string SetupSuccessfulQueryPipeline(string question)
    {
        var queryEmbedding = new float[1024];
        queryEmbedding[0] = 0.5f;

        _mockEmbeddingService
            .Setup(e => e.GenerateEmbeddingAsync(question, "query", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                IsSuccess = true,
                Embedding = queryEmbedding,
                Provider = "VoyageAI",
                TokensUsed = 15
            });

        var chunk = new DocumentChunkRecord
        {
            Id = 1,
            DocumentId = 42,
            ChunkIndex = 0,
            SectionName = "COVERAGE",
            Content = "Dwelling Coverage: $350,000. Personal Property: $175,000. Liability: $300,000.",
            TokenCount = 30,
            Document = new DocumentRecord { Id = 42, FileName = "homeowners-policy-HO2025.pdf" }
        };

        _mockDocumentRepository
            .Setup(r => r.SearchSimilarChunksAsync(queryEmbedding, 20, null))
            .ReturnsAsync(new List<(DocumentChunkRecord Chunk, double Similarity)>
            {
                (chunk, 0.90)
            });

        _mockHybridRetrieval
            .Setup(h => h.FuseResults(
                It.IsAny<List<(DocumentChunkRecord Chunk, double Score)>>(),
                It.IsAny<List<(DocumentChunkRecord Chunk, double Score)>>(),
                5))
            .Returns(new List<(DocumentChunkRecord Chunk, double Score)> { (chunk, 0.033) });

        var llmAnswer = "The dwelling coverage amount is $350,000 per the declarations section [1].";
        _mockChatCompletion
            .Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatMessageContent>
            {
                new(AuthorRole.Assistant, llmAnswer)
            });

        return llmAnswer;
    }

    [Fact]
    public async Task QueryDocuments_SafeAnswer_NoWarning()
    {
        // Arrange — content safety screens the LLM answer and returns safe
        var mockContentSafety = new Mock<IContentSafetyService>();
        mockContentSafety
            .Setup(cs => cs.AnalyzeTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContentSafetyResult
            {
                IsSuccess = true,
                IsSafe = true,
                Provider = "AzureContentSafety",
                FlaggedCategories = []
            });

        var question = "What is the dwelling coverage for my homeowners policy?";
        var expectedAnswer = SetupSuccessfulQueryPipeline(question);
        var sut = CreateServiceWithContentSafety(mockContentSafety.Object);

        // Act
        var result = await sut.QueryAsync(question);

        // Assert — answer unchanged (no warning prepended)
        Assert.Equal(expectedAnswer, result.Answer);

        // AnswerSafety populated and safe
        Assert.NotNull(result.AnswerSafety);
        Assert.True(result.AnswerSafety.IsSafe);
        Assert.Empty(result.AnswerSafety.FlaggedCategories);
        Assert.Equal("AzureContentSafety", result.AnswerSafety.Provider);

        // Content safety was called once (for the LLM answer)
        mockContentSafety.Verify(
            cs => cs.AnalyzeTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task QueryDocuments_FlaggedAnswer_PrependsWarning()
    {
        // Arrange — content safety flags the LLM answer (e.g., violent claim description)
        var mockContentSafety = new Mock<IContentSafetyService>();
        mockContentSafety
            .Setup(cs => cs.AnalyzeTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContentSafetyResult
            {
                IsSuccess = true,
                IsSafe = false,
                Provider = "AzureContentSafety",
                ViolenceSeverity = 4,
                SelfHarmSeverity = 3,
                FlaggedCategories = ["Violence", "SelfHarm"]
            });

        var question = "Describe the injuries from the workers compensation claim WC-2025-1234.";
        var expectedAnswer = SetupSuccessfulQueryPipeline(question);
        var sut = CreateServiceWithContentSafety(mockContentSafety.Object);

        // Act
        var result = await sut.QueryAsync(question);

        // Assert — warning prepended to the answer
        Assert.StartsWith("[Content Warning:", result.Answer);
        Assert.Contains(expectedAnswer, result.Answer);

        // AnswerSafety populated and flagged
        Assert.NotNull(result.AnswerSafety);
        Assert.False(result.AnswerSafety.IsSafe);
        Assert.Contains("Violence", result.AnswerSafety.FlaggedCategories);
        Assert.Contains("SelfHarm", result.AnswerSafety.FlaggedCategories);
        Assert.Equal("AzureContentSafety", result.AnswerSafety.Provider);
    }

    [Fact]
    public async Task QueryDocuments_ContentSafetyNull_AnswerSafetyNull()
    {
        // Arrange — no content safety service (null injection)
        var question = "What exclusions apply to the commercial property policy?";
        SetupSuccessfulQueryPipeline(question);
        var sut = CreateServiceWithContentSafety(contentSafety: null);

        // Act
        var result = await sut.QueryAsync(question);

        // Assert — AnswerSafety is null when content safety service is unavailable
        Assert.Null(result.AnswerSafety);

        // Answer is still returned normally
        Assert.NotNull(result.Answer);
        Assert.NotEmpty(result.Answer);
        Assert.NotEmpty(result.Citations);
    }
}
