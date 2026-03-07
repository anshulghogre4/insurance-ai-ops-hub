using MediatR;
using Microsoft.Extensions.Options;
using SentimentAnalyzer.Agents.Configuration;
using SentimentAnalyzer.Agents.Orchestration;
using SentimentAnalyzer.API.Features.Health.Queries;
using SentimentAnalyzer.API.Models;

namespace SentimentAnalyzer.API.Endpoints;

/// <summary>
/// Minimal API endpoint mappings for provider health monitoring.
/// </summary>
public static class ProviderHealthEndpoints
{
    public static RouteGroupBuilder MapProviderHealthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/insurance/health")
            .WithTags("Provider Health");

        // AllowAnonymous by design: health endpoints must be reachable by monitoring
        // tools, load balancers, and the frontend status widget without auth tokens.
        group.MapGet("/providers", GetProviderHealthAsync)
            .WithName("GetProviderHealth")
            .WithDescription("Get health status of all LLM providers and multimodal services.")
            .AllowAnonymous();

        group.MapGet("/providers/extended", GetExtendedProviderHealthAsync)
            .WithName("GetExtendedProviderHealth")
            .WithDescription("Get categorized health status of all provider chains.")
            .AllowAnonymous();

        return group;
    }

    private static async Task<IResult> GetProviderHealthAsync(IMediator mediator)
    {
        var result = await mediator.Send(new GetProviderHealthQuery());
        return Results.Ok(result);
    }

    /// <summary>
    /// Returns extended provider health with all providers categorized by service type.
    /// </summary>
    private static Task<IResult> GetExtendedProviderHealthAsync(
        IMediator mediator,
        IResilientKernelProvider kernelProvider,
        IOptions<AgentSystemSettings> settingsOptions)
    {
        var settings = settingsOptions.Value;
        var healthStatus = kernelProvider.GetHealthStatus();

        var response = new ExtendedProviderHealthResponse
        {
            LlmProviders = healthStatus.Select(kvp => new LlmProviderHealth
            {
                Name = kvp.Key,
                Status = kvp.Value.IsAvailable ? "Healthy" :
                    kvp.Value.ConsecutiveFailures > 3 ? "Down" : "Degraded",
                IsAvailable = kvp.Value.IsAvailable,
                ConsecutiveFailures = kvp.Value.ConsecutiveFailures,
                CooldownSeconds = kvp.Value.CooldownDuration.TotalSeconds,
                CooldownExpiresUtc = kvp.Value.CooldownExpiresUtc
            }).ToList(),

            EmbeddingProviders = BuildEmbeddingChainHealth(settings),
            OcrProviders = BuildOcrChainHealth(settings),
            NerProviders = BuildNerChainHealth(settings),
            SttProviders = BuildSttChainHealth(settings),
            ContentSafety = BuildContentSafetyHealth(settings),
            Translation = BuildTranslationHealth(settings),
            CheckedAt = DateTime.UtcNow
        };

        return Task.FromResult(Results.Ok(response));
    }

    /// <summary>
    /// Builds embedding provider chain health: Voyage AI → Cohere → Gemini → HuggingFace → Jina → Ollama.
    /// </summary>
    private static List<ProviderChainHealth> BuildEmbeddingChainHealth(AgentSystemSettings settings)
    {
        return
        [
            new ProviderChainHealth
            {
                Name = "Voyage AI",
                ChainOrder = 1,
                IsConfigured = HasApiKey(settings.Voyage.ApiKey),
                Status = HasApiKey(settings.Voyage.ApiKey) ? "Healthy" : "NotConfigured",
                IsAvailable = HasApiKey(settings.Voyage.ApiKey),
                FreeTierLimit = "50M tokens"
            },
            new ProviderChainHealth
            {
                Name = "Cohere",
                ChainOrder = 2,
                IsConfigured = HasApiKey(settings.CohereEmbedding.ApiKey),
                Status = HasApiKey(settings.CohereEmbedding.ApiKey) ? "Healthy" : "NotConfigured",
                IsAvailable = HasApiKey(settings.CohereEmbedding.ApiKey),
                FreeTierLimit = "100 req/min"
            },
            new ProviderChainHealth
            {
                Name = "Gemini",
                ChainOrder = 3,
                IsConfigured = HasApiKey(settings.GeminiEmbedding.ApiKey),
                Status = HasApiKey(settings.GeminiEmbedding.ApiKey) ? "Healthy" : "NotConfigured",
                IsAvailable = HasApiKey(settings.GeminiEmbedding.ApiKey),
                FreeTierLimit = "1,500 req/day"
            },
            new ProviderChainHealth
            {
                Name = "HuggingFace",
                ChainOrder = 4,
                IsConfigured = HasApiKey(settings.HuggingFaceEmbedding.ApiKey),
                Status = HasApiKey(settings.HuggingFaceEmbedding.ApiKey) ? "Healthy" : "NotConfigured",
                IsAvailable = HasApiKey(settings.HuggingFaceEmbedding.ApiKey),
                FreeTierLimit = "300 req/hr"
            },
            new ProviderChainHealth
            {
                Name = "Jina",
                ChainOrder = 5,
                IsConfigured = HasApiKey(settings.Jina.ApiKey),
                Status = HasApiKey(settings.Jina.ApiKey) ? "Healthy" : "NotConfigured",
                IsAvailable = HasApiKey(settings.Jina.ApiKey),
                FreeTierLimit = "1M tokens"
            },
            new ProviderChainHealth
            {
                Name = "Ollama (Local)",
                ChainOrder = 6,
                IsConfigured = true,
                Status = "Healthy",
                IsAvailable = true,
                FreeTierLimit = "Unlimited (local)"
            }
        ];
    }

    /// <summary>
    /// Builds OCR provider chain health: PdfPig → Tesseract → Azure DocIntel → Mistral → OCR Space → Gemini Vision.
    /// </summary>
    private static List<ProviderChainHealth> BuildOcrChainHealth(AgentSystemSettings settings)
    {
        return
        [
            new ProviderChainHealth
            {
                Name = "PdfPig (Local)",
                ChainOrder = 1,
                IsConfigured = true,
                Status = "Healthy",
                IsAvailable = true,
                FreeTierLimit = "Unlimited (local)"
            },
            new ProviderChainHealth
            {
                Name = "Tesseract (Local)",
                ChainOrder = 2,
                IsConfigured = true,
                Status = "Healthy",
                IsAvailable = true,
                FreeTierLimit = "Unlimited (local)"
            },
            new ProviderChainHealth
            {
                Name = "Azure Document Intelligence",
                ChainOrder = 3,
                IsConfigured = HasApiKey(settings.AzureDocumentIntelligence.ApiKey),
                Status = HasApiKey(settings.AzureDocumentIntelligence.ApiKey) ? "Healthy" : "NotConfigured",
                IsAvailable = HasApiKey(settings.AzureDocumentIntelligence.ApiKey),
                FreeTierLimit = "500 pages/month"
            },
            new ProviderChainHealth
            {
                Name = "Mistral OCR",
                ChainOrder = 4,
                IsConfigured = HasApiKey(settings.Mistral.ApiKey),
                Status = HasApiKey(settings.Mistral.ApiKey) ? "Healthy" : "NotConfigured",
                IsAvailable = HasApiKey(settings.Mistral.ApiKey),
                FreeTierLimit = "1B tokens/month"
            },
            new ProviderChainHealth
            {
                Name = "OCR Space",
                ChainOrder = 5,
                IsConfigured = HasApiKey(settings.OcrSpace.ApiKey),
                Status = HasApiKey(settings.OcrSpace.ApiKey) ? "Healthy" : "NotConfigured",
                IsAvailable = HasApiKey(settings.OcrSpace.ApiKey),
                FreeTierLimit = "500 req/day"
            },
            new ProviderChainHealth
            {
                Name = "Gemini Vision",
                ChainOrder = 6,
                IsConfigured = HasApiKey(settings.Gemini.ApiKey),
                Status = HasApiKey(settings.Gemini.ApiKey) ? "Healthy" : "NotConfigured",
                IsAvailable = HasApiKey(settings.Gemini.ApiKey),
                FreeTierLimit = "1,500 req/day"
            }
        ];
    }

    /// <summary>
    /// Builds NER provider chain health: HuggingFace BERT → Azure Language.
    /// </summary>
    private static List<ProviderChainHealth> BuildNerChainHealth(AgentSystemSettings settings)
    {
        return
        [
            new ProviderChainHealth
            {
                Name = "HuggingFace BERT",
                ChainOrder = 1,
                IsConfigured = HasApiKey(settings.HuggingFace.ApiKey),
                Status = HasApiKey(settings.HuggingFace.ApiKey) ? "Healthy" : "NotConfigured",
                IsAvailable = HasApiKey(settings.HuggingFace.ApiKey),
                FreeTierLimit = "300 req/hr"
            },
            new ProviderChainHealth
            {
                Name = "Azure AI Language",
                ChainOrder = 2,
                IsConfigured = HasApiKey(settings.AzureLanguage.ApiKey),
                Status = HasApiKey(settings.AzureLanguage.ApiKey) ? "Healthy" : "NotConfigured",
                IsAvailable = HasApiKey(settings.AzureLanguage.ApiKey),
                FreeTierLimit = "5K/month"
            }
        ];
    }

    /// <summary>
    /// Builds STT provider chain health: Deepgram → Azure Speech.
    /// </summary>
    private static List<ProviderChainHealth> BuildSttChainHealth(AgentSystemSettings settings)
    {
        return
        [
            new ProviderChainHealth
            {
                Name = "Deepgram",
                ChainOrder = 1,
                IsConfigured = HasApiKey(settings.Deepgram.ApiKey),
                Status = HasApiKey(settings.Deepgram.ApiKey) ? "Healthy" : "NotConfigured",
                IsAvailable = HasApiKey(settings.Deepgram.ApiKey),
                FreeTierLimit = "$200 credit"
            },
            new ProviderChainHealth
            {
                Name = "Azure AI Speech",
                ChainOrder = 2,
                IsConfigured = HasApiKey(settings.AzureSpeech.ApiKey),
                Status = HasApiKey(settings.AzureSpeech.ApiKey) ? "Healthy" : "NotConfigured",
                IsAvailable = HasApiKey(settings.AzureSpeech.ApiKey),
                FreeTierLimit = "5 hrs/month"
            }
        ];
    }

    /// <summary>
    /// Builds content safety service health: Azure AI Content Safety.
    /// </summary>
    private static List<ServiceHealth> BuildContentSafetyHealth(AgentSystemSettings settings)
    {
        var isConfigured = HasApiKey(settings.AzureContentSafety.ApiKey);
        return
        [
            new ServiceHealth
            {
                Name = "Azure AI Content Safety",
                IsConfigured = isConfigured,
                Status = isConfigured ? "Available" : "NotConfigured"
            }
        ];
    }

    /// <summary>
    /// Builds translation service health: Azure AI Translator.
    /// </summary>
    private static List<ServiceHealth> BuildTranslationHealth(AgentSystemSettings settings)
    {
        var isConfigured = HasApiKey(settings.AzureTranslator.ApiKey);
        return
        [
            new ServiceHealth
            {
                Name = "Azure AI Translator",
                IsConfigured = isConfigured,
                Status = isConfigured ? "Available" : "NotConfigured"
            }
        ];
    }

    /// <summary>
    /// Checks whether an API key is present and non-empty.
    /// </summary>
    private static bool HasApiKey(string apiKey) => !string.IsNullOrWhiteSpace(apiKey);
}
