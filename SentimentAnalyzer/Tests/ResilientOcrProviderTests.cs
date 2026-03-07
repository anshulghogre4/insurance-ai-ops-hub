using Microsoft.Extensions.Logging;
using Moq;
using SentimentAnalyzer.API.Services.Multimodal;
using Xunit;

namespace SentimentAnalyzer.Tests;

/// <summary>
/// Tests for ResilientOcrProvider.
/// Validates 6-tier fallback chain ordered by data safety:
/// PdfPig (local) → Tesseract (local) → Azure DocIntel → Mistral OCR → OCR Space → Gemini Vision.
/// </summary>
public class ResilientOcrProviderTests
{
    private readonly Mock<IDocumentOcrService> _pdfPigMock = new();
    private readonly Mock<IDocumentOcrService> _tesseractMock = new();
    private readonly Mock<IDocumentOcrService> _azureMock = new();
    private readonly Mock<IDocumentOcrService> _mistralOcrMock = new();
    private readonly Mock<IDocumentOcrService> _ocrSpaceMock = new();
    private readonly Mock<IDocumentOcrService> _geminiMock = new();
    private readonly Mock<ILogger<ResilientOcrProvider>> _loggerMock = new();

    private ResilientOcrProvider CreateProvider()
    {
        return new ResilientOcrProvider(
            _pdfPigMock.Object,
            _tesseractMock.Object,
            _azureMock.Object,
            _mistralOcrMock.Object,
            _ocrSpaceMock.Object,
            _geminiMock.Object,
            _loggerMock.Object);
    }

    private void SetupLocalProvidersFail()
    {
        _pdfPigMock.Setup(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult
            {
                IsSuccess = false,
                Provider = "PdfPig",
                ErrorMessage = "Insufficient text extracted (likely a scanned document)"
            });

        _tesseractMock.Setup(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult
            {
                IsSuccess = false,
                Provider = "Tesseract",
                ErrorMessage = "Tesseract tessdata not configured"
            });
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

        // No other providers should be called
        _tesseractMock.Verify(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _azureMock.Verify(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mistralOcrMock.Verify(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _ocrSpaceMock.Verify(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _geminiMock.Verify(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExtractTextAsync_PdfPigFails_TesseractSucceeds_ReturnsTesseractResult()
    {
        _pdfPigMock.Setup(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult
            {
                IsSuccess = false,
                Provider = "PdfPig",
                ErrorMessage = "Insufficient text extracted (likely a scanned document)"
            });

        _tesseractMock.Setup(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult
            {
                IsSuccess = true,
                ExtractedText = "SCANNED CLAIM FORM\nClaim: CLM-2024-112233\nAdjuster: Field Services\nDamage Type: Water intrusion from roof leak",
                PageCount = 2,
                Confidence = 0.88,
                Provider = "Tesseract"
            });

        var provider = CreateProvider();
        var result = await provider.ExtractTextAsync(new byte[] { 1, 2, 3 }, "application/pdf");

        Assert.True(result.IsSuccess);
        Assert.Equal("Tesseract", result.Provider);
        Assert.Contains("CLM-2024-112233", result.ExtractedText);

        _azureMock.Verify(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExtractTextAsync_LocalProvidersFail_FallsBackToAzure()
    {
        SetupLocalProvidersFail();

        _azureMock.Setup(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult
            {
                IsSuccess = true,
                ExtractedText = "PROPERTY DAMAGE ASSESSMENT\nClaim: CLM-2024-887766\nAdjuster: Field Services Division\nDamage: Wind and hail damage to roof and siding\nEstimate: $18,500",
                PageCount = 2,
                Confidence = 0.92,
                Provider = "AzureDocIntel"
            });

        var provider = CreateProvider();
        var result = await provider.ExtractTextAsync(new byte[] { 1, 2, 3 }, "application/pdf");

        Assert.True(result.IsSuccess);
        Assert.Equal("AzureDocIntel", result.Provider);
        Assert.Contains("CLM-2024-887766", result.ExtractedText);

        _mistralOcrMock.Verify(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExtractTextAsync_LocalAndAzureFail_FallsBackToMistralOcr()
    {
        SetupLocalProvidersFail();

        _azureMock.Setup(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult { IsSuccess = false, Provider = "AzureDocIntel", ErrorMessage = "API key not configured" });

        _mistralOcrMock.Setup(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult
            {
                IsSuccess = true,
                ExtractedText = "WORKERS COMPENSATION CLAIM\nPolicy: WC-2024-556677\nEmployee: Warehouse Staff\nInjury: Lower back strain during material handling",
                PageCount = 3,
                Confidence = 0.90,
                Provider = "MistralOCR"
            });

        var provider = CreateProvider();
        var result = await provider.ExtractTextAsync(new byte[] { 1, 2, 3 }, "application/pdf");

        Assert.True(result.IsSuccess);
        Assert.Equal("MistralOCR", result.Provider);
        Assert.Contains("WC-2024-556677", result.ExtractedText);

        _ocrSpaceMock.Verify(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExtractTextAsync_FirstFourFail_FallsBackToOcrSpace()
    {
        SetupLocalProvidersFail();

        _azureMock.Setup(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult { IsSuccess = false, Provider = "AzureDocIntel", ErrorMessage = "Rate limit exceeded" });

        _mistralOcrMock.Setup(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult { IsSuccess = false, Provider = "MistralOCR", ErrorMessage = "API key not configured" });

        _ocrSpaceMock.Setup(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult
            {
                IsSuccess = true,
                ExtractedText = "AUTO INSURANCE CLAIM\nPolicy: AUTO-2024-334455\nVehicle: 2024 Honda Accord EX-L\nRepair Estimate: $6,200",
                PageCount = 1,
                Confidence = 0.85,
                Provider = "OcrSpace"
            });

        var provider = CreateProvider();
        var result = await provider.ExtractTextAsync(new byte[] { 1, 2, 3 }, "application/pdf");

        Assert.True(result.IsSuccess);
        Assert.Equal("OcrSpace", result.Provider);

        _geminiMock.Verify(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExtractTextAsync_FirstFiveFail_FallsBackToGemini()
    {
        SetupLocalProvidersFail();

        _azureMock.Setup(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult { IsSuccess = false, Provider = "AzureDocIntel", ErrorMessage = "Not configured" });
        _mistralOcrMock.Setup(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult { IsSuccess = false, Provider = "MistralOCR", ErrorMessage = "Not configured" });
        _ocrSpaceMock.Setup(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult { IsSuccess = false, Provider = "OcrSpace", ErrorMessage = "Not configured" });

        _geminiMock.Setup(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult
            {
                IsSuccess = true,
                ExtractedText = "UMBRELLA LIABILITY ENDORSEMENT\nPolicy: UMB-2024-998877\nLimit: $2,000,000 per occurrence",
                PageCount = 1,
                Confidence = 0.75,
                Provider = "GeminiVision"
            });

        var provider = CreateProvider();
        var result = await provider.ExtractTextAsync(new byte[] { 1, 2, 3 }, "application/pdf");

        Assert.True(result.IsSuccess);
        Assert.Equal("GeminiVision", result.Provider);
    }

    [Fact]
    public async Task ExtractTextAsync_AllFail_ReturnsGeminiError()
    {
        SetupLocalProvidersFail();

        _azureMock.Setup(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult { IsSuccess = false, Provider = "AzureDocIntel", ErrorMessage = "Service unavailable" });
        _mistralOcrMock.Setup(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult { IsSuccess = false, Provider = "MistralOCR", ErrorMessage = "Service unavailable" });
        _ocrSpaceMock.Setup(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult { IsSuccess = false, Provider = "OcrSpace", ErrorMessage = "Service unavailable" });
        _geminiMock.Setup(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult { IsSuccess = false, Provider = "GeminiVision", ErrorMessage = "InternalServerError" });

        var provider = CreateProvider();
        var result = await provider.ExtractTextAsync(new byte[] { 1, 2, 3 }, "application/pdf");

        Assert.False(result.IsSuccess);
        Assert.Equal("GeminiVision", result.Provider);
        Assert.Contains("InternalServerError", result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractTextAsync_AzureCooldown_SkipsToMistralOcr()
    {
        SetupLocalProvidersFail();

        _azureMock.Setup(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult { IsSuccess = false, Provider = "AzureDocIntel", ErrorMessage = "Rate limit exceeded" });

        _mistralOcrMock.Setup(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult
            {
                IsSuccess = true,
                ExtractedText = "PROFESSIONAL LIABILITY DECLARATION\nPolicy: PL-2024-667788\nInsured: Cornerstone Financial Advisors LLC",
                PageCount = 1,
                Confidence = 0.90,
                Provider = "MistralOCR"
            });

        var provider = CreateProvider();

        // First call: Azure fails (triggers cooldown) → Mistral succeeds
        var result1 = await provider.ExtractTextAsync(new byte[] { 1, 2, 3 }, "application/pdf");
        Assert.Equal("MistralOCR", result1.Provider);

        // Second call: Azure SKIPPED (cooldown) → Mistral succeeds
        var result2 = await provider.ExtractTextAsync(new byte[] { 4, 5, 6 }, "application/pdf");
        Assert.Equal("MistralOCR", result2.Provider);

        // Azure called only once (second call skipped due to cooldown)
        _azureMock.Verify(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExtractTextAsync_AzurePartialExtraction_FallsThrough()
    {
        _pdfPigMock.Setup(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult
            {
                IsSuccess = false,
                PageCount = 13, // PdfPig detected 13 pages
                Provider = "PdfPig",
                ErrorMessage = "Insufficient text extracted"
            });

        _tesseractMock.Setup(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult { IsSuccess = false, Provider = "Tesseract", ErrorMessage = "No tessdata" });

        _azureMock.Setup(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult
            {
                IsSuccess = true,
                PageCount = 2, // Azure only got 2 of 13 pages
                ExtractedText = "Page 1 and 2 only",
                Confidence = 0.95,
                Provider = "AzureDocIntel"
            });

        _mistralOcrMock.Setup(s => s.ExtractTextAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult
            {
                IsSuccess = true,
                ExtractedText = "Full 13-page insurance policy document extracted by Mistral OCR",
                PageCount = 13,
                Confidence = 0.90,
                Provider = "MistralOCR"
            });

        var provider = CreateProvider();
        var result = await provider.ExtractTextAsync(new byte[] { 1, 2, 3 }, "application/pdf");

        // Should skip Azure's partial result and use Mistral's full extraction
        Assert.True(result.IsSuccess);
        Assert.Equal("MistralOCR", result.Provider);
        Assert.Equal(13, result.PageCount);
    }
}
