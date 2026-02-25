using MediatR;
using Microsoft.Extensions.Configuration;
using SentimentAnalyzer.Agents.Orchestration;
using SentimentAnalyzer.API.Models;

namespace SentimentAnalyzer.API.Features.Health.Queries;

/// <summary>
/// Query to retrieve health status of all AI providers and multimodal services.
/// </summary>
public record GetProviderHealthQuery : IRequest<ProviderHealthResponse>;

/// <summary>
/// Handler that polls provider health from the resilient kernel provider
/// and reports multimodal service configuration status.
/// </summary>
public class GetProviderHealthHandler : IRequestHandler<GetProviderHealthQuery, ProviderHealthResponse>
{
    private readonly IResilientKernelProvider _kernelProvider;
    private readonly IConfiguration _config;

    public GetProviderHealthHandler(IResilientKernelProvider kernelProvider, IConfiguration config)
    {
        _kernelProvider = kernelProvider ?? throw new ArgumentNullException(nameof(kernelProvider));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public Task<ProviderHealthResponse> Handle(GetProviderHealthQuery query, CancellationToken cancellationToken)
    {
        var healthStatus = _kernelProvider.GetHealthStatus();

        var response = new ProviderHealthResponse
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
            MultimodalServices = GetMultimodalServiceHealth(),
            CheckedAt = DateTime.UtcNow
        };

        return Task.FromResult(response);
    }

    /// <summary>
    /// Checks configuration to determine which multimodal services are available.
    /// </summary>
    private List<ServiceHealth> GetMultimodalServiceHealth()
    {
        return
        [
            new ServiceHealth
            {
                Name = "Deepgram (Speech-to-Text)",
                IsConfigured = !string.IsNullOrWhiteSpace(_config["AgentSystem:Deepgram:ApiKey"]),
                Status = !string.IsNullOrWhiteSpace(_config["AgentSystem:Deepgram:ApiKey"]) ? "Available" : "NotConfigured"
            },
            new ServiceHealth
            {
                Name = "AzureVision (Image Analysis)",
                IsConfigured = !string.IsNullOrWhiteSpace(_config["AgentSystem:AzureVision:ApiKey"]),
                Status = !string.IsNullOrWhiteSpace(_config["AgentSystem:AzureVision:ApiKey"]) ? "Available" : "NotConfigured"
            },
            new ServiceHealth
            {
                Name = "CloudflareVision (Image Fallback)",
                IsConfigured = !string.IsNullOrWhiteSpace(_config["AgentSystem:Cloudflare:ApiKey"]),
                Status = !string.IsNullOrWhiteSpace(_config["AgentSystem:Cloudflare:ApiKey"]) ? "Available" : "NotConfigured"
            },
            new ServiceHealth
            {
                Name = "OcrSpace (Document OCR)",
                IsConfigured = !string.IsNullOrWhiteSpace(_config["AgentSystem:OcrSpace:ApiKey"]),
                Status = !string.IsNullOrWhiteSpace(_config["AgentSystem:OcrSpace:ApiKey"]) ? "Available" : "NotConfigured"
            },
            new ServiceHealth
            {
                Name = "HuggingFace (Entity Extraction)",
                IsConfigured = !string.IsNullOrWhiteSpace(_config["AgentSystem:HuggingFace:ApiKey"]),
                Status = !string.IsNullOrWhiteSpace(_config["AgentSystem:HuggingFace:ApiKey"]) ? "Available" : "NotConfigured"
            },
            new ServiceHealth
            {
                Name = "Voyage AI (Finance Embeddings)",
                IsConfigured = !string.IsNullOrWhiteSpace(_config["AgentSystem:Voyage:ApiKey"]),
                Status = !string.IsNullOrWhiteSpace(_config["AgentSystem:Voyage:ApiKey"]) ? "Available" : "NotConfigured"
            }
        ];
    }
}
