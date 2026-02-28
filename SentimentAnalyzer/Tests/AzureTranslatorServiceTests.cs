using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using SentimentAnalyzer.Agents.Configuration;
using SentimentAnalyzer.Agents.Orchestration;
using SentimentAnalyzer.API.Services.Multimodal;
using Xunit;

namespace SentimentAnalyzer.Tests;

/// <summary>
/// Tests for AzureTranslatorService (Azure AI Translator — multilingual claims processing).
/// Uses mocked HttpMessageHandler to simulate Azure Translator API responses with insurance-realistic content.
/// </summary>
public class AzureTranslatorServiceTests
{
    private static (AzureTranslatorService service, Mock<HttpMessageHandler> handler) CreateServiceWithMockHttp(
        string apiKey = "translator_key_ins_2024",
        string region = "eastus",
        IPIIRedactor? piiRedactor = null)
    {
        var handler = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(handler.Object);
        var settings = new AgentSystemSettings
        {
            AzureTranslator = new AzureTranslatorSettings
            {
                ApiKey = apiKey,
                Endpoint = "https://api.cognitive.microsofttranslator.com",
                Region = region
            }
        };
        var service = new AzureTranslatorService(
            httpClient,
            Options.Create(settings),
            new Mock<ILogger<AzureTranslatorService>>().Object,
            piiRedactor);
        return (service, handler);
    }

    private static void SetupMockResponse(
        Mock<HttpMessageHandler> handler,
        HttpStatusCode statusCode,
        string responseBody)
    {
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            });
    }

    [Fact]
    public async Task TranslateAsync_MissingApiKey_ReturnsFailure()
    {
        var (service, _) = CreateServiceWithMockHttp(apiKey: "");

        var result = await service.TranslateAsync(
            "El asegurado reporta daños por agua en la propiedad residencial.");

        Assert.False(result.IsSuccess);
        Assert.Equal("AzureTranslator", result.Provider);
        Assert.Contains("not configured", result.ErrorMessage);
    }

    [Fact]
    public async Task DetectLanguageAsync_MissingApiKey_ReturnsFailure()
    {
        var (service, _) = CreateServiceWithMockHttp(apiKey: "");

        var result = await service.DetectLanguageAsync(
            "Reclamación de seguro de automóvil por colisión en la autopista.");

        Assert.False(result.IsSuccess);
        Assert.Equal("AzureTranslator", result.Provider);
        Assert.Contains("not configured", result.ErrorMessage);
    }

    [Fact]
    public async Task TranslateAsync_ValidResponse_ReturnsTranslation()
    {
        var (service, handler) = CreateServiceWithMockHttp();

        var responseJson = """
        [{
            "detectedLanguage": { "language": "es", "score": 0.99 },
            "translations": [{
                "text": "The insured reports water damage to the residential property located at 789 Main Street.",
                "to": "en"
            }]
        }]
        """;
        SetupMockResponse(handler, HttpStatusCode.OK, responseJson);

        var result = await service.TranslateAsync(
            "El asegurado reporta daños por agua en la propiedad residencial ubicada en la Calle Principal 789.");

        Assert.True(result.IsSuccess);
        Assert.Equal("AzureTranslator", result.Provider);
        Assert.Equal("es", result.DetectedSourceLanguage);
        Assert.Equal(0.99, result.Confidence);
        Assert.Contains("water damage", result.TranslatedText);
        Assert.Contains("residential property", result.TranslatedText);
    }

    [Fact]
    public async Task DetectLanguageAsync_ValidResponse_ReturnsDetectedLanguage()
    {
        var (service, handler) = CreateServiceWithMockHttp();

        var responseJson = """
        [{
            "language": "es",
            "score": 0.97,
            "isTranslationSupported": true,
            "isTransliterationSupported": false
        }]
        """;
        SetupMockResponse(handler, HttpStatusCode.OK, responseJson);

        var result = await service.DetectLanguageAsync(
            "Reclamación de seguro de automóvil por colisión en la autopista.");

        Assert.True(result.IsSuccess);
        Assert.Equal("AzureTranslator", result.Provider);
        Assert.Equal("es", result.DetectedLanguage);
        Assert.Equal("Spanish", result.LanguageName);
        Assert.Equal(0.97, result.Confidence);
    }

    [Fact]
    public async Task TranslateAsync_ApiError_ReturnsFailure()
    {
        var (service, handler) = CreateServiceWithMockHttp();

        SetupMockResponse(handler, HttpStatusCode.InternalServerError,
            """{"error":{"code":"500","message":"Azure Translator service temporarily unavailable"}}""");

        var result = await service.TranslateAsync(
            "Demande d'indemnisation pour dommages causés par une tempête à la toiture.");

        Assert.False(result.IsSuccess);
        Assert.Equal("AzureTranslator", result.Provider);
        Assert.Contains("InternalServerError", result.ErrorMessage);
    }

    [Fact]
    public async Task TranslateAsync_EmptyText_ReturnsFailure()
    {
        var (service, _) = CreateServiceWithMockHttp();

        var result = await service.TranslateAsync("");

        Assert.False(result.IsSuccess);
        Assert.Equal("AzureTranslator", result.Provider);
        Assert.Contains("empty", result.ErrorMessage);
    }

    [Fact]
    public async Task Provider_ReturnsAzureTranslator()
    {
        // Even on validation failure, the provider name must be "AzureTranslator"
        var (service, _) = CreateServiceWithMockHttp(apiKey: "");

        var translateResult = await service.TranslateAsync(
            "Schadensmeldung für Kfz-Versicherung nach Auffahrunfall.");
        var detectResult = await service.DetectLanguageAsync(
            "Schadensmeldung für Kfz-Versicherung nach Auffahrunfall.");

        Assert.Equal("AzureTranslator", translateResult.Provider);
        Assert.Equal("AzureTranslator", detectResult.Provider);
    }

    [Fact]
    public async Task TranslateAsync_WithPiiRedaction_RedactsBeforeSending()
    {
        var piiRedactorMock = new Mock<IPIIRedactor>();
        piiRedactorMock
            .Setup(p => p.Redact(It.IsAny<string>()))
            .Returns<string>(s => s.Replace("456-78-9012", "[SSN-REDACTED]"));

        var (service, handler) = CreateServiceWithMockHttp(piiRedactor: piiRedactorMock.Object);

        var responseJson = """
        [{
            "detectedLanguage": { "language": "es", "score": 0.95 },
            "translations": [{
                "text": "Workers compensation claim for insured [SSN-REDACTED], injury at manufacturing plant on March 15, 2024.",
                "to": "en"
            }]
        }]
        """;
        SetupMockResponse(handler, HttpStatusCode.OK, responseJson);

        var result = await service.TranslateAsync(
            "Reclamación de compensación laboral para asegurado 456-78-9012, lesión en planta de manufactura el 15 de marzo de 2024.");

        Assert.True(result.IsSuccess);
        piiRedactorMock.Verify(p => p.Redact(It.IsAny<string>()), Times.Once);
    }
}
