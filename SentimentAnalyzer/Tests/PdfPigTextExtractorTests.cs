using Microsoft.Extensions.Logging;
using Moq;
using SentimentAnalyzer.Agents.Orchestration;
using SentimentAnalyzer.API.Services.Multimodal;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace SentimentAnalyzer.Tests;

/// <summary>
/// Tests for PdfPigTextExtractor (Tier 1 OCR — local native PDF text extraction).
/// Uses PdfDocumentBuilder to create minimal valid PDFs with insurance-realistic content.
/// </summary>
public class PdfPigTextExtractorTests
{
    private readonly Mock<ILogger<PdfPigTextExtractor>> _loggerMock = new();
    private readonly Mock<IPIIRedactor> _piiRedactorMock = new();

    private PdfPigTextExtractor CreateService(bool withRedactor = false)
    {
        if (withRedactor)
        {
            // Default PII redaction: pass-through (no PII detected)
            _piiRedactorMock.Setup(p => p.Redact(It.IsAny<string>())).Returns<string>(s => s);
            return new PdfPigTextExtractor(_loggerMock.Object, _piiRedactorMock.Object);
        }

        return new PdfPigTextExtractor(_loggerMock.Object);
    }

    /// <summary>
    /// Creates a minimal valid PDF document containing the specified text using PdfPig's PdfDocumentBuilder.
    /// </summary>
    private static byte[] CreateMinimalPdf(string text)
    {
        using var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(UglyToad.PdfPig.Content.PageSize.Letter);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        page.AddText(text, 12, new UglyToad.PdfPig.Core.PdfPoint(50, 700), font);
        return builder.Build();
    }

    [Fact]
    public async Task ExtractTextAsync_WithNativePdf_ReturnsText()
    {
        var insuranceText = "INSURANCE POLICY DOCUMENT Policy Number: HO-2024-789456 Coverage: Dwelling $250,000 Deductible: $1,000 Effective Date: January 1, 2024 Insured: Commercial Property Holdings LLC";
        var pdfBytes = CreateMinimalPdf(insuranceText);
        var service = CreateService();

        var result = await service.ExtractTextAsync(pdfBytes, "application/pdf");

        Assert.True(result.IsSuccess);
        Assert.Equal("PdfPig", result.Provider);
        Assert.Equal(1.0, result.Confidence);
        Assert.Equal(1, result.PageCount);
        Assert.Contains("INSURANCE POLICY DOCUMENT", result.ExtractedText);
        Assert.Contains("HO-2024-789456", result.ExtractedText);
    }

    [Fact]
    public async Task ExtractTextAsync_WithNonPdfMimeType_ReturnsError()
    {
        var service = CreateService();

        var result = await service.ExtractTextAsync(new byte[] { 1, 2, 3 }, "image/png");

        Assert.False(result.IsSuccess);
        Assert.Equal("PdfPig", result.Provider);
        Assert.Contains("only supports PDF", result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractTextAsync_WithInsufficientText_ReturnsFailure()
    {
        var pdfBytes = CreateMinimalPdf("Hi");
        var service = CreateService();

        var result = await service.ExtractTextAsync(pdfBytes, "application/pdf");

        Assert.False(result.IsSuccess);
        Assert.Equal("PdfPig", result.Provider);
        Assert.Contains("Insufficient text", result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractTextAsync_WithInvalidPdfBytes_ReturnsError()
    {
        var service = CreateService();

        var result = await service.ExtractTextAsync(new byte[] { 1, 2, 3 }, "application/pdf");

        Assert.False(result.IsSuccess);
        Assert.Equal("PdfPig", result.Provider);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractTextAsync_AppliesPiiRedaction()
    {
        var insuranceText = "INSURANCE POLICY DOCUMENT Policy Number: HO-2024-789456 Coverage: Dwelling $250,000 Deductible: $1,000 Effective Date: January 1, 2024 Insured: Commercial Property Holdings LLC";
        var pdfBytes = CreateMinimalPdf(insuranceText);

        var service = CreateService(withRedactor: true);

        // Override the default pass-through: replace policy number with redacted marker
        _piiRedactorMock.Setup(p => p.Redact(It.IsAny<string>()))
            .Returns<string>(s => s.Replace("HO-2024-789456", "[POLICY-REDACTED]"));

        var result = await service.ExtractTextAsync(pdfBytes, "application/pdf");

        Assert.True(result.IsSuccess);
        Assert.Contains("[POLICY-REDACTED]", result.ExtractedText);
        Assert.DoesNotContain("HO-2024-789456", result.ExtractedText);
        _piiRedactorMock.Verify(p => p.Redact(It.IsAny<string>()), Times.Once);
    }
}
