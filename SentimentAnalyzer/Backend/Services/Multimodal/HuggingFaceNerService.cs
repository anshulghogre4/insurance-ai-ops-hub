using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using SentimentAnalyzer.Agents.Configuration;

namespace SentimentAnalyzer.API.Services.Multimodal;

/// <summary>
/// Named Entity Recognition service using HuggingFace Inference API (dslim/bert-base-NER).
/// Free tier: rate-limited (300 requests/hour).
/// Insurance use case: extracting policy numbers, names, dates, and monetary amounts
/// from claim descriptions and policy documents.
/// NOTE: NER requires unredacted text to detect entities. Callers should ensure the
/// extracted entities are handled securely and not forwarded to other external providers.
/// </summary>
public class HuggingFaceNerService : IEntityExtractionService
{
    private readonly HttpClient _httpClient;
    private readonly HuggingFaceSettings _settings;
    private readonly ILogger<HuggingFaceNerService> _logger;
    private static readonly string _baseUrl = "https://api-inference.huggingface.co/models";

    public HuggingFaceNerService(
        HttpClient httpClient,
        IOptions<AgentSystemSettings> settings,
        ILogger<HuggingFaceNerService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _settings = settings?.Value?.HuggingFace ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<EntityExtractionResult> ExtractEntitiesAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            return new EntityExtractionResult
            {
                IsSuccess = false,
                Provider = "HuggingFace",
                ErrorMessage = "HuggingFace API key not configured."
            };
        }

        try
        {
            // NER requires raw text to detect entities — PII redaction would defeat the purpose.
            // Log a warning so audit trails capture that unredacted text was sent externally.
            _logger.LogWarning("NER: Sending text ({Length} chars) to external HuggingFace API. " +
                "PII redaction is NOT applied to preserve entity detection accuracy. " +
                "Callers must handle extracted entities securely.", text.Length);

            var requestUrl = $"{_baseUrl}/{_settings.NerModel}";
            var payload = JsonSerializer.Serialize(new { inputs = text });

            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, cancellationToken);

            // Handle model cold start (503 with estimated_time)
            if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
            {
                var errorJson = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("HuggingFace model loading (cold start). Response: {Response}", errorJson);
                return new EntityExtractionResult
                {
                    IsSuccess = false,
                    Provider = "HuggingFace",
                    ErrorMessage = "Model is loading (cold start). Please retry in 20-30 seconds."
                };
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("HuggingFace API returned {StatusCode}: {Error}", response.StatusCode, errorBody);
                return new EntityExtractionResult
                {
                    IsSuccess = false,
                    Provider = "HuggingFace",
                    ErrorMessage = $"HuggingFace API error: {response.StatusCode}"
                };
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);

            var entities = new List<ExtractedEntity>();
            foreach (var entity in doc.RootElement.EnumerateArray())
            {
                var entityGroup = entity.GetProperty("entity_group").GetString() ?? string.Empty;
                var word = entity.GetProperty("word").GetString() ?? string.Empty;
                var score = entity.GetProperty("score").GetDouble();
                var start = entity.GetProperty("start").GetInt32();
                var end = entity.GetProperty("end").GetInt32();

                entities.Add(new ExtractedEntity
                {
                    Type = MapEntityType(entityGroup),
                    Value = word.Trim(),
                    Confidence = score,
                    StartIndex = start,
                    EndIndex = end
                });
            }

            // Post-process: extract insurance-specific entities via regex
            var insuranceEntities = ExtractInsuranceEntities(text);
            entities.AddRange(insuranceEntities);

            _logger.LogInformation("HuggingFace NER completed. BERT entities: {BertCount}, Insurance entities: {InsCount}, Total: {Total}",
                entities.Count - insuranceEntities.Count, insuranceEntities.Count, entities.Count);

            return new EntityExtractionResult
            {
                IsSuccess = true,
                Entities = entities,
                Provider = "HuggingFace"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HuggingFace NER extraction failed");
            return new EntityExtractionResult
            {
                IsSuccess = false,
                Provider = "HuggingFace",
                ErrorMessage = $"Entity extraction error: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Maps BERT NER entity types to standardized entity type names.
    /// </summary>
    private static string MapEntityType(string bertEntityGroup)
    {
        return bertEntityGroup.ToUpperInvariant() switch
        {
            "PER" => "PERSON",
            "ORG" => "ORGANIZATION",
            "LOC" => "LOCATION",
            "MISC" => "MISCELLANEOUS",
            _ => bertEntityGroup.ToUpperInvariant()
        };
    }

    /// <summary>
    /// Insurance-specific entity patterns that BERT NER doesn't detect.
    /// These regex patterns extract policy numbers, claim numbers, monetary amounts,
    /// dates, SSNs, phone numbers, and email addresses from claim text.
    /// </summary>
    private static readonly (Regex Pattern, string EntityType)[] InsurancePatterns =
    [
        // Policy numbers: HO-2024-789456, AUTO-12345, GL-2024-001
        (new Regex(@"\b[A-Z]{2,4}-\d{4,10}(?:-\d{1,8})?\b", RegexOptions.Compiled), "POLICY_NUMBER"),

        // Claim numbers: CLM-2024-12345678
        (new Regex(@"\bCLM-\d{4}-\d{4,8}\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "CLAIM_NUMBER"),

        // Monetary amounts: $250,000 or $1,234.56
        (new Regex(@"\$[\d,]+(?:\.\d{2})?\b", RegexOptions.Compiled), "MONEY"),

        // Dates: January 1, 2024 or 01/15/2024 or 2024-01-15
        (new Regex(@"\b(?:January|February|March|April|May|June|July|August|September|October|November|December)\s+\d{1,2},?\s+\d{4}\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "DATE"),
        (new Regex(@"\b\d{1,2}/\d{1,2}/\d{2,4}\b", RegexOptions.Compiled), "DATE"),
        (new Regex(@"\b\d{4}-\d{2}-\d{2}\b", RegexOptions.Compiled), "DATE"),

        // SSN: 123-45-6789
        (new Regex(@"\b\d{3}-\d{2}-\d{4}\b", RegexOptions.Compiled), "SSN"),

        // Phone numbers: (555) 123-4567 or 555-123-4567
        (new Regex(@"\(?\d{3}\)?[-.\s]?\d{3}[-.\s]?\d{4}\b", RegexOptions.Compiled), "PHONE"),

        // Email addresses
        (new Regex(@"\b[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}\b", RegexOptions.Compiled), "EMAIL")
    ];

    /// <summary>
    /// Extracts insurance-specific entities from text using regex patterns.
    /// Supplements BERT NER with domain-specific entity types.
    /// </summary>
    private static List<ExtractedEntity> ExtractInsuranceEntities(string text)
    {
        var entities = new List<ExtractedEntity>();
        var seen = new HashSet<string>(); // Deduplicate by value+type

        foreach (var (pattern, entityType) in InsurancePatterns)
        {
            foreach (Match match in pattern.Matches(text))
            {
                var key = $"{entityType}:{match.Value}";
                if (seen.Add(key))
                {
                    entities.Add(new ExtractedEntity
                    {
                        Type = entityType,
                        Value = match.Value.Trim(),
                        Confidence = 0.95, // Regex matches are high-confidence
                        StartIndex = match.Index,
                        EndIndex = match.Index + match.Length
                    });
                }
            }
        }

        return entities;
    }
}
