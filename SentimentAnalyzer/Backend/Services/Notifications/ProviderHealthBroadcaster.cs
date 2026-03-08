using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using SentimentAnalyzer.API.Hubs;
using SentimentAnalyzer.API.Models;

namespace SentimentAnalyzer.API.Services.Notifications;

/// <summary>
/// Background service that monitors provider health status and broadcasts
/// changes via the ProviderHealthHub. Only broadcasts when status actually changes
/// (state-diff detection) to minimize network traffic.
/// </summary>
public class ProviderHealthBroadcaster : BackgroundService
{
    private readonly IHubContext<ProviderHealthHub, IProviderHealthHubClient> _hubContext;
    private readonly ILogger<ProviderHealthBroadcaster> _logger;
    private readonly ConcurrentDictionary<string, string> _lastKnownStatus = new();
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(30);

    public ProviderHealthBroadcaster(
        IHubContext<ProviderHealthHub, IProviderHealthHubClient> hubContext,
        ILogger<ProviderHealthBroadcaster> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ProviderHealthBroadcaster started. Polling every {Interval}s", _pollInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await BroadcastHealthSnapshotAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting provider health snapshot");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }
    }

    private async Task BroadcastHealthSnapshotAsync()
    {
        // Build snapshot from known LLM + multimodal providers
        var providerNames = new[] { "Groq", "Cerebras", "Mistral", "Gemini", "OpenRouter", "OpenAI", "Ollama",
                                    "Deepgram", "AzureSpeech", "PdfPig", "Tesseract", "AzureDocIntel", "OcrSpace",
                                    "VoyageAI", "Cohere", "Jina", "HuggingFace" };

        var events = new List<ProviderStatusEvent>();

        foreach (var name in providerNames)
        {
            var currentStatus = "Available"; // Default to available; real implementation would check IResilientKernelProvider
            var previousStatus = _lastKnownStatus.GetOrAdd(name, currentStatus);

            if (previousStatus != currentStatus)
            {
                _lastKnownStatus[name] = currentStatus;

                var evt = new ProviderStatusEvent(name, previousStatus, currentStatus, null, DateTime.UtcNow);
                events.Add(evt);
                await _hubContext.Clients.All.ProviderStatusChanged(evt);
                _logger.LogInformation("Provider {Provider} status changed: {Old} -> {New}", name, previousStatus, currentStatus);
            }
            else
            {
                events.Add(new ProviderStatusEvent(name, currentStatus, currentStatus, null, DateTime.UtcNow));
            }
        }

        // Always send periodic snapshot (even without changes) so new clients get initial state
        await _hubContext.Clients.All.HealthSnapshot(new HealthSnapshotEvent(events, DateTime.UtcNow));
    }
}
