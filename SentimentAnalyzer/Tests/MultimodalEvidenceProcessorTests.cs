using Microsoft.Extensions.Logging;
using Moq;
using SentimentAnalyzer.Agents.Orchestration;
using SentimentAnalyzer.API.Data;
using SentimentAnalyzer.API.Data.Entities;
using SentimentAnalyzer.API.Services.Claims;
using SentimentAnalyzer.API.Services.Multimodal;
using Xunit;

namespace SentimentAnalyzer.Tests;

/// <summary>
/// Tests for MultimodalEvidenceProcessor — MIME routing and NER integration.
/// </summary>
public class MultimodalEvidenceProcessorTests
{
    private readonly Mock<IImageAnalysisService> _mockImage;
    private readonly Mock<ISpeechToTextService> _mockSpeech;
    private readonly Mock<IDocumentOcrService> _mockOcr;
    private readonly Mock<IEntityExtractionService> _mockNer;
    private readonly Mock<IClaimsRepository> _mockRepo;
    private readonly Mock<ILogger<MultimodalEvidenceProcessor>> _mockLogger;
    private readonly MultimodalEvidenceProcessor _processor;

    public MultimodalEvidenceProcessorTests()
    {
        _mockImage = new Mock<IImageAnalysisService>();
        _mockSpeech = new Mock<ISpeechToTextService>();
        _mockOcr = new Mock<IDocumentOcrService>();
        _mockNer = new Mock<IEntityExtractionService>();
        _mockRepo = new Mock<IClaimsRepository>();
        _mockLogger = new Mock<ILogger<MultimodalEvidenceProcessor>>();

        _processor = new MultimodalEvidenceProcessor(
            _mockImage.Object,
            _mockSpeech.Object,
            _mockOcr.Object,
            _mockNer.Object,
            _mockRepo.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task ProcessAsync_ImageFile_RoutesToVisionService()
    {
        // Arrange
        var imageData = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }; // JPEG header
        _mockImage.Setup(s => s.AnalyzeImageAsync(imageData, "image/jpeg", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImageAnalysisResult
            {
                IsSuccess = true,
                Description = "Damaged vehicle with front-end collision impact",
                DamageIndicators = ["front bumper damage", "cracked windshield"],
                Provider = "AzureVision"
            });
        _mockNer.Setup(s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EntityExtractionResult { IsSuccess = true, Entities = [] });
        _mockRepo.Setup(r => r.SaveEvidenceAsync(It.IsAny<ClaimEvidenceRecord>()))
            .ReturnsAsync((ClaimEvidenceRecord e) => { e.Id = 1; return e; });

        // Act
        var result = await _processor.ProcessAsync(1, imageData, "image/jpeg", "damage_photo.jpg");

        // Assert
        Assert.Equal("image", result.EvidenceType);
        Assert.Equal("AzureVision", result.Provider);
        Assert.Contains("Damaged vehicle", result.ProcessedText);
        Assert.Equal(2, result.DamageIndicators.Count);
    }

    [Fact]
    public async Task ProcessAsync_AudioFile_RoutesToSpeechService()
    {
        // Arrange
        var audioData = new byte[] { 0x52, 0x49, 0x46, 0x46 }; // WAV header
        _mockSpeech.Setup(s => s.TranscribeAsync(audioData, "audio/wav", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranscriptionResult
            {
                IsSuccess = true,
                Text = "I was driving on Highway 101 when a truck rear-ended my vehicle",
                Provider = "Deepgram"
            });
        _mockNer.Setup(s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EntityExtractionResult { IsSuccess = true, Entities = [] });
        _mockRepo.Setup(r => r.SaveEvidenceAsync(It.IsAny<ClaimEvidenceRecord>()))
            .ReturnsAsync((ClaimEvidenceRecord e) => { e.Id = 2; return e; });

        // Act
        var result = await _processor.ProcessAsync(1, audioData, "audio/wav", "witness_statement.wav");

        // Assert
        Assert.Equal("audio", result.EvidenceType);
        Assert.Equal("Deepgram", result.Provider);
        Assert.Contains("Highway 101", result.ProcessedText);
    }

    [Fact]
    public async Task ProcessAsync_PdfFile_RoutesToOcrService()
    {
        // Arrange
        var pdfData = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // PDF header
        _mockOcr.Setup(s => s.ExtractTextAsync(pdfData, "application/pdf", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult
            {
                IsSuccess = true,
                ExtractedText = "Police Report #2024-45678. Accident occurred at intersection of Main and 5th.",
                Provider = "OcrSpace"
            });
        _mockNer.Setup(s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EntityExtractionResult { IsSuccess = true, Entities = [] });
        _mockRepo.Setup(r => r.SaveEvidenceAsync(It.IsAny<ClaimEvidenceRecord>()))
            .ReturnsAsync((ClaimEvidenceRecord e) => { e.Id = 3; return e; });

        // Act
        var result = await _processor.ProcessAsync(1, pdfData, "application/pdf", "police_report.pdf");

        // Assert
        Assert.Equal("document", result.EvidenceType);
        Assert.Equal("OcrSpace", result.Provider);
        Assert.Contains("Police Report", result.ProcessedText);
    }

    [Fact]
    public async Task ProcessAsync_RunsNerOnProcessedText()
    {
        // Arrange
        var imageData = new byte[] { 0xFF, 0xD8 };
        _mockImage.Setup(s => s.AnalyzeImageAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImageAnalysisResult
            {
                IsSuccess = true,
                Description = "Water-damaged basement with visible mold growth",
                Provider = "AzureVision"
            });
        _mockNer.Setup(s => s.ExtractEntitiesAsync("Water-damaged basement with visible mold growth", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EntityExtractionResult
            {
                IsSuccess = true,
                Entities = [new ExtractedEntity { Type = "DAMAGE", Value = "mold growth", Confidence = 0.9 }]
            });
        _mockRepo.Setup(r => r.SaveEvidenceAsync(It.IsAny<ClaimEvidenceRecord>()))
            .ReturnsAsync((ClaimEvidenceRecord e) => { e.Id = 4; return e; });

        // Act
        await _processor.ProcessAsync(1, imageData, "image/jpeg", "basement.jpg");

        // Assert
        _mockNer.Verify(s => s.ExtractEntitiesAsync("Water-damaged basement with visible mold growth", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_UnsupportedMimeType_ReturnsEmptyResult()
    {
        // Arrange
        var data = new byte[] { 0x00 };
        _mockNer.Setup(s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EntityExtractionResult { IsSuccess = true, Entities = [] });
        _mockRepo.Setup(r => r.SaveEvidenceAsync(It.IsAny<ClaimEvidenceRecord>()))
            .ReturnsAsync((ClaimEvidenceRecord e) => { e.Id = 5; return e; });

        // Act
        var result = await _processor.ProcessAsync(1, data, "application/x-unknown", "file.xyz");

        // Assert
        Assert.Equal("unknown", result.EvidenceType);
        Assert.Equal("None", result.Provider);
        Assert.Empty(result.ProcessedText);
    }

    [Fact]
    public async Task ProcessAsync_PersistsEvidenceRecord()
    {
        // Arrange
        var audioData = new byte[] { 0x52, 0x49 };
        _mockSpeech.Setup(s => s.TranscribeAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranscriptionResult { IsSuccess = true, Text = "Statement recorded", Provider = "Deepgram" });
        _mockNer.Setup(s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EntityExtractionResult { IsSuccess = true, Entities = [] });
        _mockRepo.Setup(r => r.SaveEvidenceAsync(It.IsAny<ClaimEvidenceRecord>()))
            .ReturnsAsync((ClaimEvidenceRecord e) => { e.Id = 6; return e; });

        // Act
        await _processor.ProcessAsync(1, audioData, "audio/mp3", "statement.mp3");

        // Assert
        _mockRepo.Verify(r => r.SaveEvidenceAsync(It.Is<ClaimEvidenceRecord>(e =>
            e.ClaimId == 1 &&
            e.EvidenceType == "audio" &&
            e.Provider == "Deepgram"
        )), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_NerFailure_ContinuesWithoutEntities()
    {
        // Arrange
        var imageData = new byte[] { 0xFF };
        _mockImage.Setup(s => s.AnalyzeImageAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImageAnalysisResult { IsSuccess = true, Description = "Car damage photo", Provider = "AzureVision" });
        _mockNer.Setup(s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("NER service unavailable"));
        _mockRepo.Setup(r => r.SaveEvidenceAsync(It.IsAny<ClaimEvidenceRecord>()))
            .ReturnsAsync((ClaimEvidenceRecord e) => { e.Id = 7; return e; });

        // Act
        var result = await _processor.ProcessAsync(1, imageData, "image/jpeg", "car.jpg");

        // Assert — should not throw, evidence still saved
        Assert.Equal("image", result.EvidenceType);
        Assert.Equal("AzureVision", result.Provider);
    }

    [Fact]
    public async Task ProcessAsync_ImagePrimaryFails_FallsBackToCloudflare()
    {
        // Arrange — processor with fallback service
        var mockFallback = new Mock<IImageAnalysisService>();
        var processorWithFallback = new MultimodalEvidenceProcessor(
            _mockImage.Object, _mockSpeech.Object, _mockOcr.Object,
            _mockNer.Object, _mockRepo.Object, _mockLogger.Object,
            mockFallback.Object);

        var imageData = new byte[] { 0xFF, 0xD8 };
        _mockImage.Setup(s => s.AnalyzeImageAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImageAnalysisResult { IsSuccess = false, ErrorMessage = "AzureVision quota exceeded" });
        mockFallback.Setup(s => s.AnalyzeImageAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImageAnalysisResult
            {
                IsSuccess = true,
                Description = "Vehicle damage detected via fallback",
                Provider = "CloudflareVision",
                DamageIndicators = ["rear damage"]
            });
        _mockNer.Setup(s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EntityExtractionResult { IsSuccess = true, Entities = [] });
        _mockRepo.Setup(r => r.SaveEvidenceAsync(It.IsAny<ClaimEvidenceRecord>()))
            .ReturnsAsync((ClaimEvidenceRecord e) => { e.Id = 8; return e; });

        // Act
        var result = await processorWithFallback.ProcessAsync(1, imageData, "image/jpeg", "crash.jpg");

        // Assert — Cloudflare fallback was used
        Assert.Equal("image", result.EvidenceType);
        Assert.Equal("CloudflareVision", result.Provider);
        Assert.Contains("fallback", result.ProcessedText);
        mockFallback.Verify(s => s.AnalyzeImageAsync(imageData, "image/jpeg", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_BothVisionServicesFail_ReturnsPrimaryResult()
    {
        // Arrange — processor with fallback service, both fail
        var mockFallback = new Mock<IImageAnalysisService>();
        var processorWithFallback = new MultimodalEvidenceProcessor(
            _mockImage.Object, _mockSpeech.Object, _mockOcr.Object,
            _mockNer.Object, _mockRepo.Object, _mockLogger.Object,
            mockFallback.Object);

        var imageData = new byte[] { 0xFF, 0xD8 };
        _mockImage.Setup(s => s.AnalyzeImageAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImageAnalysisResult { IsSuccess = false, ErrorMessage = "Azure down", Provider = "AzureVision" });
        mockFallback.Setup(s => s.AnalyzeImageAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImageAnalysisResult { IsSuccess = false, ErrorMessage = "Cloudflare down too" });
        _mockNer.Setup(s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EntityExtractionResult { IsSuccess = true, Entities = [] });
        _mockRepo.Setup(r => r.SaveEvidenceAsync(It.IsAny<ClaimEvidenceRecord>()))
            .ReturnsAsync((ClaimEvidenceRecord e) => { e.Id = 9; return e; });

        // Act
        var result = await processorWithFallback.ProcessAsync(1, imageData, "image/jpeg", "damage.jpg");

        // Assert — still processes without throwing, returns primary provider info
        Assert.Equal("image", result.EvidenceType);
        Assert.Equal("AzureVision", result.Provider);
    }

    [Fact]
    public async Task ProcessAsync_FallbackThrowsException_GracefulDegradation()
    {
        // Arrange — fallback throws exception instead of returning failure
        var mockFallback = new Mock<IImageAnalysisService>();
        var processorWithFallback = new MultimodalEvidenceProcessor(
            _mockImage.Object, _mockSpeech.Object, _mockOcr.Object,
            _mockNer.Object, _mockRepo.Object, _mockLogger.Object,
            mockFallback.Object);

        var imageData = new byte[] { 0xFF, 0xD8 };
        _mockImage.Setup(s => s.AnalyzeImageAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImageAnalysisResult { IsSuccess = false, ErrorMessage = "Azure timeout" });
        mockFallback.Setup(s => s.AnalyzeImageAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Cloudflare network error"));
        _mockNer.Setup(s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EntityExtractionResult { IsSuccess = true, Entities = [] });
        _mockRepo.Setup(r => r.SaveEvidenceAsync(It.IsAny<ClaimEvidenceRecord>()))
            .ReturnsAsync((ClaimEvidenceRecord e) => { e.Id = 10; return e; });

        // Act — should NOT throw
        var result = await processorWithFallback.ProcessAsync(1, imageData, "image/jpeg", "photo.jpg");

        // Assert — graceful degradation
        Assert.Equal("image", result.EvidenceType);
    }

    [Fact]
    public async Task ProcessAsync_WithPiiRedactor_RedactsTextBeforePersistence()
    {
        // Arrange — processor with PII redactor
        var mockRedactor = new Mock<IPIIRedactor>();
        mockRedactor.Setup(r => r.Redact(It.IsAny<string>()))
            .Returns<string>(s => s.Replace("HO-2024-999999", "[POLICY-REDACTED]"));

        var processorWithRedactor = new MultimodalEvidenceProcessor(
            _mockImage.Object, _mockSpeech.Object, _mockOcr.Object,
            _mockNer.Object, _mockRepo.Object, _mockLogger.Object,
            piiRedactor: mockRedactor.Object);

        var audioData = new byte[] { 0x52, 0x49, 0x46, 0x46 };
        _mockSpeech.Setup(s => s.TranscribeAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranscriptionResult
            {
                IsSuccess = true,
                Text = "Claim filed on policy HO-2024-999999 for water damage",
                Provider = "Deepgram"
            });
        _mockNer.Setup(s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EntityExtractionResult { IsSuccess = true, Entities = [] });

        ClaimEvidenceRecord? savedRecord = null;
        _mockRepo.Setup(r => r.SaveEvidenceAsync(It.IsAny<ClaimEvidenceRecord>()))
            .Callback<ClaimEvidenceRecord>(e => savedRecord = e)
            .ReturnsAsync((ClaimEvidenceRecord e) => { e.Id = 20; return e; });

        // Act
        var result = await processorWithRedactor.ProcessAsync(1, audioData, "audio/wav", "statement.wav");

        // Assert — persisted text is PII-redacted
        Assert.NotNull(savedRecord);
        Assert.Contains("[POLICY-REDACTED]", savedRecord!.ProcessedText);
        Assert.DoesNotContain("HO-2024-999999", savedRecord.ProcessedText);
        // API response also redacted
        Assert.Contains("[POLICY-REDACTED]", result.ProcessedText);
        Assert.DoesNotContain("HO-2024-999999", result.ProcessedText);
    }

    [Fact]
    public async Task ProcessAsync_WithoutPiiRedactor_PersistsRawText()
    {
        // Arrange — processor without PII redactor (backward compat)
        var audioData = new byte[] { 0x52, 0x49, 0x46, 0x46 };
        _mockSpeech.Setup(s => s.TranscribeAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranscriptionResult
            {
                IsSuccess = true,
                Text = "Claim on policy HO-2024-888888",
                Provider = "Deepgram"
            });
        _mockNer.Setup(s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EntityExtractionResult { IsSuccess = true, Entities = [] });

        ClaimEvidenceRecord? savedRecord = null;
        _mockRepo.Setup(r => r.SaveEvidenceAsync(It.IsAny<ClaimEvidenceRecord>()))
            .Callback<ClaimEvidenceRecord>(e => savedRecord = e)
            .ReturnsAsync((ClaimEvidenceRecord e) => { e.Id = 21; return e; });

        // Act
        var result = await _processor.ProcessAsync(1, audioData, "audio/wav", "statement.wav");

        // Assert — raw text persisted when no redactor (backward compat)
        Assert.NotNull(savedRecord);
        Assert.Contains("HO-2024-888888", savedRecord!.ProcessedText);
    }

    [Fact]
    public async Task ProcessAsync_RedactsPiiEntityValuesInEntitiesJson()
    {
        // Arrange — NER returns PII entities (SSN, POLICY_NUMBER, PERSON) and non-PII (DAMAGE, LOCATION)
        var imageData = new byte[] { 0xFF, 0xD8 };
        _mockImage.Setup(s => s.AnalyzeImageAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImageAnalysisResult
            {
                IsSuccess = true,
                Description = "Water damage in basement with mold",
                Provider = "AzureVision"
            });
        _mockNer.Setup(s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EntityExtractionResult
            {
                IsSuccess = true,
                Entities =
                [
                    new ExtractedEntity { Type = "SSN", Value = "123-45-6789", Confidence = 0.95 },
                    new ExtractedEntity { Type = "POLICY_NUMBER", Value = "HO-2024-999999", Confidence = 0.92 },
                    new ExtractedEntity { Type = "DAMAGE", Value = "mold growth", Confidence = 0.88 },
                    new ExtractedEntity { Type = "PERSON", Value = "Jane Policyholder", Confidence = 0.85 }
                ]
            });

        ClaimEvidenceRecord? savedRecord = null;
        _mockRepo.Setup(r => r.SaveEvidenceAsync(It.IsAny<ClaimEvidenceRecord>()))
            .Callback<ClaimEvidenceRecord>(e => savedRecord = e)
            .ReturnsAsync((ClaimEvidenceRecord e) => { e.Id = 30; return e; });

        // Act
        await _processor.ProcessAsync(1, imageData, "image/jpeg", "basement.jpg");

        // Assert — PII entities are redacted (SSN, POLICY_NUMBER, PERSON), non-PII preserved
        Assert.NotNull(savedRecord);
        Assert.Contains("[SSN-REDACTED]", savedRecord!.EntitiesJson);
        Assert.Contains("[POLICY_NUMBER-REDACTED]", savedRecord.EntitiesJson);
        Assert.Contains("[PERSON-REDACTED]", savedRecord.EntitiesJson);
        Assert.DoesNotContain("123-45-6789", savedRecord.EntitiesJson);
        Assert.DoesNotContain("HO-2024-999999", savedRecord.EntitiesJson);
        Assert.DoesNotContain("Jane Policyholder", savedRecord.EntitiesJson);
        // Non-PII entities keep their values
        Assert.Contains("mold growth", savedRecord.EntitiesJson);
    }

    [Fact]
    public async Task ProcessAsync_RedactsPersonEntityType_GdprCcpaCompliance()
    {
        // Arrange — HuggingFaceNerService maps BERT "PER" → "PERSON" before entities reach processor
        var imageData = new byte[] { 0xFF, 0xD8 };
        _mockImage.Setup(s => s.AnalyzeImageAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImageAnalysisResult
            {
                IsSuccess = true,
                Description = "Person identified near damaged property",
                Provider = "AzureVision"
            });
        _mockNer.Setup(s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EntityExtractionResult
            {
                IsSuccess = true,
                Entities =
                [
                    new ExtractedEntity { Type = "PERSON", Value = "John Smith", Confidence = 0.92 },
                    new ExtractedEntity { Type = "ORGANIZATION", Value = "Acme Insurance", Confidence = 0.88 },
                    new ExtractedEntity { Type = "LOCATION", Value = "Springfield", Confidence = 0.85 },
                    new ExtractedEntity { Type = "EMAIL", Value = "john@example.com", Confidence = 0.95 }
                ]
            });

        ClaimEvidenceRecord? savedRecord = null;
        _mockRepo.Setup(r => r.SaveEvidenceAsync(It.IsAny<ClaimEvidenceRecord>()))
            .Callback<ClaimEvidenceRecord>(e => savedRecord = e)
            .ReturnsAsync((ClaimEvidenceRecord e) => { e.Id = 31; return e; });

        // Act
        await _processor.ProcessAsync(1, imageData, "image/jpeg", "scene.jpg");

        // Assert — PERSON and EMAIL redacted; ORGANIZATION and LOCATION preserved
        Assert.NotNull(savedRecord);
        Assert.Contains("[PERSON-REDACTED]", savedRecord!.EntitiesJson);
        Assert.DoesNotContain("John Smith", savedRecord.EntitiesJson);
        Assert.Contains("[EMAIL-REDACTED]", savedRecord.EntitiesJson);
        Assert.DoesNotContain("john@example.com", savedRecord.EntitiesJson);
        // Non-PII entities preserved
        Assert.Contains("Acme Insurance", savedRecord.EntitiesJson);
        Assert.Contains("Springfield", savedRecord.EntitiesJson);
    }
}
