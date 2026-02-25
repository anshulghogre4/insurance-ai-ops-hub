using System.Text.Json;
using MediatR;
using SentimentAnalyzer.Agents.Models;
using SentimentAnalyzer.Agents.Orchestration;
using SentimentAnalyzer.API.Data;
using SentimentAnalyzer.API.Data.Entities;
using SentimentAnalyzer.API.Services.Multimodal;
using SentimentAnalyzer.Domain.Enums;
using ApiModels = SentimentAnalyzer.API.Models;

namespace SentimentAnalyzer.API.Features.Insurance.Commands;

public record AnalyzeInsuranceCommand(
    string Text,
    string InteractionType = "General",
    string? CustomerId = null) : IRequest<ApiModels.InsuranceAnalysisResponse>;

public class AnalyzeInsuranceHandler : IRequestHandler<AnalyzeInsuranceCommand, ApiModels.InsuranceAnalysisResponse>
{
    private readonly IAnalysisOrchestrator _orchestrator;
    private readonly IAnalysisRepository _repository;
    private readonly ILogger<AnalyzeInsuranceHandler> _logger;
    private readonly IFinancialSentimentPreScreener? _preScreener;
    private readonly IPIIRedactor? _piiRedactor;

    public AnalyzeInsuranceHandler(
        IAnalysisOrchestrator orchestrator,
        IAnalysisRepository repository,
        ILogger<AnalyzeInsuranceHandler> logger,
        IFinancialSentimentPreScreener? preScreener = null,
        IPIIRedactor? piiRedactor = null)
    {
        _orchestrator = orchestrator;
        _repository = repository;
        _logger = logger;
        _preScreener = preScreener;
        _piiRedactor = piiRedactor;
    }

    public async Task<ApiModels.InsuranceAnalysisResponse> Handle(AnalyzeInsuranceCommand command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting insurance analysis for interaction type: {Type}", command.InteractionType);

        if (!Enum.TryParse<InteractionType>(command.InteractionType, true, out var interactionType))
        {
            interactionType = InteractionType.General;
        }

        // --- FinBERT Pre-Screening ---
        var preScreenResult = await TryPreScreenAsync(command.Text, cancellationToken);
        if (preScreenResult != null)
        {
            _logger.LogInformation(
                "Pre-screening short-circuit. Sentiment: {Sentiment}, Score: {Score:F3}, " +
                "Elapsed: {Ms}ms. Skipping full orchestration.",
                preScreenResult.Sentiment, preScreenResult.TopScore,
                preScreenResult.ElapsedMilliseconds);

            var fastResponse = BuildPreScreenResponse(preScreenResult, command.InteractionType);

            try { await PersistAnalysisAsync(command, fastResponse); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to persist pre-screened analysis."); }

            return fastResponse;
        }

        // --- Full Multi-Agent Orchestration ---
        _logger.LogInformation("Pre-screening did not short-circuit. Proceeding to full orchestration.");
        var agentResult = await _orchestrator.AnalyzeAsync(command.Text, interactionType);
        var response = MapToResponse(agentResult);

        // Persist to database (don't block the response on failure)
        try
        {
            await PersistAnalysisAsync(command, response);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist analysis to database. Response still returned.");
        }

        _logger.LogInformation("Insurance analysis completed. Sentiment: {Sentiment}, Purchase Intent: {Intent}",
            response.Sentiment, response.InsuranceAnalysis.PurchaseIntentScore);

        // Complaint escalation monitoring — structured warning for alerting systems
        if (string.Equals(response.InsuranceAnalysis.RiskIndicators.ComplaintEscalationRisk, "High", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(response.InsuranceAnalysis.RiskIndicators.ComplaintEscalationRisk, "Critical", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "COMPLAINT_ESCALATION: Risk={Risk}, Sentiment={Sentiment}, ChurnRisk={Churn}, " +
                "InteractionType={Interaction}, CustomerId={Customer}",
                response.InsuranceAnalysis.RiskIndicators.ComplaintEscalationRisk,
                response.Sentiment,
                response.InsuranceAnalysis.RiskIndicators.ChurnRisk,
                command.InteractionType,
                command.CustomerId ?? "anonymous");
        }

        return response;
    }

    /// <summary>
    /// Attempts FinBERT pre-screening. Returns the result ONLY if it is successful
    /// AND high-confidence. Returns null on any failure or low-confidence result,
    /// causing the caller to fall through to full orchestration.
    /// </summary>
    private async Task<FinancialSentimentResult?> TryPreScreenAsync(
        string text, CancellationToken cancellationToken)
    {
        if (_preScreener == null)
        {
            _logger.LogDebug("FinBERT pre-screener not registered. Skipping pre-screening.");
            return null;
        }

        try
        {
            // PII redaction BEFORE sending to external FinBERT API
            var redactedText = _piiRedactor?.Redact(text) ?? text;

            var result = await _preScreener.PreScreenAsync(redactedText, cancellationToken);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("FinBERT pre-screening failed: {Error}. Falling through to orchestration.",
                    result.ErrorMessage);
                return null;
            }

            if (!result.IsHighConfidence)
            {
                _logger.LogInformation(
                    "FinBERT pre-screening returned low confidence ({Score:F3}). " +
                    "Falling through to full orchestration.",
                    result.TopScore);
                return null;
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FinBERT pre-screening threw exception. Falling through to orchestration.");
            return null;
        }
    }

    /// <summary>
    /// Builds a fast InsuranceAnalysisResponse from the FinBERT pre-screening result.
    /// Provides sentiment + confidence without the full multi-agent insurance details.
    /// </summary>
    private static ApiModels.InsuranceAnalysisResponse BuildPreScreenResponse(
        FinancialSentimentResult preScreen, string interactionType)
    {
        // Map FinBERT label to the project's PascalCase convention
        var sentiment = char.ToUpperInvariant(preScreen.Sentiment[0]) + preScreen.Sentiment[1..];

        return new ApiModels.InsuranceAnalysisResponse
        {
            Sentiment = sentiment,
            ConfidenceScore = Math.Clamp(preScreen.TopScore, 0.0, 1.0),
            Explanation = $"High-confidence sentiment detected by FinBERT pre-screening " +
                          $"({preScreen.TopScore:F3} confidence). Full multi-agent analysis was skipped.",
            EmotionBreakdown = preScreen.Scores.ToDictionary(
                kvp => kvp.Key,
                kvp => Math.Round(kvp.Value, 4)),
            InsuranceAnalysis = new ApiModels.InsuranceAnalysisDetail
            {
                PurchaseIntentScore = sentiment == "Positive" ? 70 : sentiment == "Negative" ? 20 : 50,
                CustomerPersona = "PreScreened",
                JourneyStage = "Unknown",
                InteractionType = interactionType,
                KeyTopics = []
            },
            Quality = new ApiModels.QualityDetail
            {
                IsValid = true,
                QualityScore = 60,
                Issues = [],
                Suggestions = ["Pre-screened result. Submit with lower confidence threshold for full analysis."],
                Warnings = ["Pre-screened result via FinBERT. Limited insurance-specific insights."]
            }
        };
    }

    private async Task PersistAnalysisAsync(AnalyzeInsuranceCommand command, ApiModels.InsuranceAnalysisResponse response)
    {
        // Truncate first, then PII-redact before persisting (P0 security fix)
        var truncatedText = command.Text.Length > 10000 ? command.Text[..10000] : command.Text;
        var safeText = _piiRedactor?.Redact(truncatedText) ?? truncatedText;

        // Defense-in-depth: PII-redact explanation too (agents may echo original text)
        var truncatedExplanation = response.Explanation.Length > 5000 ? response.Explanation[..5000] : response.Explanation;
        var safeExplanation = _piiRedactor?.Redact(truncatedExplanation) ?? truncatedExplanation;

        // Truncate serialized JSON to fit MaxLength columns
        var recommendationsJson = TruncateJson(
            JsonSerializer.Serialize(response.InsuranceAnalysis.PolicyRecommendations), 5000);

        var record = new AnalysisRecord
        {
            InputText = safeText,
            InteractionType = command.InteractionType,
            CustomerId = command.CustomerId,
            Sentiment = response.Sentiment,
            ConfidenceScore = response.ConfidenceScore,
            Explanation = safeExplanation,
            PurchaseIntentScore = response.InsuranceAnalysis.PurchaseIntentScore,
            CustomerPersona = response.InsuranceAnalysis.CustomerPersona,
            JourneyStage = response.InsuranceAnalysis.JourneyStage,
            ChurnRisk = response.InsuranceAnalysis.RiskIndicators.ChurnRisk,
            ComplaintEscalationRisk = response.InsuranceAnalysis.RiskIndicators.ComplaintEscalationRisk,
            FraudIndicators = response.InsuranceAnalysis.RiskIndicators.FraudIndicators,
            KeyTopics = string.Join(",", response.InsuranceAnalysis.KeyTopics),
            PolicyRecommendationsJson = recommendationsJson,
            EmotionBreakdownJson = TruncateJson(JsonSerializer.Serialize(response.EmotionBreakdown), 1000),
            IsValid = response.Quality.IsValid,
            QualityScore = response.Quality.QualityScore,
            CreatedAt = DateTime.UtcNow
        };

        await _repository.SaveAsync(record);
        _logger.LogInformation("Analysis persisted with ID {Id}", record.Id);
    }

    private static ApiModels.InsuranceAnalysisResponse MapToResponse(AgentAnalysisResult agentResult)
    {
        return new ApiModels.InsuranceAnalysisResponse
        {
            Sentiment = agentResult.Sentiment,
            ConfidenceScore = Math.Clamp(agentResult.ConfidenceScore, 0.0, 1.0),
            Explanation = agentResult.Explanation,
            EmotionBreakdown = agentResult.EmotionBreakdown ?? new Dictionary<string, double>(),
            InsuranceAnalysis = new ApiModels.InsuranceAnalysisDetail
            {
                PurchaseIntentScore = Math.Clamp(agentResult.InsuranceAnalysis?.PurchaseIntentScore ?? 50, 0, 100),
                CustomerPersona = agentResult.InsuranceAnalysis?.CustomerPersona ?? "NewBuyer",
                JourneyStage = agentResult.InsuranceAnalysis?.JourneyStage ?? "Awareness",
                RiskIndicators = new ApiModels.RiskIndicatorDetail
                {
                    ChurnRisk = agentResult.InsuranceAnalysis?.RiskIndicators?.ChurnRisk ?? "Low",
                    ComplaintEscalationRisk = agentResult.InsuranceAnalysis?.RiskIndicators?.ComplaintEscalationRisk ?? "Low",
                    FraudIndicators = agentResult.InsuranceAnalysis?.RiskIndicators?.FraudIndicators ?? "None"
                },
                PolicyRecommendations = agentResult.InsuranceAnalysis?.PolicyRecommendations?
                    .Select(r => new ApiModels.PolicyRecommendationDetail
                    {
                        Product = r.Product,
                        Reasoning = r.Reasoning
                    }).ToList() ?? [],
                InteractionType = agentResult.InsuranceAnalysis?.InteractionType ?? "General",
                KeyTopics = agentResult.InsuranceAnalysis?.KeyTopics ?? []
            },
            Quality = MapQuality(agentResult)
        };
    }

    private static string TruncateJson(string json, int maxLength)
        => json.Length > maxLength ? json[..maxLength] : json;

    private static ApiModels.QualityDetail MapQuality(AgentAnalysisResult agentResult)
    {
        var issues = agentResult.Quality?.Issues?
            .Select(i => new ApiModels.QualityIssueDetail
            {
                Severity = i.Severity,
                Field = i.Field,
                Message = i.Message
            }).ToList() ?? [];

        var suggestions = agentResult.Quality?.Suggestions ?? [];

        // Build flattened warnings from issues + suggestions for backward compatibility
        var warnings = new List<string>();
        foreach (var issue in issues)
        {
            warnings.Add($"[{issue.Severity}] {issue.Field}: {issue.Message}");
        }
        warnings.AddRange(suggestions);

        return new ApiModels.QualityDetail
        {
            IsValid = agentResult.Quality?.IsValid ?? agentResult.IsSuccess,
            QualityScore = agentResult.Quality?.QualityScore ?? (agentResult.IsSuccess ? 80 : 0),
            Issues = issues,
            Suggestions = suggestions,
            Warnings = warnings
        };
    }
}
