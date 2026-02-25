using System.Text.Json;
using Microsoft.Extensions.Logging;
using SentimentAnalyzer.Agents.Orchestration;
using SentimentAnalyzer.API.Data;
using SentimentAnalyzer.API.Data.Entities;
using SentimentAnalyzer.API.Models;
using SentimentAnalyzer.API.Services.Embeddings;

namespace SentimentAnalyzer.API.Services.Fraud;

/// <summary>
/// Cross-claim fraud correlation service. Identifies suspicious patterns across multiple
/// claims using four strategies: date proximity, narrative similarity (embeddings),
/// shared fraud flags, and severity/fraud-score clustering.
///
/// A correlation is only created when 2+ indicator types are detected for the same
/// claim pair, reducing false positives. Results are capped at 20 per analysis.
/// </summary>
public class FraudCorrelationService : IFraudCorrelationService
{
    /// <summary>Maximum number of correlations persisted per analysis run.</summary>
    private const int MaxCorrelationsPerAnalysis = 20;

    /// <summary>Minimum cosine similarity threshold for narrative similarity flagging.</summary>
    private const double NarrativeSimilarityThreshold = 0.85;

    /// <summary>Minimum shared fraud flags required for SharedFlags correlation.</summary>
    private const int MinSharedFlagsCount = 2;

    /// <summary>Minimum fraud score for SameSeverity correlation (both claims must meet this).</summary>
    private const double MinFraudScoreForSeverityCorrelation = 60.0;

    /// <summary>Safety cap on total claims fetched to prevent runaway pagination.</summary>
    private const int MaxClaimsToFetch = 5000;

    /// <summary>Page size for paginating through all claims during correlation analysis.</summary>
    private const int ClaimsFetchPageSize = 100;

    /// <summary>
    /// Date proximity windows by claim type. Long-tail fraud types (e.g., WorkersComp)
    /// get wider windows. Default is 180 days for unknown claim types.
    /// </summary>
    private static readonly Dictionary<string, int> DateProximityWindowByClaimType = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Auto", 90 },
        { "Property", 180 },
        { "Liability", 180 },
        { "WorkersComp", 365 },
    };

    /// <summary>Default date proximity window for claim types not in the dictionary.</summary>
    private const int DefaultDateProximityWindowDays = 180;

    private readonly IClaimsRepository _claimsRepo;
    private readonly IFraudCorrelationRepository _correlationRepo;
    private readonly IEmbeddingService _embeddingService;
    private readonly IPIIRedactor _piiRedactor;
    private readonly ILogger<FraudCorrelationService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="FraudCorrelationService"/>.
    /// </summary>
    /// <param name="claimsRepo">Repository for claim data access.</param>
    /// <param name="correlationRepo">Repository for fraud correlation persistence.</param>
    /// <param name="embeddingService">Embedding service for narrative similarity comparison.</param>
    /// <param name="piiRedactor">PII redactor for double-checking text before embedding.</param>
    /// <param name="logger">Logger instance.</param>
    public FraudCorrelationService(
        IClaimsRepository claimsRepo,
        IFraudCorrelationRepository correlationRepo,
        IEmbeddingService embeddingService,
        IPIIRedactor piiRedactor,
        ILogger<FraudCorrelationService> logger)
    {
        _claimsRepo = claimsRepo ?? throw new ArgumentNullException(nameof(claimsRepo));
        _correlationRepo = correlationRepo ?? throw new ArgumentNullException(nameof(correlationRepo));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _piiRedactor = piiRedactor ?? throw new ArgumentNullException(nameof(piiRedactor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<List<FraudCorrelationResponse>> AnalyzeCorrelationsAsync(int claimId, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting cross-claim fraud correlation analysis for claim {ClaimId}", claimId);

        var sourceClaim = await _claimsRepo.GetClaimByIdAsync(claimId);
        if (sourceClaim is null)
        {
            throw new KeyNotFoundException($"Claim {claimId} not found");
        }

        // Paginate through all claims to avoid missing correlations (QA-H1 / BA-C3)
        var candidateClaims = await FetchAllCandidateClaimsAsync(claimId);

        if (candidateClaims.Count == 0)
        {
            _logger.LogInformation("No other claims found to correlate with claim {ClaimId}", claimId);
            return [];
        }

        _logger.LogInformation(
            "Analyzing claim {ClaimId} against {CandidateCount} candidate claims",
            claimId, candidateClaims.Count);

        // Run all four correlation strategies and collect indicators per candidate
        var candidateIndicators = new Dictionary<int, List<CorrelationIndicator>>();

        foreach (var candidate in candidateClaims)
        {
            var indicators = new List<CorrelationIndicator>();

            // Strategy 1: Date Proximity (claim-type-aware window)
            var dateIndicator = EvaluateDateProximity(sourceClaim, candidate);
            if (dateIndicator is not null)
                indicators.Add(dateIndicator);

            // Strategy 3: Shared Fraud Flags (before embedding to save API calls)
            var flagIndicator = EvaluateSharedFraudFlags(sourceClaim, candidate);
            if (flagIndicator is not null)
                indicators.Add(flagIndicator);

            // Strategy 4: Same Severity + High Fraud Score
            var severityIndicator = EvaluateSameSeverityHighFraud(sourceClaim, candidate);
            if (severityIndicator is not null)
                indicators.Add(severityIndicator);

            if (indicators.Count > 0)
                candidateIndicators[candidate.Id] = indicators;
        }

        // Strategy 2: Narrative Similarity (embedding-based, batched for efficiency)
        // Only embed candidates that already have at least 1 indicator OR embed all if we want full coverage.
        // We embed all to catch narrative-only correlations that might combine with another strategy.
        await EvaluateNarrativeSimilarityBatchAsync(sourceClaim, candidateClaims, candidateIndicators, ct);

        // Filter: require 2+ indicator types per candidate to form a correlation
        var qualifiedCorrelations = new List<FraudCorrelationRecord>();

        foreach (var (candidateId, indicators) in candidateIndicators)
        {
            if (indicators.Count < 2)
                continue;

            // Create a composite correlation record with the highest score among indicators
            var compositeScore = indicators.Average(i => i.Score);
            var allTypes = string.Join("+", indicators.Select(i => i.Type).Distinct().OrderBy(t => t));
            var allDetails = string.Join(" | ", indicators.Select(i => i.Details));

            qualifiedCorrelations.Add(new FraudCorrelationRecord
            {
                SourceClaimId = claimId,
                CorrelatedClaimId = candidateId,
                CorrelationType = allTypes,
                CorrelationScore = Math.Round(compositeScore, 4),
                Details = allDetails.Length > 500 ? allDetails[..497] + "..." : allDetails,
                DetectedAt = DateTime.UtcNow
            });
        }

        // Cap at MaxCorrelationsPerAnalysis, keeping highest scores
        var topCorrelations = qualifiedCorrelations
            .OrderByDescending(c => c.CorrelationScore)
            .Take(MaxCorrelationsPerAnalysis)
            .ToList();

        // Persist the discovered correlations
        if (topCorrelations.Count > 0)
        {
            await _correlationRepo.SaveCorrelationsAsync(topCorrelations);
        }

        _logger.LogInformation(
            "Fraud correlation analysis complete for claim {ClaimId}: {Total} candidates analyzed, {Qualified} correlations found ({Persisted} persisted)",
            claimId, candidateClaims.Count, qualifiedCorrelations.Count, topCorrelations.Count);

        // Map to response models, enriching with both source and correlated claim metadata
        return topCorrelations.Select(r =>
        {
            var correlated = candidateClaims.FirstOrDefault(c => c.Id == r.CorrelatedClaimId);
            return MapToResponse(r, sourceClaim, correlated);
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<PaginatedResponse<FraudCorrelationResponse>> GetCorrelationsAsync(
        int claimId, int page = 1, int pageSize = 20)
    {
        var (records, totalCount) = await _correlationRepo.GetByClaimIdAsync(claimId, page, pageSize);
        return new PaginatedResponse<FraudCorrelationResponse>
        {
            Items = records.Select(r => MapToResponse(r, r.SourceClaim, r.CorrelatedClaim)).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <inheritdoc />
    public async Task<PaginatedResponse<FraudCorrelationResponse>> GetAllCorrelationsAsync(
        double minScore = 0.5, int page = 1, int pageSize = 50)
    {
        var (records, totalCount) = await _correlationRepo.GetAllAsync(minScore, page, pageSize);
        return new PaginatedResponse<FraudCorrelationResponse>
        {
            Items = records.Select(r => MapToResponse(r, r.SourceClaim, r.CorrelatedClaim)).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    // =========================================================================
    // Claim Fetching (Paginated)
    // =========================================================================

    /// <summary>
    /// Paginates through all claims to build a complete candidate list for correlation analysis.
    /// Excludes the source claim. Safety-capped at <see cref="MaxClaimsToFetch"/> claims.
    /// </summary>
    private async Task<List<ClaimRecord>> FetchAllCandidateClaimsAsync(int excludeClaimId)
    {
        var allCandidates = new List<ClaimRecord>();
        var page = 1;
        int totalCount;

        do
        {
            var (claims, total) = await _claimsRepo.GetClaimsAsync(pageSize: ClaimsFetchPageSize, page: page);
            totalCount = total;
            allCandidates.AddRange(claims.Where(c => c.Id != excludeClaimId));
            page++;
        } while (allCandidates.Count + 1 < totalCount && page <= MaxClaimsToFetch / ClaimsFetchPageSize);

        _logger.LogInformation(
            "Fetched {Count} candidate claims across {Pages} pages (total in system: {Total})",
            allCandidates.Count, page - 1, totalCount);

        return allCandidates;
    }

    // =========================================================================
    // Strategy 1: Date Proximity (claim-type-aware)
    // =========================================================================

    /// <summary>
    /// Evaluates whether two claims have date proximity with the same claim type.
    /// Uses claim-type-specific windows: Auto=90d, Property/Liability=180d, WorkersComp=365d.
    /// Unknown types default to 180 days.
    /// Score = 1.0 - (daysBetween / windowDays).
    /// </summary>
    private static CorrelationIndicator? EvaluateDateProximity(ClaimRecord source, ClaimRecord candidate)
    {
        if (!string.Equals(source.ClaimType, candidate.ClaimType, StringComparison.OrdinalIgnoreCase))
            return null;

        var windowDays = GetDateProximityWindow(source.ClaimType);
        var daysBetween = Math.Abs((source.CreatedAt - candidate.CreatedAt).TotalDays);

        if (daysBetween > windowDays)
            return null;

        var score = 1.0 - (daysBetween / windowDays);

        return new CorrelationIndicator
        {
            Type = "DateProximity",
            Score = score,
            Details = $"Same claim type ({source.ClaimType}), {daysBetween:F0} days apart within {windowDays}-day window (score: {score:F2})"
        };
    }

    /// <summary>
    /// Gets the date proximity window in days for a given claim type.
    /// Falls back to <see cref="DefaultDateProximityWindowDays"/> for unknown types.
    /// </summary>
    private static int GetDateProximityWindow(string claimType)
    {
        return DateProximityWindowByClaimType.TryGetValue(claimType, out var window)
            ? window
            : DefaultDateProximityWindowDays;
    }

    // =========================================================================
    // Strategy 2: Similar Narrative (Embedding-based)
    // =========================================================================

    /// <summary>
    /// Embeds the source claim text and compares against all candidate claim texts
    /// using cosine similarity. Flags pairs with similarity above the threshold.
    /// PII-redacts text before embedding as a safety double-check.
    /// </summary>
    private async Task EvaluateNarrativeSimilarityBatchAsync(
        ClaimRecord source,
        List<ClaimRecord> candidates,
        Dictionary<int, List<CorrelationIndicator>> indicators,
        CancellationToken ct)
    {
        try
        {
            // Double-check PII redaction (ClaimText should already be redacted, but enforce)
            var sourceText = _piiRedactor.Redact(source.ClaimText);

            var sourceEmbedding = await _embeddingService.GenerateEmbeddingAsync(
                sourceText, "document", ct);

            if (!sourceEmbedding.IsSuccess || sourceEmbedding.Embedding.Length == 0)
            {
                _logger.LogWarning(
                    "Failed to generate embedding for source claim {ClaimId}: {Error}",
                    source.Id, sourceEmbedding.ErrorMessage ?? "Unknown error");
                return;
            }

            // Batch embed candidate claim texts for efficiency
            var candidateTexts = candidates
                .Select(c => _piiRedactor.Redact(c.ClaimText))
                .ToArray();

            var batchResult = await _embeddingService.GenerateBatchEmbeddingsAsync(
                candidateTexts, "document", ct);

            if (!batchResult.IsSuccess || batchResult.Embeddings.Length == 0)
            {
                _logger.LogWarning(
                    "Failed to generate batch embeddings for {Count} candidates: {Error}",
                    candidates.Count, batchResult.ErrorMessage ?? "Unknown error");

                // Fall back to individual embeddings
                await EvaluateNarrativeSimilarityIndividualAsync(source, sourceEmbedding, candidates, indicators, ct);
                return;
            }

            for (var i = 0; i < candidates.Count && i < batchResult.Embeddings.Length; i++)
            {
                var candidateEmbedding = batchResult.Embeddings[i];
                if (candidateEmbedding.Length == 0) continue;

                var similarity = CosineSimilarity.Compute(sourceEmbedding.Embedding, candidateEmbedding);

                if (similarity >= NarrativeSimilarityThreshold)
                {
                    var indicator = new CorrelationIndicator
                    {
                        Type = "SimilarNarrative",
                        Score = similarity,
                        Details = $"Narrative cosine similarity: {similarity:F4} (threshold: {NarrativeSimilarityThreshold})"
                    };

                    if (!indicators.ContainsKey(candidates[i].Id))
                        indicators[candidates[i].Id] = [];

                    indicators[candidates[i].Id].Add(indicator);
                }
            }

            _logger.LogInformation(
                "Narrative similarity analysis complete: {Count} candidates evaluated via batch embedding",
                candidates.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Narrative similarity analysis failed for claim {ClaimId}; skipping strategy",
                source.Id);
        }
    }

    /// <summary>
    /// Fallback: individually embed each candidate when batch embedding fails.
    /// </summary>
    private async Task EvaluateNarrativeSimilarityIndividualAsync(
        ClaimRecord source,
        EmbeddingResult sourceEmbedding,
        List<ClaimRecord> candidates,
        Dictionary<int, List<CorrelationIndicator>> indicators,
        CancellationToken ct)
    {
        foreach (var candidate in candidates)
        {
            try
            {
                var candidateText = _piiRedactor.Redact(candidate.ClaimText);
                var candidateEmbedding = await _embeddingService.GenerateEmbeddingAsync(
                    candidateText, "document", ct);

                if (!candidateEmbedding.IsSuccess || candidateEmbedding.Embedding.Length == 0)
                    continue;

                var similarity = CosineSimilarity.Compute(
                    sourceEmbedding.Embedding, candidateEmbedding.Embedding);

                if (similarity >= NarrativeSimilarityThreshold)
                {
                    var indicator = new CorrelationIndicator
                    {
                        Type = "SimilarNarrative",
                        Score = similarity,
                        Details = $"Narrative cosine similarity: {similarity:F4} (threshold: {NarrativeSimilarityThreshold})"
                    };

                    if (!indicators.ContainsKey(candidate.Id))
                        indicators[candidate.Id] = [];

                    indicators[candidate.Id].Add(indicator);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Individual embedding failed for candidate claim {CandidateId}; skipping",
                    candidate.Id);
            }
        }
    }

    // =========================================================================
    // Strategy 3: Shared Fraud Flags
    // =========================================================================

    /// <summary>
    /// Parses FraudFlagsJson from both claims and identifies shared flags.
    /// Requires 2+ flags in common to flag a correlation.
    /// Score = sharedCount / totalUniqueFlags.
    /// </summary>
    private CorrelationIndicator? EvaluateSharedFraudFlags(ClaimRecord source, ClaimRecord candidate)
    {
        var sourceFlags = ParseFraudFlags(source.FraudFlagsJson);
        var candidateFlags = ParseFraudFlags(candidate.FraudFlagsJson);

        if (sourceFlags.Count == 0 || candidateFlags.Count == 0)
            return null;

        var sharedFlags = sourceFlags.Intersect(candidateFlags, StringComparer.OrdinalIgnoreCase).ToList();

        if (sharedFlags.Count < MinSharedFlagsCount)
            return null;

        var totalUnique = sourceFlags.Union(candidateFlags, StringComparer.OrdinalIgnoreCase).Count();
        var score = totalUnique > 0 ? (double)sharedFlags.Count / totalUnique : 0.0;

        return new CorrelationIndicator
        {
            Type = "SharedFlags",
            Score = score,
            Details = $"{sharedFlags.Count} shared fraud flags: {string.Join(", ", sharedFlags.Take(3))}" +
                      (sharedFlags.Count > 3 ? $" (+{sharedFlags.Count - 3} more)" : "")
        };
    }

    /// <summary>
    /// Safely parses FraudFlagsJson (a JSON string array) into a list of strings.
    /// </summary>
    private List<string> ParseFraudFlags(string fraudFlagsJson)
    {
        if (string.IsNullOrWhiteSpace(fraudFlagsJson) || fraudFlagsJson == "[]")
            return [];

        try
        {
            var flags = JsonSerializer.Deserialize<List<string>>(fraudFlagsJson);
            return flags ?? [];
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse FraudFlagsJson; treating as empty");
            return [];
        }
    }

    // =========================================================================
    // Strategy 4: Same Severity + High Fraud Score
    // =========================================================================

    /// <summary>
    /// Evaluates whether both claims have the same severity AND both have a fraud score
    /// at or above the threshold. Score = avg(fraudScore1, fraudScore2) / 100.
    /// </summary>
    private static CorrelationIndicator? EvaluateSameSeverityHighFraud(ClaimRecord source, ClaimRecord candidate)
    {
        if (source.FraudScore < MinFraudScoreForSeverityCorrelation ||
            candidate.FraudScore < MinFraudScoreForSeverityCorrelation)
            return null;

        if (!string.Equals(source.Severity, candidate.Severity, StringComparison.OrdinalIgnoreCase))
            return null;

        var score = (source.FraudScore + candidate.FraudScore) / 200.0;

        return new CorrelationIndicator
        {
            Type = "SameSeverity",
            Score = score,
            Details = $"Same severity ({source.Severity}), both high fraud: source={source.FraudScore:F0}, correlated={candidate.FraudScore:F0}"
        };
    }

    // =========================================================================
    // Mapping
    // =========================================================================

    /// <summary>
    /// Maps a <see cref="FraudCorrelationRecord"/> to a <see cref="FraudCorrelationResponse"/>,
    /// enriching with metadata from both the source and correlated claims when available.
    /// Parses the composite CorrelationType string into individual type entries.
    /// </summary>
    private static FraudCorrelationResponse MapToResponse(
        FraudCorrelationRecord record,
        ClaimRecord? sourceClaim,
        ClaimRecord? correlatedClaim)
    {
        return new FraudCorrelationResponse
        {
            Id = record.Id,
            SourceClaimId = record.SourceClaimId,
            CorrelatedClaimId = record.CorrelatedClaimId,
            CorrelationType = record.CorrelationType,
            CorrelationTypes = string.IsNullOrWhiteSpace(record.CorrelationType)
                ? []
                : record.CorrelationType.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            CorrelationScore = record.CorrelationScore,
            Details = record.Details,
            SourceClaimSeverity = sourceClaim?.Severity,
            SourceClaimType = sourceClaim?.ClaimType,
            SourceFraudScore = sourceClaim?.FraudScore,
            CorrelatedClaimSeverity = correlatedClaim?.Severity,
            CorrelatedClaimType = correlatedClaim?.ClaimType,
            CorrelatedFraudScore = correlatedClaim?.FraudScore,
            DetectedAt = record.DetectedAt,
            Status = record.Status,
            ReviewedBy = record.ReviewedBy,
            ReviewedAt = record.ReviewedAt,
            DismissalReason = record.DismissalReason
        };
    }

    // =========================================================================
    // Internal types
    // =========================================================================

    /// <summary>
    /// Internal indicator representing one detected correlation signal between two claims.
    /// Multiple indicators for the same claim pair are aggregated before creating a record.
    /// </summary>
    private sealed class CorrelationIndicator
    {
        /// <summary>Correlation type: DateProximity, SimilarNarrative, SharedFlags, SameSeverity.</summary>
        public string Type { get; init; } = string.Empty;

        /// <summary>Score for this individual indicator (0.0-1.0).</summary>
        public double Score { get; init; }

        /// <summary>Human-readable details for this indicator.</summary>
        public string Details { get; init; } = string.Empty;
    }
}
