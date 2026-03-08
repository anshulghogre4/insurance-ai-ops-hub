using Microsoft.AspNetCore.SignalR;
using SentimentAnalyzer.API.Models;

namespace SentimentAnalyzer.API.Hubs;

/// <summary>Strongly-typed client contract for analytics hub.</summary>
public interface IAnalyticsHubClient
{
    Task MetricsUpdate(AnalyticsMetrics metrics);
}

/// <summary>
/// SignalR hub for real-time analytics metrics.
/// Broadcasts rolling-window aggregated metrics to connected dashboard clients.
/// </summary>
public class AnalyticsHub : Hub<IAnalyticsHubClient>
{
    private readonly ILogger<AnalyticsHub> _logger;

    public AnalyticsHub(ILogger<AnalyticsHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Analytics hub client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Analytics hub client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
