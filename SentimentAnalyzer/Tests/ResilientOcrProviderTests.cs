using Microsoft.Extensions.Logging;
using Moq;
using SentimentAnalyzer.API.Services.Multimodal;
using Xunit;

namespace SentimentAnalyzer.Tests;

/// <summary>
/// Tests for ResilientOcrProvider.
/// Validates 4-tier fallback chain ordered by data safety:
/// PdfPig (local) → Azure Doc Intel (no training) → OCR Space (GDPR) → Gemini Vision (last resort).
/// </summary>
public class ResilientOcrProviderTests
{
    private readonly Mock<IDocumentOcrService> _pdfPigMock = new();
    private readonly Mock<IDocumentOcrService> _azureMock = new();
    private readonly Mock<IDocumentOcrService> _geminiMock = new();
    private readonly Mock<IDocumentOcrService> _ocrSpaceMock = new();
    private readonly Mock<ILogger<ResilientOcrProvider>> _loggerMock = new();

    private ResilientOcrProvider CreateProvider()
    {
        return new ResilientOcrProvider(
            _pdfPigMock.Object,
            _azureMock.Object,
            _geminiMock.Object,
            _ocrSpaceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task ExtractTextAsync_PdfPigSucceeds_ReturnsPdfPigResult()
    {
        _pdfPigMock.Setup(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult
            {
                IsSuccess = true,
                ExtractedText = "COMMERCIAL GENERAL LIABILITY POLICY\nPolicy Number: CGL-2024-445566\nNamed Insured: Great Lakes Manufacturing Corp\nCoverage: $1,000,000 per occurrence",
                PageCount = 1,
                Confidence = 1.0,
                Provider = "PdfPig"
            });

        var provider = CreateProvider();
        var result = await provider.ExtractTextAsync(
            new byte[] { 1, 2, 3 }, "application/pdf");

        Assert.True(result.IsSuccess);
        Assert.Equal("PdfPig", result.Provider);
        Assert.Contains("CGL-2024-445566", result.ExtractedText);

        // Azure, OCR Space, and Gemini should NEVER be called
        _azureMock.Verify(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _ocrSpaceMock.Verify(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _geminiMock.Verify(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExtractTextAsync_PdfPigFails_FallsBackToAzure()
    {
        _pdfPigMock.Setup(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult
            {
                IsSuccess = false,
                Provider = "PdfPig",
                ErrorMessage = "Insufficient text extracted (likely a scanned document)"
            });

        _azureMock.Setup(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult
            {
                IsSuccess = true,
                ExtractedText = "PROPERTY DAMAGE ASSESSMENT\nClaim: CLM-2024-887766\nAdjuster: Field Services Division\nStructure: Single-family dwelling, 2,400 sq ft\nDamage: Wind and hail damage to roof and siding\nEstimate: $18,500",
                PageCount = 2,
                Confidence = 0.92,
                Provider = "AzureDocIntel"
            });

        var provider = CreateProvider();
        var result = await provider.ExtractTextAsync(
            new byte[] { 1, 2, 3 }, "application/pdf");

        Assert.True(result.IsSuccess);
        Assert.Equal("AzureDocIntel", result.Provider);
        Assert.Contains("CLM-2024-887766", result.ExtractedText);

        // OCR Space and Gemini should NOT be called (Azure succeeded)
        _ocrSpaceMock.Verify(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _geminiMock.Verify(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExtractTextAsync_PdfPigAndAzureFail_FallsBackToOcrSpace()
    {
        _pdfPigMock.Setup(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult
            {
                IsSuccess = false,
                Provider = "PdfPig",
                ErrorMessage = "Insufficient text extracted (likely a scanned document)"
            });

        _azureMock.Setup(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult
            {
                IsSuccess = false,
                Provider = "AzureDocIntel",
                ErrorMessage = "Azure Document Intelligence API error (HTTP 429): Rate limit exceeded"
            });

        _ocrSpaceMock.Setup(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult
            {
                IsSuccess = true,
                ExtractedText = "AUTO INSURANCE CLAIM\nPolicy: AUTO-2024-334455\nVehicle: 2024 Honda Accord EX-L\nDate of Loss: May 22, 2024\nDescription: Rear-end collision at signalized intersection\nRepair Estimate: $6,200",
                PageCount = 1,
                Confidence = 0.85,
                Provider = "OcrSpace"
            });

        var provider = CreateProvider();
        var result = await provider.ExtractTextAsync(
            new byte[] { 1, 2, 3 }, "application/pdf");

        Assert.True(result.IsSuccess);
        Assert.Equal("OcrSpace", result.Provider);
        Assert.Contains("AUTO-2024-334455", result.ExtractedText);

        // Gemini should NOT be called (OCR Space succeeded before it)
        _geminiMock.Verify(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExtractTextAsync_FirstThreeFail_FallsBackToGemini()
    {
        _pdfPigMock.Setup(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult
            {
                IsSuccess = false,
                Provider = "PdfPig",
                ErrorMessage = "Insufficient text extracted (likely a scanned document)"
            });

        _azureMock.Setup(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult
            {
                IsSuccess = false,
                Provider = "AzureDocIntel",
                ErrorMessage = "Azure Document Intelligence API key not configured."
            });

        _ocrSpaceMock.Setup(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult
            {
                IsSuccess = false,
                Provider = "OcrSpace",
                ErrorMessage = "OCR.space API key not configured."
            });

        _geminiMock.Setup(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult
            {
                IsSuccess = true,
                ExtractedText = "UMBRELLA LIABILITY ENDORSEMENT\nPolicy: UMB-2024-998877\nUnderlying Coverage: CGL + Auto\nLimit: $2,000,000 per occurrence\nRetention: $10,000 self-insured",
                PageCount = 1,
                Confidence = 0.75,
                Provider = "GeminiVision"
            });

        var provider = CreateProvider();
        var result = await provider.ExtractTextAsync(
            new byte[] { 1, 2, 3 }, "application/pdf");

        Assert.True(result.IsSuccess);
        Assert.Equal("GeminiVision", result.Provider);
        Assert.Contains("UMB-2024-998877", result.ExtractedText);
    }

    [Fact]
    public async Task ExtractTextAsync_AllFail_ReturnsGeminiError()
    {
        _pdfPigMock.Setup(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult
            {
                IsSuccess = false,
                Provider = "PdfPig",
                ErrorMessage = "PdfPig extraction error: invalid PDF structure"
            });

        _azureMock.Setup(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult
            {
                IsSuccess = false,
                Provider = "AzureDocIntel",
                ErrorMessage = "Azure Document Intelligence API error (HTTP 503): Service unavailable"
            });

        _ocrSpaceMock.Setup(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult
            {
                IsSuccess = false,
                Provider = "OcrSpace",
                ErrorMessage = "OCR.space API error: InternalServerError"
            });

        _geminiMock.Setup(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult
            {
                IsSuccess = false,
                Provider = "GeminiVision",
                ErrorMessage = "Gemini Vision API error: InternalServerError"
            });

        var provider = CreateProvider();
        var result = await provider.ExtractTextAsync(
            new byte[] { 1, 2, 3 }, "application/pdf");

        Assert.False(result.IsSuccess);
        Assert.Equal("GeminiVision", result.Provider);
        Assert.Contains("InternalServerError", result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractTextAsync_AzureCooldown_SkipsToOcrSpace()
    {
        _pdfPigMock.Setup(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult
            {
                IsSuccess = false,
                Provider = "PdfPig",
                ErrorMessage = "Insufficient text extracted (likely a scanned document)"
            });

        // Azure fails on first call, triggering cooldown
        _azureMock.Setup(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult
            {
                IsSuccess = false,
                Provider = "AzureDocIntel",
                ErrorMessage = "Azure Document Intelligence API error (HTTP 429): Rate limit exceeded"
            });

        _ocrSpaceMock.Setup(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult
            {
                IsSuccess = true,
                ExtractedText = "PROFESSIONAL LIABILITY DECLARATION\nPolicy: PL-2024-667788\nInsured: Cornerstone Financial Advisors LLC\nCoverage: Errors and Omissions\nLimit: $500,000 per claim",
                PageCount = 1,
                Confidence = 0.85,
                Provider = "OcrSpace"
            });

        var provider = CreateProvider();

        // First call: PdfPig fails → Azure fails (triggers cooldown) → OCR Space succeeds
        var result1 = await provider.ExtractTextAsync(new byte[] { 1, 2, 3 }, "application/pdf");
        Assert.Equal("OcrSpace", result1.Provider);

        // Second call: PdfPig fails → Azure SKIPPED (cooldown) → OCR Space succeeds
        var result2 = await provider.ExtractTextAsync(new byte[] { 4, 5, 6 }, "application/pdf");
        Assert.Equal("OcrSpace", result2.Provider);

        // Azure should have been called exactly once (second call skipped due to cooldown)
        _azureMock.Verify(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        // Gemini should NEVER be called (OCR Space succeeded)
        _geminiMock.Verify(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExtractTextAsync_AzureAndOcrSpaceCooldown_SkipsToGemini()
    {
        _pdfPigMock.Setup(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult
            {
                IsSuccess = false,
                Provider = "PdfPig",
                ErrorMessage = "Insufficient text extracted (likely a scanned document)"
            });

        // Azure fails, triggering cooldown
        _azureMock.Setup(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult
            {
                IsSuccess = false,
                Provider = "AzureDocIntel",
                ErrorMessage = "Azure Document Intelligence API error (HTTP 503): Service unavailable"
            });

        // OCR Space fails, triggering cooldown
        _ocrSpaceMock.Setup(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult
            {
                IsSuccess = false,
                Provider = "OcrSpace",
                ErrorMessage = "OCR.space API error: TooManyRequests"
            });

        _geminiMock.Setup(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult
            {
                IsSuccess = true,
                ExtractedText = "CERTIFICATE OF INSURANCE\nCertificate Holder: Tri-County School District\nInsurer: National Educators Mutual\nPolicy: ED-2024-223344\nCoverage: General Liability $1,000,000",
                PageCount = 1,
                Confidence = 0.75,
                Provider = "GeminiVision"
            });

        var provider = CreateProvider();

        // First call: PdfPig fails → Azure fails (cooldown) → OCR Space fails (cooldown) → Gemini succeeds
        var result1 = await provider.ExtractTextAsync(new byte[] { 1, 2, 3 }, "application/pdf");
        Assert.Equal("GeminiVision", result1.Provider);

        // Second call: PdfPig fails → Azure SKIPPED → OCR Space SKIPPED → Gemini succeeds
        var result2 = await provider.ExtractTextAsync(new byte[] { 4, 5, 6 }, "application/pdf");
        Assert.Equal("GeminiVision", result2.Provider);

        // Azure and OCR Space should have been called exactly once each (both skipped on second call)
        _azureMock.Verify(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _ocrSpaceMock.Verify(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
