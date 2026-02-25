using Microsoft.Extensions.Logging;
using Moq;
using SentimentAnalyzer.API.Features.Claims.Commands;
using SentimentAnalyzer.API.Models;
using SentimentAnalyzer.API.Services.Claims;
using Xunit;

namespace SentimentAnalyzer.Tests;

/// <summary>
/// Tests for UploadClaimEvidenceHandler — multimodal evidence upload.
/// </summary>
public class UploadClaimEvidenceHandlerTests
{
    private readonly Mock<IMultimodalEvidenceProcessor> _mockProcessor;
    private readonly Mock<ILogger<UploadClaimEvidenceHandler>> _mockLogger;
    private readonly UploadClaimEvidenceHandler _handler;

    public UploadClaimEvidenceHandlerTests()
    {
        _mockProcessor = new Mock<IMultimodalEvidenceProcessor>();
        _mockLogger = new Mock<ILogger<UploadClaimEvidenceHandler>>();
        _handler = new UploadClaimEvidenceHandler(_mockProcessor.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ImageUpload_ReturnsVisionResult()
    {
        // Arrange
        var command = new UploadClaimEvidenceCommand(1, [0xFF, 0xD8], "image/jpeg", "damage.jpg");
        _mockProcessor.Setup(p => p.ProcessAsync(1, command.FileData, "image/jpeg", "damage.jpg"))
            .ReturnsAsync(new ClaimEvidenceResponse
            {
                EvidenceType = "image",
                Provider = "AzureVision",
                ProcessedText = "Vehicle front-end damage visible",
                DamageIndicators = ["bumper damage"]
            });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal("image", result.EvidenceType);
        Assert.Equal("AzureVision", result.Provider);
        Assert.Single(result.DamageIndicators);
    }

    [Fact]
    public async Task Handle_AudioUpload_ReturnsTranscript()
    {
        // Arrange
        var command = new UploadClaimEvidenceCommand(2, [0x52, 0x49], "audio/wav", "statement.wav");
        _mockProcessor.Setup(p => p.ProcessAsync(2, command.FileData, "audio/wav", "statement.wav"))
            .ReturnsAsync(new ClaimEvidenceResponse
            {
                EvidenceType = "audio",
                Provider = "Deepgram",
                ProcessedText = "I was driving east on Route 9 when the other driver ran a red light"
            });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal("audio", result.EvidenceType);
        Assert.Contains("red light", result.ProcessedText);
    }

    [Fact]
    public async Task Handle_PdfUpload_ReturnsOcrText()
    {
        // Arrange
        var command = new UploadClaimEvidenceCommand(3, [0x25, 0x50], "application/pdf", "report.pdf");
        _mockProcessor.Setup(p => p.ProcessAsync(3, command.FileData, "application/pdf", "report.pdf"))
            .ReturnsAsync(new ClaimEvidenceResponse
            {
                EvidenceType = "document",
                Provider = "OcrSpace",
                ProcessedText = "Police Incident Report #2024-12345"
            });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal("document", result.EvidenceType);
        Assert.Contains("Police Incident Report", result.ProcessedText);
    }

    [Fact]
    public async Task Handle_DelegatesToProcessor()
    {
        // Arrange
        var fileData = new byte[] { 0xFF, 0xD8, 0xFF };
        var command = new UploadClaimEvidenceCommand(5, fileData, "image/png", "roof.png");
        _mockProcessor.Setup(p => p.ProcessAsync(5, fileData, "image/png", "roof.png"))
            .ReturnsAsync(new ClaimEvidenceResponse { EvidenceType = "image", Provider = "AzureVision" });

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockProcessor.Verify(p => p.ProcessAsync(5, fileData, "image/png", "roof.png"), Times.Once);
    }
}
