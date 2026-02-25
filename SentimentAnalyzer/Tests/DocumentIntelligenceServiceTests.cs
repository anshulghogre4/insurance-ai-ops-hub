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

        var chunkResults = new List<(DocumentChunkRecord Chunk, double Similarity)>
        {
            (new DocumentChunkRecord
            {
                Id = 1,
                DocumentId = 42,
                ChunkIndex = 0,
                SectionName = "DECLARATIONS",
                Content = "Dwelling Coverage: $350,000. Personal Property: $175,000.",
                TokenCount = 30,
                Document = new DocumentRecord { Id = 42, FileName = "homeowners-policy-HO2024.pdf" }
            }, 0.92),
            (new DocumentChunkRecord
            {
                Id = 2,
                DocumentId = 42,
                ChunkIndex = 1,
                SectionName = "COVERAGE",
                Content = "Coverage A - Dwelling covers the structure up to the stated limit.",
                TokenCount = 28,
                Document = new DocumentRecord { Id = 42, FileName = "homeowners-policy-HO2024.pdf" }
            }, 0.85)
        };

        _mockDocumentRepository
            .Setup(r => r.SearchSimilarChunksAsync(queryEmbedding, 5, null))
            .ReturnsAsync(chunkResults);

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
        Assert.Equal(0.92, firstCitation.Similarity);
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
            .Setup(r => r.SearchSimilarChunksAsync(queryEmbedding, 5, null))
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

        var chunkResults = new List<(DocumentChunkRecord Chunk, double Similarity)>
        {
            (new DocumentChunkRecord
            {
                Id = 1, DocumentId = 42, ChunkIndex = 0,
                SectionName = "COVERAGE",
                Content = "Dwelling Coverage: $350,000 for the insured property.",
                TokenCount = 25,
                Document = new DocumentRecord { Id = 42, FileName = "policy.pdf" }
            }, 0.88)
        };

        _mockDocumentRepository
            .Setup(r => r.SearchSimilarChunksAsync(queryEmbedding, 5, null))
            .ReturnsAsync(chunkResults);

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
        // Arrange — return chunks with mixed similarity scores, some below 0.3 threshold
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

        var chunkResults = new List<(DocumentChunkRecord Chunk, double Similarity)>
        {
            (new DocumentChunkRecord
            {
                Id = 1, DocumentId = 42, ChunkIndex = 0,
                SectionName = "COVERAGE",
                Content = "Dwelling coverage: $350,000.",
                TokenCount = 20,
                Document = new DocumentRecord { Id = 42, FileName = "policy.pdf" }
            }, 0.85),
            (new DocumentChunkRecord
            {
                Id = 2, DocumentId = 42, ChunkIndex = 1,
                SectionName = "DECLARATIONS",
                Content = "Policy period: 01/01/2025 to 01/01/2026.",
                TokenCount = 20,
                Document = new DocumentRecord { Id = 42, FileName = "policy.pdf" }
            }, 0.15),  // Below 0.3 threshold — should be filtered out
            (new DocumentChunkRecord
            {
                Id = 3, DocumentId = 42, ChunkIndex = 2,
                SectionName = "CONDITIONS",
                Content = "Random unrelated content.",
                TokenCount = 15,
                Document = new DocumentRecord { Id = 42, FileName = "policy.pdf" }
            }, 0.10)   // Below 0.3 threshold — should be filtered out
        };

        _mockDocumentRepository
            .Setup(r => r.SearchSimilarChunksAsync(queryEmbedding, 5, null))
            .ReturnsAsync(chunkResults);

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
        Assert.Equal(0.85, result.Citations[0].Similarity);
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
}
