using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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

namespace SentimentAnalyzer.Tests;

/// <summary>
/// Unit tests for SyntheticQAService — generates synthetic Q&amp;A pairs from indexed
/// insurance document chunks for fine-tuning preparation. Tests cover generation,
/// retrieval, PII redaction, LLM failure resilience, and category parsing.
/// </summary>
public class SyntheticQAServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly InsuranceAnalysisDbContext _db;
    private readonly Mock<IResilientKernelProvider> _mockKernelProvider;
    private readonly Mock<IChatCompletionService> _mockChatCompletion;
    private readonly Mock<IPIIRedactor> _mockPiiRedactor;
    private readonly Mock<ILogger<SyntheticQAService>> _mockLogger;
    private readonly SyntheticQAService _sut;

    /// <summary>
    /// Realistic insurance chunk content for Coverage Details section.
    /// </summary>
    private const string CoverageChunkContent =
        "Coverage Details: The insured property located at 4521 Maple Ridge Drive is covered for direct " +
        "physical loss up to $500,000 per occurrence. The deductible is $2,500 per claim. Personal property " +
        "coverage extends to $250,000 with a replacement cost valuation.";

    /// <summary>
    /// Realistic insurance chunk content for Claims Procedure section.
    /// </summary>
    private const string ClaimsProcedureChunkContent =
        "Claims Procedure: To file a claim, contact the claims department within 72 hours of the loss event. " +
        "Provide policy number, date of loss, description of damage, and estimated repair costs. An adjuster " +
        "will be assigned within 48 hours. All supporting documentation must be submitted within 30 days.";

    /// <summary>
    /// Realistic insurance chunk content for Subrogation section.
    /// </summary>
    private const string SubrogationChunkContent =
        "Subrogation: The insurer reserves the right to pursue recovery from any third party responsible for " +
        "the covered loss. The policyholder agrees to cooperate fully with subrogation efforts and shall not " +
        "waive any rights against liable parties without prior written consent from the insurer.";

    /// <summary>
    /// Well-formed JSON response from the LLM for coverage chunk Q&amp;A generation.
    /// </summary>
    private const string ValidLlmJsonResponse = """
        [
            {
                "question": "What is the maximum coverage amount per occurrence for the insured property?",
                "answer": "The insured property is covered for direct physical loss up to $500,000 per occurrence.",
                "category": "factual",
                "confidence": 0.95
            },
            {
                "question": "What is the deductible amount per claim?",
                "answer": "The deductible is $2,500 per claim.",
                "category": "factual",
                "confidence": 0.92
            },
            {
                "question": "If a policyholder experiences a total loss of personal property, what is the maximum reimbursement they can expect?",
                "answer": "Personal property coverage extends to $250,000 with a replacement cost valuation, so the maximum reimbursement would be $250,000 at replacement cost.",
                "category": "inferential",
                "confidence": 0.88
            }
        ]
        """;

    /// <summary>
    /// Well-formed JSON response covering all three Q&amp;A categories.
    /// </summary>
    private const string AllCategoriesLlmJsonResponse = """
        [
            {
                "question": "What is the per-occurrence coverage limit?",
                "answer": "The coverage limit is $500,000 per occurrence.",
                "category": "factual",
                "confidence": 0.95
            },
            {
                "question": "If both the dwelling and personal property are damaged in the same event, could the total claim exceed $500,000?",
                "answer": "Yes, the dwelling coverage ($500,000) and personal property coverage ($250,000) are separate limits, so a combined claim could total up to $750,000.",
                "category": "inferential",
                "confidence": 0.85
            },
            {
                "question": "What steps should a policyholder take immediately after discovering property damage?",
                "answer": "The policyholder should contact the claims department within 72 hours, provide the policy number and date of loss, describe the damage, and submit estimated repair costs.",
                "category": "procedural",
                "confidence": 0.91
            }
        ]
        """;

    public SyntheticQAServiceTests()
    {
        // Setup in-memory SQLite for DbContext (same pattern as DocumentRepositoryTests)
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<InsuranceAnalysisDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new InsuranceAnalysisDbContext(options);
        _db.Database.EnsureCreated();

        _mockKernelProvider = new Mock<IResilientKernelProvider>();
        _mockChatCompletion = new Mock<IChatCompletionService>();
        _mockPiiRedactor = new Mock<IPIIRedactor>();
        _mockLogger = new Mock<ILogger<SyntheticQAService>>();

        // Default PII redaction: pass-through (no PII in default test data)
        _mockPiiRedactor.Setup(p => p.Redact(It.IsAny<string>())).Returns<string>(s => s);

        // Build a test Kernel with the mocked IChatCompletionService
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(_mockChatCompletion.Object);
        var kernel = builder.Build();

        _mockKernelProvider.Setup(p => p.GetKernel()).Returns(kernel);
        _mockKernelProvider.Setup(p => p.ActiveProviderName).Returns("Groq");

        // SyntheticQAService uses DbContext directly (no IDocumentRepository)
        _sut = new SyntheticQAService(
            _db,
            _mockKernelProvider.Object,
            _mockPiiRedactor.Object,
            _mockLogger.Object);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    // ────────────────────────────────────────────────────────────
    // Helper Methods
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Seeds a realistic homeowners insurance document with chunks into the in-memory DB.
    /// Returns the document record for assertions.
    /// </summary>
    private async Task<DocumentRecord> SeedDocumentWithChunksAsync(
        int documentId = 42,
        string fileName = "homeowners-policy-POL-2024-INS-7789.pdf",
        string status = "Ready",
        List<DocumentChunkRecord>? customChunks = null)
    {
        var document = new DocumentRecord
        {
            Id = documentId,
            FileName = fileName,
            MimeType = "application/pdf",
            Category = "Policy",
            ExtractedText = CoverageChunkContent + "\n\n" + ClaimsProcedureChunkContent,
            PageCount = 5,
            EmbeddingProvider = "VoyageAI",
            EmbeddingDimensions = 1024,
            Status = status
        };

        _db.Documents.Add(document);
        await _db.SaveChangesAsync();

        var chunks = customChunks ?? CreateDefaultChunks(documentId);
        if (chunks.Count > 0)
        {
            _db.DocumentChunks.AddRange(chunks);
            await _db.SaveChangesAsync();
        }

        // Update chunk count on the document
        document.ChunkCount = chunks.Count;
        await _db.SaveChangesAsync();

        return document;
    }

    /// <summary>
    /// Creates a list of realistic insurance document chunk records.
    /// None are parent chunks with children, so all are eligible for Q&amp;A generation.
    /// </summary>
    private static List<DocumentChunkRecord> CreateDefaultChunks(int documentId)
    {
        return
        [
            new DocumentChunkRecord
            {
                DocumentId = documentId,
                ChunkIndex = 0,
                SectionName = "COVERAGE",
                Content = CoverageChunkContent,
                TokenCount = CoverageChunkContent.Length / 4,
                PageNumber = 1,
                ChunkLevel = 0,
                IsSafe = true
            },
            new DocumentChunkRecord
            {
                DocumentId = documentId,
                ChunkIndex = 1,
                SectionName = "CLAIMS PROCEDURE",
                Content = ClaimsProcedureChunkContent,
                TokenCount = ClaimsProcedureChunkContent.Length / 4,
                PageNumber = 3,
                ChunkLevel = 0,
                IsSafe = true
            },
            new DocumentChunkRecord
            {
                DocumentId = documentId,
                ChunkIndex = 2,
                SectionName = "SUBROGATION",
                Content = SubrogationChunkContent,
                TokenCount = SubrogationChunkContent.Length / 4,
                PageNumber = 4,
                ChunkLevel = 0,
                IsSafe = true
            }
        ];
    }

    /// <summary>
    /// Sets up the mock LLM to return a specific JSON response for any chat completion call.
    /// Uses GetChatMessageContentAsync (singular) matching the actual service implementation.
    /// </summary>
    private void SetupLlmResponse(string jsonResponse)
    {
        _mockChatCompletion
            .Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatMessageContent>
            {
                new(AuthorRole.Assistant, jsonResponse)
            });
    }

    // ────────────────────────────────────────────────────────────
    // Test 1: GenerateQAPairs_ValidDocument_GeneratesPairs
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateQAPairs_ValidDocument_GeneratesPairs()
    {
        // Arrange — document with 3 chunks from a homeowners insurance policy
        await SeedDocumentWithChunksAsync();
        SetupLlmResponse(ValidLlmJsonResponse);

        // Act
        var result = await _sut.GenerateQAPairsAsync(42);

        // Assert — pairs should be generated and returned
        Assert.NotNull(result);
        Assert.Equal(42, result.DocumentId);
        Assert.Equal("homeowners-policy-POL-2024-INS-7789.pdf", result.DocumentName);
        Assert.Null(result.ErrorMessage);
        Assert.True(result.TotalPairsGenerated > 0, "Expected at least one Q&A pair to be generated.");
        Assert.True(result.Pairs.Count > 0, "Pairs list should not be empty for a valid document.");
        Assert.True(result.ElapsedMilliseconds >= 0, "ElapsedMilliseconds should be non-negative.");
        Assert.False(string.IsNullOrEmpty(result.LlmProvider), "LlmProvider should be populated.");

        // Verify each pair has required fields populated
        foreach (var pair in result.Pairs)
        {
            Assert.False(string.IsNullOrWhiteSpace(pair.Question), "Q&A pair question must not be empty.");
            Assert.False(string.IsNullOrWhiteSpace(pair.Answer), "Q&A pair answer must not be empty.");
            Assert.Contains(pair.Category, new[] { "factual", "inferential", "procedural" });
            Assert.InRange(pair.Confidence, 0.0, 1.0);
        }

        // Verify LLM was called (once per eligible chunk = 3)
        _mockChatCompletion.Verify(
            c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(3));

        // Verify pairs were persisted to DB
        var savedPairs = await _db.DocumentQAPairs.Where(p => p.DocumentId == 42).ToListAsync();
        Assert.True(savedPairs.Count > 0, "Q&A pairs should be saved to the database.");
    }

    // ────────────────────────────────────────────────────────────
    // Test 2: GenerateQAPairs_DocumentNotFound_ReturnsError
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateQAPairs_DocumentNotFound_ReturnsError()
    {
        // Arrange — no document exists for ID 999 (DB is empty)

        // Act
        var result = await _sut.GenerateQAPairsAsync(999);

        // Assert — error message should indicate document not found
        Assert.NotNull(result);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("not found", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, result.TotalPairsGenerated);
        Assert.Empty(result.Pairs);

        // LLM should never be called when document doesn't exist
        _mockChatCompletion.Verify(
            c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ────────────────────────────────────────────────────────────
    // Test 3: GenerateQAPairs_PIIRedactedBeforeLLMCall
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateQAPairs_PIIRedactedBeforeLLMCall()
    {
        // Arrange — chunk contains PII (SSN and email)
        var piiChunkContent =
            "Policyholder Jane Doe, SSN 987-65-4321, email jane.doe@example.com, " +
            "filed claim CLM-2024-88901 for water damage to the insured dwelling.";
        var redactedChunkContent =
            "Policyholder [NAME-REDACTED], SSN [SSN-REDACTED], email [EMAIL-REDACTED], " +
            "filed claim [CLAIM-REDACTED] for water damage to the insured dwelling.";

        var piiChunks = new List<DocumentChunkRecord>
        {
            new()
            {
                DocumentId = 42,
                ChunkIndex = 0,
                SectionName = "CLAIMS PROCEDURE",
                Content = piiChunkContent,
                TokenCount = piiChunkContent.Length / 4,
                PageNumber = 1,
                ChunkLevel = 0,
                IsSafe = true
            }
        };

        await SeedDocumentWithChunksAsync(customChunks: piiChunks);

        // PII redactor transforms the PII-laden content
        _mockPiiRedactor
            .Setup(p => p.Redact(piiChunkContent))
            .Returns(redactedChunkContent);

        // Capture the prompt sent to LLM to verify PII was redacted
        string? capturedPrompt = null;
        _mockChatCompletion
            .Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(),
                It.IsAny<CancellationToken>()))
            .Callback<ChatHistory, PromptExecutionSettings?, Kernel?, CancellationToken>((history, _, _, _) =>
            {
                // Capture the user message that contains the chunk content
                capturedPrompt = history.LastOrDefault(m => m.Role == AuthorRole.User)?.Content;
            })
            .ReturnsAsync(new List<ChatMessageContent>
            {
                new(AuthorRole.Assistant, ValidLlmJsonResponse)
            });

        // Act
        var result = await _sut.GenerateQAPairsAsync(42);

        // Assert — PII redactor must be called before LLM invocation
        _mockPiiRedactor.Verify(p => p.Redact(piiChunkContent), Times.Once);

        // Verify the LLM received redacted content, not raw PII
        Assert.NotNull(capturedPrompt);
        Assert.DoesNotContain("987-65-4321", capturedPrompt); // SSN should be redacted
        Assert.DoesNotContain("jane.doe@example.com", capturedPrompt); // Email should be redacted
        Assert.Contains("[SSN-REDACTED]", capturedPrompt); // Redaction placeholders should be present
    }

    // ────────────────────────────────────────────────────────────
    // Test 4: GenerateQAPairs_LLMFailsForOneChunk_ContinuesWithOthers
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateQAPairs_LLMFailsForOneChunk_ContinuesWithOthers()
    {
        // Arrange — 3 chunks: LLM succeeds for #1 and #3, throws for #2
        await SeedDocumentWithChunksAsync();

        var callCount = 0;
        _mockChatCompletion
            .Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 2)
                {
                    // Second chunk fails — simulate LLM provider error
                    throw new HttpRequestException("Groq rate limit exceeded (429 Too Many Requests)");
                }
                return new List<ChatMessageContent>
                {
                    new(AuthorRole.Assistant, ValidLlmJsonResponse)
                };
            });

        // Act
        var result = await _sut.GenerateQAPairsAsync(42);

        // Assert — result should contain pairs from the 2 successful chunks
        Assert.NotNull(result);
        Assert.True(result.TotalPairsGenerated > 0, "Should have pairs from the successful chunks.");

        // When some chunks fail, service reports partial failure in ErrorMessage
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("failed", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        // LLM should have been called for all 3 chunks (even though one failed)
        _mockChatCompletion.Verify(
            c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(3));

        // Pairs from the 2 successful chunks should be persisted
        var savedPairs = await _db.DocumentQAPairs.Where(p => p.DocumentId == 42).ToListAsync();
        Assert.True(savedPairs.Count > 0, "Pairs from successful chunks should still be saved.");
    }

    // ────────────────────────────────────────────────────────────
    // Test 5: GenerateQAPairs_EmptyChunks_ReturnsEmptyResult
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateQAPairs_EmptyChunks_ReturnsEmptyResult()
    {
        // Arrange — document exists but has no chunks
        await SeedDocumentWithChunksAsync(customChunks: []);

        // Act
        var result = await _sut.GenerateQAPairsAsync(42);

        // Assert — zero pairs, with informational message about no eligible chunks
        Assert.NotNull(result);
        Assert.Equal(0, result.TotalPairsGenerated);
        Assert.Empty(result.Pairs);
        // Service returns an informational error when no eligible chunks exist
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("eligible", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        // LLM should never be called when there are no chunks
        _mockChatCompletion.Verify(
            c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ────────────────────────────────────────────────────────────
    // Test 6: GetQAPairs_ExistingPairs_ReturnsPairs
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetQAPairs_ExistingPairs_ReturnsPairs()
    {
        // Arrange — seed DB with document, chunk, and existing Q&A pairs
        var document = await SeedDocumentWithChunksAsync();

        // Get the chunk ID that was auto-assigned by EF
        var chunkId = await _db.DocumentChunks
            .Where(c => c.DocumentId == 42 && c.SectionName == "COVERAGE")
            .Select(c => c.Id)
            .FirstAsync();

        var existingPairs = new List<DocumentQAPairRecord>
        {
            new()
            {
                DocumentId = 42,
                ChunkId = chunkId,
                Question = "What is the coverage limit per occurrence?",
                Answer = "The coverage limit is $500,000 per occurrence.",
                Category = "factual",
                Confidence = 0.95,
                LlmProvider = "Groq"
            },
            new()
            {
                DocumentId = 42,
                ChunkId = chunkId,
                Question = "What is the deductible per claim?",
                Answer = "The deductible is $2,500 per claim.",
                Category = "factual",
                Confidence = 0.92,
                LlmProvider = "Groq"
            }
        };
        _db.DocumentQAPairs.AddRange(existingPairs);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetQAPairsAsync(42);

        // Assert — pairs should be returned
        Assert.NotNull(result);
        Assert.Equal(42, result.DocumentId);
        Assert.Equal("homeowners-policy-POL-2024-INS-7789.pdf", result.DocumentName);
        Assert.Equal(2, result.TotalPairsGenerated);
        Assert.Equal(2, result.Pairs.Count);
        Assert.Null(result.ErrorMessage);

        // Verify pair content
        var firstPair = result.Pairs.First();
        Assert.Equal("What is the coverage limit per occurrence?", firstPair.Question);
        Assert.Equal("The coverage limit is $500,000 per occurrence.", firstPair.Answer);
        Assert.Equal("factual", firstPair.Category);
        Assert.Equal("COVERAGE", firstPair.SectionName);
    }

    // ────────────────────────────────────────────────────────────
    // Test 7: GetQAPairs_NoPairs_ReturnsEmptyResult
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetQAPairs_NoPairs_ReturnsEmptyResult()
    {
        // Arrange — document exists with chunks but no Q&A pairs generated yet
        await SeedDocumentWithChunksAsync();

        // Act
        var result = await _sut.GetQAPairsAsync(42);

        // Assert — empty but valid result
        Assert.NotNull(result);
        Assert.Equal(42, result.DocumentId);
        Assert.Equal("homeowners-policy-POL-2024-INS-7789.pdf", result.DocumentName);
        Assert.Equal(0, result.TotalPairsGenerated);
        Assert.Empty(result.Pairs);
        Assert.Null(result.ErrorMessage);
    }

    // ────────────────────────────────────────────────────────────
    // Test 8: GetQAPairs_DocumentNotFound_ReturnsError
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetQAPairs_DocumentNotFound_ReturnsError()
    {
        // Arrange — no document in DB for ID 999

        // Act
        var result = await _sut.GetQAPairsAsync(999);

        // Assert — error message should indicate document not found
        Assert.NotNull(result);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("not found", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, result.TotalPairsGenerated);
        Assert.Empty(result.Pairs);
    }

    // ────────────────────────────────────────────────────────────
    // Test 9: GenerateQAPairs_MalformedLLMResponse_HandlesGracefully
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateQAPairs_MalformedLLMResponse_HandlesGracefully()
    {
        // Arrange — single chunk, LLM returns non-JSON gibberish
        var singleChunk = new List<DocumentChunkRecord>
        {
            new()
            {
                DocumentId = 42,
                ChunkIndex = 0,
                SectionName = "COVERAGE",
                Content = CoverageChunkContent,
                TokenCount = CoverageChunkContent.Length / 4,
                PageNumber = 1,
                ChunkLevel = 0,
                IsSafe = true
            }
        };
        await SeedDocumentWithChunksAsync(customChunks: singleChunk);

        // LLM returns non-JSON text (refusal / hallucination)
        _mockChatCompletion
            .Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatMessageContent>
            {
                new(AuthorRole.Assistant,
                    "I'm sorry, I cannot generate Q&A pairs for this content as it appears to be " +
                    "a standard insurance policy document. Please provide more specific instructions.")
            });

        // Act — should NOT throw; handles malformed response gracefully
        var result = await _sut.GenerateQAPairsAsync(42);

        // Assert — zero pairs generated but no crash
        Assert.NotNull(result);
        Assert.Equal(0, result.TotalPairsGenerated);
        Assert.Empty(result.Pairs);

        // No pairs saved to DB either
        var dbPairs = await _db.DocumentQAPairs.Where(p => p.DocumentId == 42).ToListAsync();
        Assert.Empty(dbPairs);
    }

    // ────────────────────────────────────────────────────────────
    // Test 10: GenerateQAPairs_CategoriesAreParsedCorrectly
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateQAPairs_CategoriesAreParsedCorrectly()
    {
        // Arrange — single chunk, LLM returns Q&A pairs with all three category types
        var singleChunk = new List<DocumentChunkRecord>
        {
            new()
            {
                DocumentId = 42,
                ChunkIndex = 0,
                SectionName = "COVERAGE",
                Content = CoverageChunkContent + " " + ClaimsProcedureChunkContent,
                TokenCount = (CoverageChunkContent.Length + ClaimsProcedureChunkContent.Length) / 4,
                PageNumber = 1,
                ChunkLevel = 0,
                IsSafe = true
            }
        };
        await SeedDocumentWithChunksAsync(customChunks: singleChunk);

        // LLM returns one pair of each category
        SetupLlmResponse(AllCategoriesLlmJsonResponse);

        // Act
        var result = await _sut.GenerateQAPairsAsync(42);

        // Assert — all three categories should be present
        Assert.NotNull(result);
        Assert.True(result.TotalPairsGenerated >= 3, $"Expected at least 3 pairs, got {result.TotalPairsGenerated}.");

        var categories = result.Pairs.Select(p => p.Category).Distinct().ToList();
        Assert.Contains("factual", categories);
        Assert.Contains("inferential", categories);
        Assert.Contains("procedural", categories);

        // Verify specific category assignments
        var factualPair = result.Pairs.First(p => p.Category == "factual");
        Assert.Contains("coverage limit", factualPair.Question, StringComparison.OrdinalIgnoreCase);

        var inferentialPair = result.Pairs.First(p => p.Category == "inferential");
        Assert.Contains("exceed", inferentialPair.Question, StringComparison.OrdinalIgnoreCase);

        var proceduralPair = result.Pairs.First(p => p.Category == "procedural");
        Assert.Contains("steps", proceduralPair.Question, StringComparison.OrdinalIgnoreCase);

        // Verify confidence scores are within valid range for all pairs
        Assert.All(result.Pairs, pair =>
        {
            Assert.InRange(pair.Confidence, 0.0, 1.0);
        });

        // Verify section name propagation from source chunk
        Assert.All(result.Pairs, pair =>
        {
            Assert.Equal("COVERAGE", pair.SectionName);
        });

        // Verify pairs persisted to DB with correct categories
        var dbPairs = await _db.DocumentQAPairs.Where(p => p.DocumentId == 42).ToListAsync();
        Assert.True(dbPairs.Count >= 3, "All category pairs should be saved to the database.");
        var dbCategories = dbPairs.Select(p => p.Category).Distinct().ToList();
        Assert.Contains("factual", dbCategories);
        Assert.Contains("inferential", dbCategories);
        Assert.Contains("procedural", dbCategories);
    }
}
