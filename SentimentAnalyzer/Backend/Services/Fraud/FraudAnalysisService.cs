using System.Text.Json;
using Microsoft.Extensions.Logging;
using SentimentAnalyzer.Agents.Configuration;
using SentimentAnalyzer.Agents.Models;
using SentimentAnalyzer.Agents.Orchestration;
using SentimentAnalyzer.API.Data;
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

        // Run the FraudScoring orchestration profile
        var agentResult = await _orchestrator.AnalyzeAsync(
            claim.ClaimText, OrchestrationProfile.FraudScoring, InteractionType.Complaint);

        var fraud = agentResult.FraudAnalysis;

        // Update the claim with fraud analysis results
        claim.FraudScore = fraud?.FraudProbabilityScore ?? 0;
        claim.FraudRiskLevel = fraud?.RiskLevel ?? "VeryLow";
        var fraudJson = fraud != null ? JsonSerializer.Serialize(fraud) : "{}";
        claim.FraudAnalysisJson = fraudJson.Length > 10000 ? fraudJson[..10000] : fraudJson;
        claim.Status = claim.FraudScore >= 75 ? "UnderReview" : claim.Status;

        await _claimsRepo.UpdateClaimAsync(claim);

        if (claim.FraudScore >= 75)
        {
            _logger.LogWarning("SIU referral recommended for claim {ClaimId}: FraudScore={FraudScore}, Reason={Reason}",
                claimId, claim.FraudScore, fraud?.SiuReferralReason ?? "High fraud probability score");
        }

        _logger.LogInformation("Fraud analysis completed for claim {ClaimId}: Score={Score}, RiskLevel={Risk}",
            claimId, claim.FraudScore, claim.FraudRiskLevel);

        return MapToFraudResponse(claimId, fraud);
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
    public async Task<List<FraudAnalysisResponse>> GetFraudAlertsAsync(double minFraudScore = 55, int pageSize = 50)
    {
        var claims = await _claimsRepo.GetFraudAlertsAsync(minFraudScore, pageSize);

        return claims.Select(c => new FraudAnalysisResponse
        {
            ClaimId = c.Id,
            FraudScore = c.FraudScore,
            RiskLevel = c.FraudRiskLevel,
            ReferToSIU = c.FraudScore >= 75,
            Confidence = 0
        }).ToList();
    }

    /// <summary>
    /// Maps a FraudAnalysisDetail agent output to a FraudAnalysisResponse.
    /// </summary>
    private static FraudAnalysisResponse MapToFraudResponse(int claimId, FraudAnalysisDetail? fraud)
    {
        return new FraudAnalysisResponse
        {
            ClaimId = claimId,
            FraudScore = fraud?.FraudProbabilityScore ?? 0,
            RiskLevel = fraud?.RiskLevel ?? "VeryLow",
            ReferToSIU = fraud?.ReferToSIU ?? false,
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
