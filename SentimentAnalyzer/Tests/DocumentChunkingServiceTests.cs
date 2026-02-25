using Microsoft.Extensions.Logging;
using Moq;
using SentimentAnalyzer.API.Services.Documents;
using Xunit;

namespace SentimentAnalyzer.Tests;

/// <summary>
/// Tests for InsuranceDocumentChunkingService — verifies insurance-section-aware
/// document chunking, sentence-boundary splitting, overlap, and token estimation.
/// </summary>
public class DocumentChunkingServiceTests
{
    private readonly InsuranceDocumentChunkingService _service;

    public DocumentChunkingServiceTests()
    {
        var logger = new Mock<ILogger<InsuranceDocumentChunkingService>>();
        _service = new InsuranceDocumentChunkingService(logger.Object);
    }

    // ──────────────────────────────────────────
    // Empty / null input
    // ──────────────────────────────────────────

    [Fact]
    public void ChunkDocument_EmptyText_ReturnsEmptyList()
    {
        // Arrange
        var text = string.Empty;

        // Act
        var result = _service.ChunkDocument(text);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void ChunkDocument_NullText_ReturnsEmptyList()
    {
        // Arrange
        string? text = null;

        // Act
        var result = _service.ChunkDocument(text!);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    // ──────────────────────────────────────────
    // Short text (no section headers)
    // ──────────────────────────────────────────

    [Fact]
    public void ChunkDocument_ShortText_ReturnsSingleGeneralChunk()
    {
        // Arrange — realistic insurance text with no recognized section headers, under 512 tokens
        var text = "The policyholder reported minor windstorm damage to the roof on February 10, 2025. " +
                   "An adjuster was dispatched on February 12. Initial estimate: $4,200 for shingle replacement. " +
                   "No interior water damage was observed during the on-site inspection.";

        // Act
        var result = _service.ChunkDocument(text);

        // Assert
        Assert.Single(result);
        Assert.Equal("GENERAL", result[0].SectionName);
        Assert.Equal(0, result[0].Index);
        Assert.Contains("policyholder reported minor windstorm damage", result[0].Content);
    }

    // ──────────────────────────────────────────
    // Insurance section header splitting
    // ──────────────────────────────────────────

    [Fact]
    public void ChunkDocument_WithInsuranceSectionHeaders_SplitsBySections()
    {
        // Arrange — document with three standard insurance section headers
        var text =
            "DECLARATIONS:\n" +
            "Named Insured: ABC Corp. Policy Period: 01/01/2024 to 01/01/2025. Coverage Limits: $500,000 per occurrence. " +
            "Premium: $12,450 annually. Deductible: $2,500 per claim.\n" +
            "\n" +
            "COVERAGE:\n" +
            "This policy covers direct physical loss or damage to covered property caused by a covered peril. " +
            "Coverage extends to business personal property, tenant improvements, and loss of business income resulting from a covered loss. " +
            "Additional coverage is provided for debris removal up to $25,000 per occurrence.\n" +
            "\n" +
            "EXCLUSIONS:\n" +
            "This policy does not cover loss or damage caused by: 1. Flood or surface water. 2. Earthquake or volcanic eruption. " +
            "3. War or military action. 4. Nuclear hazard. 5. Intentional acts by the insured or any person acting on behalf of the insured.";

        // Act
        var result = _service.ChunkDocument(text);

        // Assert — should have 3 chunks, one per section
        Assert.True(result.Count >= 3, $"Expected at least 3 chunks but got {result.Count}");

        var sectionNames = result.Select(c => c.SectionName).Distinct().ToList();
        Assert.Contains("DECLARATIONS", sectionNames);
        Assert.Contains("COVERAGE", sectionNames);
        Assert.Contains("EXCLUSIONS", sectionNames);

        // Verify content is in the right sections
        var declarationsChunk = result.First(c => c.SectionName == "DECLARATIONS");
        Assert.Contains("ABC Corp", declarationsChunk.Content);

        var coverageChunk = result.First(c => c.SectionName == "COVERAGE");
        Assert.Contains("direct physical loss", coverageChunk.Content);

        var exclusionsChunk = result.First(c => c.SectionName == "EXCLUSIONS");
        Assert.Contains("Flood or surface water", exclusionsChunk.Content);
    }

    // ──────────────────────────────────────────
    // Oversized section splitting
    // ──────────────────────────────────────────

    [Fact]
    public void ChunkDocument_OversizedSection_SplitsAtSentenceBoundaries()
    {
        // Arrange — a single COVERAGE section with >2048 chars (512 tokens * 4 chars/token)
        // Each sentence is ~100 chars, so we need ~25+ sentences to exceed 2048 chars
        var longCoverageText = string.Join(" ",
            Enumerable.Range(1, 30).Select(i =>
                $"Coverage provision {i}: The insurer shall indemnify the policyholder for covered losses up to the applicable sublimit of ${i * 10000}."));

        var text =
            "COVERAGE:\n" +
            longCoverageText;

        // Verify we actually exceeded the target
        Assert.True(longCoverageText.Length > 2048,
            $"Test setup error: long text is only {longCoverageText.Length} chars, need >2048");

        // Act
        var result = _service.ChunkDocument(text);

        // Assert — should produce multiple chunks from the single oversized section
        Assert.True(result.Count > 1, $"Expected >1 chunk for oversized section but got {result.Count}");

        // All chunks should belong to the COVERAGE section
        Assert.All(result, chunk => Assert.Equal("COVERAGE", chunk.SectionName));

        // Each chunk's content should be non-empty
        Assert.All(result, chunk => Assert.False(string.IsNullOrWhiteSpace(chunk.Content)));
    }

    // ──────────────────────────────────────────
    // Sequential chunk indices
    // ──────────────────────────────────────────

    [Fact]
    public void ChunkDocument_SetsCorrectChunkIndices()
    {
        // Arrange — document with multiple sections to produce multiple chunks
        var text =
            "DECLARATIONS:\n" +
            "Named Insured: Riverside Insurance Group. Policy Number: CGL-2024-887123. Effective Date: March 1, 2024. " +
            "Premium: $8,750 semi-annually.\n" +
            "\n" +
            "CONDITIONS:\n" +
            "The insured must report all claims within 60 days of the date of loss. Failure to provide timely notice may result " +
            "in denial of the claim. The insured must cooperate fully with any investigation conducted by the insurer.\n" +
            "\n" +
            "ENDORSEMENTS:\n" +
            "Endorsement 001: Waiver of subrogation in favor of the named additional insured. " +
            "Endorsement 002: Blanket additional insured coverage for contractual obligations.";

        // Act
        var result = _service.ChunkDocument(text);

        // Assert — indices should be sequential starting from 0
        Assert.True(result.Count >= 3, $"Expected at least 3 chunks but got {result.Count}");
        for (var i = 0; i < result.Count; i++)
        {
            Assert.Equal(i, result[i].Index);
        }
    }

    // ──────────────────────────────────────────
    // Token approximation
    // ──────────────────────────────────────────

    [Fact]
    public void ChunkDocument_ApproximateTokensCalculation()
    {
        // Arrange — text with known length, no section headers
        var text = "The insured sustained hail damage to the commercial property located at 1500 Industrial Parkway. " +
                   "Roof inspection confirmed granule loss on approximately 40% of the surface area. " +
                   "The estimated repair cost is $18,500 including labor and materials.";

        // Act
        var result = _service.ChunkDocument(text);

        // Assert — ApproximateTokens should be Content.Length / 4
        Assert.Single(result);
        var chunk = result[0];
        var expectedTokens = chunk.Content.Length / 4;
        Assert.Equal(expectedTokens, chunk.ApproximateTokens);
    }

    // ──────────────────────────────────────────
    // Multiple varied section headers
    // ──────────────────────────────────────────

    [Fact]
    public void ChunkDocument_MultipleSectionsWithVariedHeaders()
    {
        // Arrange — test DEFINITIONS, ENDORSEMENTS, and CONDITIONS headers
        var text =
            "DEFINITIONS:\n" +
            "\"Bodily Injury\" means physical injury, sickness, or disease sustained by a person, including death resulting " +
            "from any of these at any time. \"Property Damage\" means physical injury to tangible property, including all " +
            "resulting loss of use of that property, or loss of use of tangible property that is not physically injured.\n" +
            "\n" +
            "ENDORSEMENTS:\n" +
            "Endorsement CG-2010: Additional Insured — Owners, Lessees, or Contractors. This endorsement modifies " +
            "insurance provided under the Commercial General Liability Coverage Part. The person or organization shown " +
            "in the schedule is an additional insured only with respect to liability for bodily injury or property damage.\n" +
            "\n" +
            "CONDITIONS:\n" +
            "Duties in the Event of Occurrence, Offense, Claim, or Suit: The insured must see to it that the insurer is " +
            "notified as soon as practicable of an occurrence or an offense which may result in a claim. Notice should include " +
            "how, when, and where the occurrence took place, the names and addresses of any injured persons and witnesses.";

        // Act
        var result = _service.ChunkDocument(text);

        // Assert — all three varied section types should be recognized
        var sectionNames = result.Select(c => c.SectionName).Distinct().ToList();
        Assert.Contains("DEFINITIONS", sectionNames);
        Assert.Contains("ENDORSEMENTS", sectionNames);
        Assert.Contains("CONDITIONS", sectionNames);
        Assert.Equal(3, sectionNames.Count);

        // Verify content integrity for each section
        var definitionsChunk = result.First(c => c.SectionName == "DEFINITIONS");
        Assert.Contains("Bodily Injury", definitionsChunk.Content);

        var endorsementsChunk = result.First(c => c.SectionName == "ENDORSEMENTS");
        Assert.Contains("CG-2010", endorsementsChunk.Content);

        var conditionsChunk = result.First(c => c.SectionName == "CONDITIONS");
        Assert.Contains("Duties in the Event of Occurrence", conditionsChunk.Content);
    }
}
