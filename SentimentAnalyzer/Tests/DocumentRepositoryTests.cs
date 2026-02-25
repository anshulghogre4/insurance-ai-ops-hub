using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SentimentAnalyzer.API.Data;
using SentimentAnalyzer.API.Data.Entities;
using Xunit;

namespace SentimentAnalyzer.Tests;

/// <summary>
/// Tests for SqliteDocumentRepository using in-memory SQLite.
/// Covers document CRUD, chunk persistence, vector similarity search, and cascade deletes.
/// </summary>
public class DocumentRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly InsuranceAnalysisDbContext _db;
    private readonly SqliteDocumentRepository _repo;

    public DocumentRepositoryTests()
    {
        // Use SQLite in-memory with shared connection (kept open for test duration)
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<InsuranceAnalysisDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new InsuranceAnalysisDbContext(options);
        _db.Database.EnsureCreated();

        var mockLogger = new Mock<ILogger<SqliteDocumentRepository>>();
        _repo = new SqliteDocumentRepository(_db, mockLogger.Object);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task SaveDocumentAsync_AssignsIdAndPersists()
    {
        // Arrange
        var document = new DocumentRecord
        {
            FileName = "homeowners-policy-2024.pdf",
            MimeType = "application/pdf",
            Category = "Policy",
            ExtractedText = "DECLARATIONS PAGE - Homeowner Policy [POLICY-REDACTED]. Dwelling coverage: $350,000. Deductible: $1,000.",
            PageCount = 12,
            ChunkCount = 8,
            EmbeddingProvider = "Voyage",
            EmbeddingDimensions = 1024,
            Status = "Ready"
        };

        // Act
        var saved = await _repo.SaveDocumentAsync(document);

        // Assert
        Assert.True(saved.Id > 0);
        Assert.Equal("homeowners-policy-2024.pdf", saved.FileName);
        Assert.Equal("Policy", saved.Category);
        Assert.Equal("Ready", saved.Status);

        var persisted = await _db.Documents.FindAsync(saved.Id);
        Assert.NotNull(persisted);
        Assert.Equal("application/pdf", persisted.MimeType);
        Assert.Equal(1024, persisted.EmbeddingDimensions);
    }

    [Fact]
    public async Task UpdateDocumentAsync_SetsUpdatedAtTimestamp()
    {
        // Arrange
        var document = new DocumentRecord
        {
            FileName = "auto-claim-evidence-packet.pdf",
            MimeType = "application/pdf",
            Category = "Claim",
            Status = "Uploading"
        };
        _db.Documents.Add(document);
        await _db.SaveChangesAsync();

        // Act
        document.Status = "Ready";
        document.ExtractedText = "Vehicle damage assessment report. Front bumper and hood replacement required.";
        document.ChunkCount = 3;
        await _repo.UpdateDocumentAsync(document);

        // Assert
        var updated = await _db.Documents.FindAsync(document.Id);
        Assert.NotNull(updated);
        Assert.Equal("Ready", updated.Status);
        Assert.NotNull(updated.UpdatedAt);
        Assert.True(updated.UpdatedAt > updated.CreatedAt);
    }

    [Fact]
    public async Task GetDocumentByIdAsync_ReturnsDocumentWithChunks()
    {
        // Arrange
        var document = new DocumentRecord
        {
            FileName = "commercial-liability-endorsement.pdf",
            MimeType = "application/pdf",
            Category = "Endorsement",
            ExtractedText = "ENDORSEMENT: Additional insured coverage for subcontractor operations.",
            PageCount = 2,
            ChunkCount = 2,
            EmbeddingProvider = "Voyage",
            EmbeddingDimensions = 1024,
            Status = "Ready"
        };
        _db.Documents.Add(document);
        await _db.SaveChangesAsync();

        _db.DocumentChunks.AddRange(
            new DocumentChunkRecord
            {
                DocumentId = document.Id,
                ChunkIndex = 0,
                SectionName = "ENDORSEMENT",
                Content = "Additional insured coverage extends to subcontractor operations on-premises.",
                TokenCount = 48,
                EmbeddingJson = JsonSerializer.Serialize(new float[] { 0.5f, 0.3f, 0.2f })
            },
            new DocumentChunkRecord
            {
                DocumentId = document.Id,
                ChunkIndex = 1,
                SectionName = "CONDITIONS",
                Content = "Coverage is contingent upon written contract between named insured and additional insured.",
                TokenCount = 55,
                EmbeddingJson = JsonSerializer.Serialize(new float[] { 0.4f, 0.4f, 0.2f })
            }
        );
        await _db.SaveChangesAsync();

        // Act
        var result = await _repo.GetDocumentByIdAsync(document.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("commercial-liability-endorsement.pdf", result.FileName);
        Assert.Equal(2, result.Chunks.Count);
        Assert.Equal("ENDORSEMENT", result.Chunks[0].SectionName);
    }

    [Fact]
    public async Task GetDocumentByIdAsync_ReturnsNullForMissingId()
    {
        // Act
        var result = await _repo.GetDocumentByIdAsync(999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetDocumentsAsync_FiltersByCategory()
    {
        // Arrange
        _db.Documents.AddRange(
            new DocumentRecord
            {
                FileName = "dwelling-fire-policy.pdf",
                MimeType = "application/pdf",
                Category = "Policy",
                Status = "Ready"
            },
            new DocumentRecord
            {
                FileName = "water-damage-claim-form.pdf",
                MimeType = "application/pdf",
                Category = "Claim",
                Status = "Ready"
            },
            new DocumentRecord
            {
                FileName = "umbrella-liability-policy.pdf",
                MimeType = "application/pdf",
                Category = "Policy",
                Status = "Ready"
            },
            new DocumentRecord
            {
                FileName = "regulatory-correspondence-doi.pdf",
                MimeType = "application/pdf",
                Category = "Correspondence",
                Status = "Ready"
            }
        );
        await _db.SaveChangesAsync();

        // Act
        var (items, totalCount) = await _repo.GetDocumentsAsync(category: "Policy");

        // Assert
        Assert.Equal(2, totalCount);
        Assert.Equal(2, items.Count);
        Assert.All(items, d => Assert.Equal("Policy", d.Category));
    }

    [Fact]
    public async Task GetDocumentsAsync_PaginatesCorrectly()
    {
        // Arrange
        for (var i = 0; i < 7; i++)
        {
            _db.Documents.Add(new DocumentRecord
            {
                FileName = $"policy-document-{i:D3}.pdf",
                MimeType = "application/pdf",
                Category = "Policy",
                Status = "Ready",
                CreatedAt = DateTime.UtcNow.AddMinutes(-i) // Ensure distinct ordering
            });
        }
        await _db.SaveChangesAsync();

        // Act
        var (page1Items, totalCount1) = await _repo.GetDocumentsAsync(pageSize: 3, page: 1);
        var (page2Items, totalCount2) = await _repo.GetDocumentsAsync(pageSize: 3, page: 2);
        var (page3Items, totalCount3) = await _repo.GetDocumentsAsync(pageSize: 3, page: 3);

        // Assert
        Assert.Equal(3, page1Items.Count);
        Assert.Equal(3, page2Items.Count);
        Assert.Equal(1, page3Items.Count);
        Assert.Equal(7, totalCount1);
        Assert.Equal(7, totalCount2);
        Assert.Equal(7, totalCount3);

        // Verify descending order by CreatedAt (most recent first)
        Assert.Equal("policy-document-000.pdf", page1Items[0].FileName);
    }

    [Fact]
    public async Task SaveChunksAsync_PersistsMultipleChunks()
    {
        // Arrange
        var document = new DocumentRecord
        {
            FileName = "workers-comp-policy-renewal.pdf",
            MimeType = "application/pdf",
            Category = "Policy",
            Status = "Processing"
        };
        _db.Documents.Add(document);
        await _db.SaveChangesAsync();

        var chunks = new List<DocumentChunkRecord>
        {
            new()
            {
                DocumentId = document.Id,
                ChunkIndex = 0,
                SectionName = "DECLARATIONS",
                Content = "Named Insured: [PII-REDACTED]. Policy Period: 01/01/2025 to 01/01/2026. Classification: Clerical Office.",
                TokenCount = 62,
                EmbeddingJson = JsonSerializer.Serialize(new float[] { 0.8f, 0.1f, 0.1f })
            },
            new()
            {
                DocumentId = document.Id,
                ChunkIndex = 1,
                SectionName = "COVERAGE",
                Content = "Part One - Workers Compensation: Statutory benefits per state law. Part Two - Employers Liability: $500,000 each accident.",
                TokenCount = 85,
                EmbeddingJson = JsonSerializer.Serialize(new float[] { 0.2f, 0.7f, 0.1f })
            },
            new()
            {
                DocumentId = document.Id,
                ChunkIndex = 2,
                SectionName = "EXCLUSIONS",
                Content = "This insurance does not cover: injury to employees excluded by endorsement, punitive damages, employment practices liability.",
                TokenCount = 71,
                EmbeddingJson = JsonSerializer.Serialize(new float[] { 0.1f, 0.2f, 0.7f })
            }
        };

        // Act
        await _repo.SaveChunksAsync(chunks);

        // Assert
        var savedChunks = await _db.DocumentChunks
            .Where(c => c.DocumentId == document.Id)
            .OrderBy(c => c.ChunkIndex)
            .ToListAsync();
        Assert.Equal(3, savedChunks.Count);
        Assert.Equal("DECLARATIONS", savedChunks[0].SectionName);
        Assert.Equal("COVERAGE", savedChunks[1].SectionName);
        Assert.Equal("EXCLUSIONS", savedChunks[2].SectionName);
        Assert.Equal(62, savedChunks[0].TokenCount);
    }

    [Fact]
    public async Task SearchSimilarChunksAsync_ReturnsTopKBySimilarity()
    {
        // Arrange — create a document with chunks having known embedding vectors
        var document = new DocumentRecord
        {
            FileName = "general-liability-policy.pdf",
            MimeType = "application/pdf",
            Category = "Policy",
            Status = "Ready"
        };
        _db.Documents.Add(document);
        await _db.SaveChangesAsync();

        // Chunk embeddings designed so similarity to query [0.9, 0.1, 0.0] is:
        // chunk0 [0.9, 0.1, 0.0] -> highest (near-identical)
        // chunk1 [0.7, 0.3, 0.0] -> second highest
        // chunk2 [0.0, 0.1, 0.9] -> lowest (nearly orthogonal)
        // chunk3 [0.5, 0.5, 0.0] -> third highest
        _db.DocumentChunks.AddRange(
            new DocumentChunkRecord
            {
                DocumentId = document.Id,
                ChunkIndex = 0,
                SectionName = "DECLARATIONS",
                Content = "Commercial General Liability declarations page with policy limits and named insureds.",
                TokenCount = 45,
                EmbeddingJson = JsonSerializer.Serialize(new float[] { 0.9f, 0.1f, 0.0f })
            },
            new DocumentChunkRecord
            {
                DocumentId = document.Id,
                ChunkIndex = 1,
                SectionName = "COVERAGE",
                Content = "Coverage A - Bodily Injury and Property Damage Liability. Occurrence limit: $1,000,000.",
                TokenCount = 52,
                EmbeddingJson = JsonSerializer.Serialize(new float[] { 0.7f, 0.3f, 0.0f })
            },
            new DocumentChunkRecord
            {
                DocumentId = document.Id,
                ChunkIndex = 2,
                SectionName = "EXCLUSIONS",
                Content = "Exclusion for professional liability, pollution, and employment-related practices.",
                TokenCount = 38,
                EmbeddingJson = JsonSerializer.Serialize(new float[] { 0.0f, 0.1f, 0.9f })
            },
            new DocumentChunkRecord
            {
                DocumentId = document.Id,
                ChunkIndex = 3,
                SectionName = "CONDITIONS",
                Content = "Duties in the event of occurrence: notify insurer promptly, cooperate in investigation.",
                TokenCount = 44,
                EmbeddingJson = JsonSerializer.Serialize(new float[] { 0.5f, 0.5f, 0.0f })
            }
        );
        await _db.SaveChangesAsync();

        var queryEmbedding = new float[] { 0.9f, 0.1f, 0.0f };

        // Act
        var results = await _repo.SearchSimilarChunksAsync(queryEmbedding, topK: 2);

        // Assert
        Assert.Equal(2, results.Count);
        // Most similar chunk should be first (DECLARATIONS with identical embedding)
        Assert.Equal("DECLARATIONS", results[0].Chunk.SectionName);
        Assert.True(results[0].Similarity > results[1].Similarity);
        // Second most similar should be COVERAGE (embedding [0.7, 0.3, 0.0])
        Assert.Equal("COVERAGE", results[1].Chunk.SectionName);
        // Verify similarity scores are positive and in descending order
        Assert.True(results[0].Similarity > 0.9);
        Assert.True(results[1].Similarity > 0.5);
    }

    [Fact]
    public async Task SearchSimilarChunksAsync_ExcludesNonReadyDocuments()
    {
        // Arrange — create two documents: one Ready, one Processing
        var readyDoc = new DocumentRecord
        {
            FileName = "approved-policy.pdf",
            MimeType = "application/pdf",
            Category = "Policy",
            Status = "Ready"
        };
        var processingDoc = new DocumentRecord
        {
            FileName = "processing-claim.pdf",
            MimeType = "application/pdf",
            Category = "Claim",
            Status = "Processing"
        };
        var failedDoc = new DocumentRecord
        {
            FileName = "failed-endorsement.pdf",
            MimeType = "application/pdf",
            Category = "Endorsement",
            Status = "Failed"
        };
        _db.Documents.AddRange(readyDoc, processingDoc, failedDoc);
        await _db.SaveChangesAsync();

        // All chunks have similar embeddings — only the Ready doc should appear
        _db.DocumentChunks.AddRange(
            new DocumentChunkRecord
            {
                DocumentId = readyDoc.Id,
                ChunkIndex = 0,
                SectionName = "COVERAGE",
                Content = "Coverage A: Dwelling protection for approved policy.",
                TokenCount = 30,
                EmbeddingJson = JsonSerializer.Serialize(new float[] { 0.9f, 0.1f, 0.0f })
            },
            new DocumentChunkRecord
            {
                DocumentId = processingDoc.Id,
                ChunkIndex = 0,
                SectionName = "GENERAL",
                Content = "Still processing this claim document.",
                TokenCount = 25,
                EmbeddingJson = JsonSerializer.Serialize(new float[] { 0.85f, 0.15f, 0.0f })
            },
            new DocumentChunkRecord
            {
                DocumentId = failedDoc.Id,
                ChunkIndex = 0,
                SectionName = "ENDORSEMENT",
                Content = "Failed endorsement OCR extraction.",
                TokenCount = 20,
                EmbeddingJson = JsonSerializer.Serialize(new float[] { 0.88f, 0.12f, 0.0f })
            }
        );
        await _db.SaveChangesAsync();

        var queryEmbedding = new float[] { 0.9f, 0.1f, 0.0f };

        // Act
        var results = await _repo.SearchSimilarChunksAsync(queryEmbedding, topK: 5);

        // Assert — only the "Ready" document's chunk should be returned
        Assert.Single(results);
        Assert.Equal(readyDoc.Id, results[0].Chunk.DocumentId);
        Assert.Equal("COVERAGE", results[0].Chunk.SectionName);
    }

    [Fact]
    public async Task SearchSimilarChunksAsync_FiltersByDocumentId()
    {
        // Arrange — create two documents with chunks
        var policyDoc = new DocumentRecord
        {
            FileName = "flood-insurance-policy.pdf",
            MimeType = "application/pdf",
            Category = "Policy",
            Status = "Ready"
        };
        var claimDoc = new DocumentRecord
        {
            FileName = "flood-claim-adjuster-report.pdf",
            MimeType = "application/pdf",
            Category = "Claim",
            Status = "Ready"
        };
        _db.Documents.AddRange(policyDoc, claimDoc);
        await _db.SaveChangesAsync();

        _db.DocumentChunks.AddRange(
            new DocumentChunkRecord
            {
                DocumentId = policyDoc.Id,
                ChunkIndex = 0,
                SectionName = "COVERAGE",
                Content = "National Flood Insurance Program coverage for residential structures in Zone AE.",
                TokenCount = 40,
                EmbeddingJson = JsonSerializer.Serialize(new float[] { 0.8f, 0.2f, 0.0f })
            },
            new DocumentChunkRecord
            {
                DocumentId = claimDoc.Id,
                ChunkIndex = 0,
                SectionName = "GENERAL",
                Content = "Adjuster field inspection: 4 feet of standing water in basement. Foundation damage observed.",
                TokenCount = 48,
                EmbeddingJson = JsonSerializer.Serialize(new float[] { 0.85f, 0.15f, 0.0f })
            }
        );
        await _db.SaveChangesAsync();

        var queryEmbedding = new float[] { 0.8f, 0.2f, 0.0f };

        // Act — search only within the policy document
        var filteredResults = await _repo.SearchSimilarChunksAsync(
            queryEmbedding, topK: 5, documentId: policyDoc.Id);

        // Act — search across all documents
        var allResults = await _repo.SearchSimilarChunksAsync(
            queryEmbedding, topK: 5);

        // Assert
        Assert.Single(filteredResults);
        var singleResult = Assert.Single(filteredResults);
        Assert.Equal(policyDoc.Id, singleResult.Chunk.DocumentId);
        Assert.Equal("COVERAGE", singleResult.Chunk.SectionName);

        Assert.Equal(2, allResults.Count);
    }

    [Fact]
    public async Task DeleteDocumentAsync_CascadeDeletesChunks()
    {
        // Arrange
        var document = new DocumentRecord
        {
            FileName = "cancelled-auto-policy.pdf",
            MimeType = "application/pdf",
            Category = "Policy",
            ExtractedText = "Policy cancelled for non-payment effective 03/15/2025.",
            Status = "Ready"
        };
        _db.Documents.Add(document);
        await _db.SaveChangesAsync();

        _db.DocumentChunks.AddRange(
            new DocumentChunkRecord
            {
                DocumentId = document.Id,
                ChunkIndex = 0,
                SectionName = "DECLARATIONS",
                Content = "Auto policy declarations: [POLICY-REDACTED]. Status: Cancelled.",
                TokenCount = 30,
                EmbeddingJson = JsonSerializer.Serialize(new float[] { 0.6f, 0.3f, 0.1f })
            },
            new DocumentChunkRecord
            {
                DocumentId = document.Id,
                ChunkIndex = 1,
                SectionName = "CONDITIONS",
                Content = "Cancellation for non-payment: 10-day notice required per state regulation.",
                TokenCount = 35,
                EmbeddingJson = JsonSerializer.Serialize(new float[] { 0.3f, 0.6f, 0.1f })
            }
        );
        await _db.SaveChangesAsync();

        // Verify chunks exist before deletion
        var chunksBefore = await _db.DocumentChunks
            .Where(c => c.DocumentId == document.Id)
            .CountAsync();
        Assert.Equal(2, chunksBefore);

        // Act
        await _repo.DeleteDocumentAsync(document.Id);

        // Assert
        var deletedDocument = await _db.Documents.FindAsync(document.Id);
        Assert.Null(deletedDocument);

        var chunksAfter = await _db.DocumentChunks
            .Where(c => c.DocumentId == document.Id)
            .CountAsync();
        Assert.Equal(0, chunksAfter);
    }
}
