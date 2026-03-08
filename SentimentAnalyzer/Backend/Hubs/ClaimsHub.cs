using Microsoft.AspNetCore.SignalR;
using SentimentAnalyzer.API.Models;

namespace SentimentAnalyzer.API.Hubs;

/// <summary>Strongly-typed client contract for the claims hub.</summary>
public interface IClaimsHubClient
{
    Task ClaimTriaged(ClaimTriagedEvent evt);
    Task ClaimStatusChanged(ClaimStatusEvent evt);
    Task FraudAlertRaised(FraudAlertEvent evt);
}

/// <summary>
/// SignalR hub for real-time claim events.
/// Supports severity-based group subscriptions for targeted broadcasting.
/// </summary>
public class ClaimsHub : Hub<IClaimsHubClient>
{
    private readonly ILogger<ClaimsHub> _logger;

    public ClaimsHub(ILogger<ClaimsHub> logger)
    {
        _logger = logger;
    }

    /// <summary>Subscribe to a severity group (Critical, High, Medium, Low).</summary>
    public async Task JoinSeverityGroup(string severity)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"severity-{severity}");
        _logger.LogInformation("Client {ConnectionId} joined severity group: {Severity}", Context.ConnectionId, severity);
    }

    /// <summary>Unsubscribe from a severity group.</summary>
    public async Task LeaveSeverityGroup(string severity)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"severity-{severity}");
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Claims hub client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Claims hub client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
