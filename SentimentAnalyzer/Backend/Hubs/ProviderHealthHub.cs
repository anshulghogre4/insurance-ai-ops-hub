using Microsoft.AspNetCore.SignalR;
using SentimentAnalyzer.API.Models;

namespace SentimentAnalyzer.API.Hubs;

/// <summary>Strongly-typed client contract for provider health hub.</summary>
public interface IProviderHealthHubClient
{
    Task ProviderStatusChanged(ProviderStatusEvent evt);
    Task HealthSnapshot(HealthSnapshotEvent evt);
}

/// <summary>
/// SignalR hub for real-time provider health status updates.
/// Connected clients receive status change events and periodic snapshots.
/// </summary>
public class ProviderHealthHub : Hub<IProviderHealthHubClient>
{
    private readonly ILogger<ProviderHealthHub> _logger;

    public ProviderHealthHub(ILogger<ProviderHealthHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Provider health hub client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Provider health hub client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
