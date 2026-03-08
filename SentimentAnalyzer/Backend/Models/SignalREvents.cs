namespace SentimentAnalyzer.API.Models;

/// <summary>Event broadcast when a claim is triaged by the agent pipeline.</summary>
public record ClaimTriagedEvent(int ClaimId, string Severity, string PersonaType, string ClaimType, double FraudScore, DateTime TriagedAt);

/// <summary>Event broadcast when a claim's status changes.</summary>
public record ClaimStatusEvent(int ClaimId, string OldStatus, string NewStatus, DateTime ChangedAt);

/// <summary>Event broadcast when fraud score exceeds alert threshold.</summary>
public record FraudAlertEvent(int ClaimId, double FraudScore, List<string> Flags, string RiskLevel, DateTime DetectedAt);

/// <summary>Event broadcast when a provider's health status changes.</summary>
public record ProviderStatusEvent(string ProviderName, string OldStatus, string NewStatus, int? CooldownSeconds, DateTime ChangedAt);

/// <summary>Periodic health snapshot of all providers.</summary>
public record HealthSnapshotEvent(List<ProviderStatusEvent> Providers, DateTime CheckedAt);

/// <summary>Rolling-window analytics metrics broadcast periodically.</summary>
public record AnalyticsMetrics(
    int ClaimsPerHour,
    double AvgTriageMs,
    double FraudDetectionRate,
    Dictionary<string, double> ProviderResponseMs,
    int DocQueriesPerHour,
    DateTime WindowStart,
    DateTime WindowEnd);
