using System.Text.Json;
using Microsoft.Extensions.Logging;
using SentimentAnalyzer.Agents.Configuration;
using SentimentAnalyzer.Agents.Models;
using SentimentAnalyzer.Agents.Orchestration;
using SentimentAnalyzer.API.Data;
using SentimentAnalyzer.API.Data.Entities;
using SentimentAnalyzer.API.Models;
using SentimentAnalyzer.Domain.Enums;

namespace SentimentAnalyzer.API.Services.Fraud;

/// <summary>
/// Facade for the fraud analysis pipeline.
/// Runs FraudScoring profile agents, updates claim fraud scores, and manages alerts.
/// </summary>
public class FraudAnalysisService : IFraudAnalysisService
{
    private readonly IAnalysisOrchestrator _orchestrator;
    private readonly IClaimsRepository _claimsRepo;
    private readonly ILogger<FraudAnalysisService> _logger;

    public FraudAnalysisService(
        IAnalysisOrchestrator orchestrator,
        IClaimsRepository claimsRepo,
        ILogger<FraudAnalysisService> logger)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _claimsRepo = claimsRepo ?? throw new ArgumentNullException(nameof(claimsRepo));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<FraudAnalysisResponse> AnalyzeFraudAsync(int claimId)
    {
        _logger.LogInformation("Starting fraud analysis for claim {ClaimId}", claimId);

        var claim = await _claimsRepo.GetClaimByIdAsync(claimId);
        if (claim == null)
        {
            throw new KeyNotFoundException($"Claim {claimId} not found");
        }

        // Build evidence context from multimodal evidence (image descriptions, audio transcripts, OCR text)
        var analysisText = claim.ClaimText;
        if (claim.Evidence?.Count > 0)
        {
            var evidenceContext = string.Join("\n", claim.Evidence.Select(e =>
                $"[{e.EvidenceType.ToUpperInvariant()} EVIDENCE via {e.Provider}]: {e.ProcessedText}"));
            analysisText = $"{claim.ClaimText}\n\n--- ATTACHED EVIDENCE ---\n{evidenceContext}";
            _logger.LogInformation("Fraud analysis for claim {ClaimId} includes {Count} evidence items", claimId, claim.Evidence.Count);
        }

        // Run the FraudScoring orchestration profile
        var agentResult = await _orchestrator.AnalyzeAsync(
            analysisText, OrchestrationProfile.FraudScoring, InteractionType.Complaint);

        var fraud = agentResult.FraudAnalysis;

        // Only update claim in DB when the LLM analysis actually returned data.
        // When all providers fail, fraud is null — preserve existing triage scores.
        if (fraud != null)
        {
            claim.FraudScore = fraud.FraudProbabilityScore;
            claim.FraudRiskLevel = fraud.RiskLevel;
            var fraudJson = JsonSerializer.Serialize(fraud);
            claim.FraudAnalysisJson = fraudJson.Length > 10000 ? fraudJson[..10000] : fraudJson;
            claim.Status = claim.FraudScore >= 75 ? "UnderReview" : claim.Status;

            await _claimsRepo.UpdateClaimAsync(claim);

            if (claim.FraudScore >= 75)
            {
                _logger.LogWarning("SIU referral recommended for claim {ClaimId}: FraudScore={FraudScore}, Reason={Reason}",
                    claimId, claim.FraudScore, fraud.SiuReferralReason ?? "High fraud probability score");
            }

            _logger.LogInformation("Fraud analysis completed for claim {ClaimId}: Score={Score}, RiskLevel={Risk}",
                claimId, claim.FraudScore, claim.FraudRiskLevel);
        }
        else
        {
            _logger.LogWarning(
                "Fraud analysis for claim {ClaimId} returned no results (LLM providers failed). Preserving existing triage data: FraudScore={Score}, RiskLevel={Risk}",
                claimId, claim.FraudScore, claim.FraudRiskLevel);
        }

        return MapToFraudResponse(claimId, fraud, claim);
    }

    /// <inheritdoc />
    public async Task<FraudAnalysisResponse?> GetFraudScoreAsync(int claimId)
    {
        var claim = await _claimsRepo.GetClaimByIdAsync(claimId);
        if (claim == null) return null;

        // If we have stored fraud analysis JSON, deserialize it
        FraudAnalysisDetail? fraud = null;
        if (claim.FraudAnalysisJson != "{}")
        {
            try
            {
                fraud = JsonSerializer.Deserialize<FraudAnalysisDetail>(claim.FraudAnalysisJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException)
            {
                // FraudAnalysisJson may have unexpected format
            }
        }

        return new FraudAnalysisResponse
        {
            ClaimId = claimId,
            FraudScore = claim.FraudScore,
            RiskLevel = claim.FraudRiskLevel,
            ReferToSIU = claim.FraudScore >= 75,
            SiuReferralReason = fraud?.SiuReferralReason ?? "",
            Confidence = fraud?.ConfidenceInAssessment ?? 0,
            Indicators = fraud?.Indicators?.Select(i => new FraudIndicatorResponse
            {
                Category = i.Category,
                Description = i.Description,
                Severity = i.Severity
            }).ToList() ?? [],
            RecommendedActions = fraud?.RecommendedActions?.Select(a => new ClaimActionResponse
            {
                Action = a.Action,
                Priority = a.Priority,
                Reasoning = a.Reasoning
            }).ToList() ?? []
        };
    }

    /// <inheritdoc />
    public async Task<List<ClaimTriageResponse>> GetFraudAlertsAsync(double minFraudScore = 55, int pageSize = 50)
    {
        var claims = await _claimsRepo.GetFraudAlertsAsync(minFraudScore, pageSize);

        return claims.Select(c =>
        {
            // Deserialize fraud flags from JSON
            var fraudFlags = new List<string>();
            if (c.FraudFlagsJson != "[]")
            {
                try
                {
                    fraudFlags = JsonSerializer.Deserialize<List<string>>(c.FraudFlagsJson) ?? [];
                }
                catch (JsonException) { /* Non-critical, use empty list */ }
            }

            // Extract estimated loss range from triage JSON
            var estimatedLoss = "";
            if (c.TriageJson != "{}")
            {
                try
                {
                    var triage = JsonSerializer.Deserialize<Agents.Models.ClaimTriageDetail>(c.TriageJson,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    estimatedLoss = triage?.EstimatedLossRange ?? "";
                }
                catch (JsonException) { /* Non-critical */ }
            }

            return new ClaimTriageResponse
            {
                ClaimId = c.Id,
                Severity = c.Severity,
                Urgency = c.Urgency,
                ClaimType = c.ClaimType,
                FraudScore = c.FraudScore,
                FraudRiskLevel = c.FraudRiskLevel,
                EstimatedLossRange = estimatedLoss,
                Status = c.Status,
                CreatedAt = c.CreatedAt,
                FraudFlags = fraudFlags,
                RecommendedActions = [],
                Evidence = []
            };
        }).ToList();
    }

    /// <summary>
    /// Maps a FraudAnalysisDetail agent output to a FraudAnalysisResponse.
    /// Falls back to existing claim data when LLM analysis failed (fraud is null).
    /// </summary>
    private static FraudAnalysisResponse MapToFraudResponse(int claimId, FraudAnalysisDetail? fraud, ClaimRecord claim)
    {
        return new FraudAnalysisResponse
        {
            ClaimId = claimId,
            FraudScore = fraud?.FraudProbabilityScore ?? claim.FraudScore,
            RiskLevel = fraud?.RiskLevel ?? claim.FraudRiskLevel,
            ReferToSIU = fraud?.ReferToSIU ?? (claim.FraudScore >= 75),
            SiuReferralReason = fraud?.SiuReferralReason ?? "",
            Confidence = fraud?.ConfidenceInAssessment ?? 0,
            Indicators = fraud?.Indicators?.Select(i => new FraudIndicatorResponse
            {
                Category = i.Category,
                Description = i.Description,
                Severity = i.Severity
            }).ToList() ?? [],
            RecommendedActions = fraud?.RecommendedActions?.Select(a => new ClaimActionResponse
            {
                Action = a.Action,
                Priority = a.Priority,
                Reasoning = a.Reasoning
            }).ToList() ?? []
        };
    }
}
