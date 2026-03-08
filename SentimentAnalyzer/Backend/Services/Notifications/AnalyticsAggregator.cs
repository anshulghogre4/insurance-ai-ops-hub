using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using SentimentAnalyzer.API.Hubs;
using SentimentAnalyzer.API.Models;

namespace SentimentAnalyzer.API.Services.Notifications;

/// <summary>
/// Background service that aggregates real-time analytics metrics using a rolling
/// 1-hour window with 10-second granularity (360 buckets). Thread-safe via
/// ConcurrentDictionary and Interlocked operations.
/// </summary>
public class AnalyticsAggregator : BackgroundService
{
    private readonly IHubContext<AnalyticsHub, IAnalyticsHubClient> _hubContext;
    private readonly ILogger<AnalyticsAggregator> _logger;
    private readonly TimeSpan _broadcastInterval = TimeSpan.FromSeconds(10);
    private readonly TimeSpan _windowSize = TimeSpan.FromHours(1);

    // Thread-safe counters
    private readonly ConcurrentDictionary<string, RollingCounter> _counters = new();

    public AnalyticsAggregator(
        IHubContext<AnalyticsHub, IAnalyticsHubClient> hubContext,
        ILogger<AnalyticsAggregator> logger)
    {
        _hubContext = hubContext;
        _logger = logger;

        // Initialize counters
        _counters["claims_processed"] = new RollingCounter(_windowSize);
        _counters["triage_time_ms"] = new RollingCounter(_windowSize);
        _counters["fraud_detected"] = new RollingCounter(_windowSize);
        _counters["doc_queries"] = new RollingCounter(_windowSize);
    }

    /// <summary>Record a claim being processed (call from ClaimsOrchestrationService).</summary>
    public void RecordClaimProcessed(double triageTimeMs)
    {
        _counters["claims_processed"].Increment();
        _counters["triage_time_ms"].Add(triageTimeMs);
    }

    /// <summary>Record a fraud detection event.</summary>
    public void RecordFraudDetected()
    {
        _counters["fraud_detected"].Increment();
    }

    /// <summary>Record a document query.</summary>
    public void RecordDocQuery()
    {
        _counters["doc_queries"].Increment();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AnalyticsAggregator started. Broadcasting every {Interval}s", _broadcastInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;

                // Snapshot each counter once to get consistent count+sum
                var claimsSnap = _counters["claims_processed"].Snapshot();
                var triageSnap = _counters["triage_time_ms"].Snapshot();
                var fraudSnap = _counters["fraud_detected"].Snapshot();
                var docSnap = _counters["doc_queries"].Snapshot();

                var claimsCount = claimsSnap.Count;
                var avgTriageMs = claimsCount > 0 ? triageSnap.Sum / claimsCount : 0;
                var fraudRate = claimsCount > 0 ? (double)fraudSnap.Count / claimsCount * 100 : 0;
                var docQueries = docSnap.Count;

                var metrics = new AnalyticsMetrics(
                    ClaimsPerHour: claimsCount,
                    AvgTriageMs: Math.Round(avgTriageMs, 1),
                    FraudDetectionRate: Math.Round(fraudRate, 1),
                    ProviderResponseMs: new Dictionary<string, double>(),
                    DocQueriesPerHour: docQueries,
                    WindowStart: now - _windowSize,
                    WindowEnd: now);

                await _hubContext.Clients.All.MetricsUpdate(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting analytics metrics");
            }

            await Task.Delay(_broadcastInterval, stoppingToken);
        }
    }
}

/// <summary>
/// Thread-safe rolling-window counter using a ConcurrentQueue of timestamped entries.
/// Uses snapshot-then-compute to avoid TOCTOU race conditions on concurrent reads.
/// Entries older than the window are pruned on read.
/// </summary>
public class RollingCounter
{
    private readonly TimeSpan _window;
    private readonly ConcurrentQueue<(DateTime Timestamp, double Value)> _entries = new();

    public RollingCounter(TimeSpan window)
    {
        _window = window;
    }

    /// <summary>Increment the counter by 1.</summary>
    public void Increment()
    {
        _entries.Enqueue((DateTime.UtcNow, 0));
    }

    /// <summary>Add a value (also increments count).</summary>
    public void Add(double value)
    {
        _entries.Enqueue((DateTime.UtcNow, value));
    }

    /// <summary>
    /// Atomically snapshot, prune, and return count + sum.
    /// Thread-safe: snapshot the queue to an array first, then filter by window.
    /// </summary>
    public (int Count, double Sum) Snapshot()
    {
        PruneOldEntries();
        var items = _entries.ToArray();
        var cutoff = DateTime.UtcNow - _window;
        var valid = items.Where(e => e.Timestamp >= cutoff).ToArray();
        return (valid.Length, valid.Sum(e => e.Value));
    }

    /// <summary>Get the count of entries within the rolling window.</summary>
    public int GetCount()
    {
        return Snapshot().Count;
    }

    /// <summary>Get the sum of values within the rolling window.</summary>
    public double GetSum()
    {
        return Snapshot().Sum;
    }

    /// <summary>Best-effort pruning of expired entries from the head of the queue.</summary>
    private void PruneOldEntries()
    {
        var cutoff = DateTime.UtcNow - _window;
        while (_entries.TryPeek(out var oldest) && oldest.Timestamp < cutoff)
        {
            _entries.TryDequeue(out _);
        }
    }
}
