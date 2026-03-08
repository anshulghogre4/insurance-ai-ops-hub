using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using SentimentAnalyzer.Agents.Configuration;
using SentimentAnalyzer.Agents.Models;
using SentimentAnalyzer.Agents.Orchestration;
using SentimentAnalyzer.API.Data;
using SentimentAnalyzer.API.Data.Entities;
using SentimentAnalyzer.API.Hubs;
using SentimentAnalyzer.API.Models;
using SentimentAnalyzer.API.Services.Notifications;
using SentimentAnalyzer.Domain.Enums;

namespace SentimentAnalyzer.API.Services.Claims;

/// <summary>
/// Facade for the claims triage pipeline.
/// Orchestrates claim analysis: text → agent pipeline → DB persistence → response.
/// </summary>
public class ClaimsOrchestrationService : IClaimsOrchestrationService
{
    private readonly IAnalysisOrchestrator _orchestrator;
    private readonly IClaimsRepository _claimsRepo;
    private readonly IPIIRedactor _piiRedactor;
    private readonly ILogger<ClaimsOrchestrationService> _logger;
    private readonly IHubContext<ClaimsHub, IClaimsHubClient>? _claimsHubContext;
    private readonly AnalyticsAggregator? _analyticsAggregator;

    public ClaimsOrchestrationService(
        IAnalysisOrchestrator orchestrator,
        IClaimsRepository claimsRepo,
        IPIIRedactor piiRedactor,
        ILogger<ClaimsOrchestrationService> logger,
        IHubContext<ClaimsHub, IClaimsHubClient>? claimsHubContext = null,
        AnalyticsAggregator? analyticsAggregator = null)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _claimsRepo = claimsRepo ?? throw new ArgumentNullException(nameof(claimsRepo));
        _piiRedactor = piiRedactor ?? throw new ArgumentNullException(nameof(piiRedactor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _claimsHubContext = claimsHubContext;
        _analyticsAggregator = analyticsAggregator;
    }

    /// <inheritdoc />
    public async Task<ClaimTriageResponse> TriageClaimAsync(string claimText, InteractionType interactionType = InteractionType.Complaint)
    {
        _logger.LogInformation("Starting claims triage for interaction type: {InteractionType}", interactionType);

        // Run the ClaimsTriage orchestration profile
        var agentResult = await _orchestrator.AnalyzeAsync(claimText, OrchestrationProfile.ClaimsTriage, interactionType);

        // Extract triage and fraud details from agent result
        var triage = agentResult.ClaimTriage;
        var fraud = agentResult.FraudAnalysis;

        // PII-redact claim text before persisting to database
        var truncatedText = claimText.Length > 5000 ? claimText[..5000] : claimText;
        var redactedText = _piiRedactor.Redact(truncatedText);

        // Build the claim record for persistence
        var claimRecord = new ClaimRecord
        {
            ClaimText = redactedText,
            Severity = triage?.Severity ?? "Medium",
            Urgency = triage?.Urgency ?? "Standard",
            ClaimType = triage?.ClaimType ?? "",
            FraudScore = fraud?.FraudProbabilityScore ?? 0,
            FraudRiskLevel = fraud?.RiskLevel ?? triage?.PreliminaryFraudRisk ?? "VeryLow",
            Status = "Triaged",
            TriageJson = TruncateString(triage != null ? JsonSerializer.Serialize(triage) : "{}", 10000),
            FraudAnalysisJson = TruncateString(fraud != null ? JsonSerializer.Serialize(fraud) : "{}", 10000),
            FraudFlagsJson = triage?.FraudFlags?.Count > 0
                ? TruncateJsonList(triage.FraudFlags, 2000)
                : "[]"
        };

        // Save the claim
        var saved = await _claimsRepo.SaveClaimAsync(claimRecord);

        // Save recommended actions if present
        var actions = BuildActionRecords(saved.Id, triage, fraud);
        if (actions.Count > 0)
        {
            await _claimsRepo.SaveActionsAsync(actions);
            saved.Actions = actions;
        }

        _logger.LogInformation("Claims triage completed. ClaimId={ClaimId}, Severity={Severity}, FraudScore={FraudScore}",
            saved.Id, saved.Severity, saved.FraudScore);

        // Sprint 7: Broadcast real-time events via SignalR
        if (_claimsHubContext != null)
        {
            var triageEvent = new ClaimTriagedEvent(
                saved.Id, saved.Severity, "Policyholder", saved.ClaimType,
                saved.FraudScore, DateTime.UtcNow);
            await _claimsHubContext.Clients.All.ClaimTriaged(triageEvent);

            // Broadcast to severity group
            await _claimsHubContext.Clients.Group($"severity-{saved.Severity}").ClaimTriaged(triageEvent);

            // Fraud alert if score is high
            if (saved.FraudScore >= 55)
            {
                var fraudFlags = new List<string>();
                try { fraudFlags = System.Text.Json.JsonSerializer.Deserialize<List<string>>(saved.FraudFlagsJson) ?? []; }
                catch (System.Text.Json.JsonException) { }

                await _claimsHubContext.Clients.All.FraudAlertRaised(new FraudAlertEvent(
                    saved.Id, saved.FraudScore, fraudFlags, saved.FraudRiskLevel, DateTime.UtcNow));
            }
        }

        // Sprint 7: Record analytics
        _analyticsAggregator?.RecordClaimProcessed(0); // TODO: measure actual triage time

        return MapToTriageResponse(saved, triage?.EstimatedLossRange ?? "");
    }

    /// <inheritdoc />
    public async Task<ClaimTriageResponse?> GetClaimAsync(int claimId)
    {
        var claim = await _claimsRepo.GetClaimByIdAsync(claimId);
        if (claim == null) return null;

        var estimatedLoss = "";
        if (claim.TriageJson != "{}")
        {
            try
            {
                var triage = JsonSerializer.Deserialize<ClaimTriageDetail>(claim.TriageJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                estimatedLoss = triage?.EstimatedLossRange ?? "";
            }
            catch (JsonException)
            {
                // TriageJson may have unexpected format; continue with empty estimate
            }
        }

        return MapToTriageResponse(claim, estimatedLoss);
    }

    /// <inheritdoc />
    public async Task<PaginatedResponse<ClaimTriageResponse>> GetClaimsHistoryAsync(
        string? severity = null,
        string? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int pageSize = 20,
        int page = 1)
    {
        var (claims, totalCount) = await _claimsRepo.GetClaimsAsync(severity, status, fromDate, toDate, pageSize, page);
        return new PaginatedResponse<ClaimTriageResponse>
        {
            Items = claims.Select(c => MapToTriageResponse(c, "")).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// Builds ClaimActionRecord list from triage and fraud recommended actions.
    /// </summary>
    private static List<ClaimActionRecord> BuildActionRecords(int claimId, ClaimTriageDetail? triage, FraudAnalysisDetail? fraud)
    {
        var actions = new List<ClaimActionRecord>();

        if (triage?.RecommendedActions != null)
        {
            foreach (var a in triage.RecommendedActions)
            {
                actions.Add(new ClaimActionRecord
                {
                    ClaimId = claimId,
                    Action = TruncateString(a.Action, 500),
                    Priority = a.Priority,
                    Reasoning = TruncateString(a.Reasoning, 1000)
                });
            }
        }

        if (fraud?.RecommendedActions != null)
        {
            foreach (var a in fraud.RecommendedActions)
            {
                // Avoid duplicates from both agents
                if (!actions.Any(existing => existing.Action == a.Action))
                {
                    actions.Add(new ClaimActionRecord
                    {
                        ClaimId = claimId,
                        Action = TruncateString(a.Action, 500),
                        Priority = a.Priority,
                        Reasoning = TruncateString(a.Reasoning, 1000)
                    });
                }
            }
        }

        return actions;
    }

    /// <summary>
    /// Maps a ClaimRecord entity to a ClaimTriageResponse API model.
    /// </summary>
    private static ClaimTriageResponse MapToTriageResponse(ClaimRecord claim, string estimatedLossRange)
    {
        var fraudFlags = new List<string>();
        if (claim.FraudFlagsJson != "[]")
        {
            try
            {
                fraudFlags = JsonSerializer.Deserialize<List<string>>(claim.FraudFlagsJson) ?? [];
            }
            catch (JsonException) { /* Non-critical, use empty list */ }
        }

        return new ClaimTriageResponse
        {
            ClaimId = claim.Id,
            Severity = claim.Severity,
            Urgency = claim.Urgency,
            ClaimType = claim.ClaimType,
            FraudScore = claim.FraudScore,
            FraudRiskLevel = claim.FraudRiskLevel,
            EstimatedLossRange = estimatedLossRange,
            Status = claim.Status,
            CreatedAt = claim.CreatedAt,
            FraudFlags = fraudFlags,
            RecommendedActions = claim.Actions.Select(a => new ClaimActionResponse
            {
                Action = a.Action,
                Priority = a.Priority,
                Reasoning = a.Reasoning
            }).ToList(),
            Evidence = claim.Evidence.Select(e => new ClaimEvidenceResponse
            {
                EvidenceType = e.EvidenceType,
                Provider = e.Provider,
                ProcessedText = e.ProcessedText,
                DamageIndicators = DeserializeStringList(e.DamageIndicatorsJson),
                CreatedAt = e.CreatedAt
            }).ToList()
        };
    }

    private static string TruncateString(string value, int maxLength)
        => value.Length > maxLength ? value[..maxLength] : value;

    /// <summary>
    /// Serializes a string list to JSON, removing trailing items if the result exceeds maxLength.
    /// Always produces valid JSON (unlike raw string truncation).
    /// </summary>
    private static string TruncateJsonList(List<string> items, int maxLength)
    {
        var json = JsonSerializer.Serialize(items);
        if (json.Length <= maxLength) return json;

        // Remove items from the end until it fits
        var trimmed = new List<string>(items);
        while (trimmed.Count > 0)
        {
            trimmed.RemoveAt(trimmed.Count - 1);
            json = JsonSerializer.Serialize(trimmed);
            if (json.Length <= maxLength) return json;
        }

        return "[]";
    }

    /// <summary>
    /// Safely deserializes a JSON string list, returning empty on failure.
    /// </summary>
    private static List<string> DeserializeStringList(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
