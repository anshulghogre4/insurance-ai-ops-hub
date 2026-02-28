using System.Text.RegularExpressions;
using Azure;
using Azure.AI.TextAnalytics;
using Microsoft.Extensions.Options;
using SentimentAnalyzer.Agents.Configuration;

namespace SentimentAnalyzer.API.Services.Multimodal;

/// <summary>
/// Named Entity Recognition service using Azure AI Language (Text Analytics SDK).
/// Free F0 tier: 5,000 text records/month. Hard cap — returns 429 after limit.
/// Insurance use case: extracting policyholder names, organizations, locations, dates,
/// monetary amounts, and insurance-specific identifiers from claim text.
///
/// NOTE: NER requires unredacted text to detect entities. Callers should ensure the
/// extracted entities are handled securely and not forwarded to other external providers.
///
/// Data safety: Azure does NOT train on customer data (any tier). Text is processed
/// and deleted within 24 hours per Microsoft's data handling policy.
/// </summary>
public class AzureLanguageNerService : IEntityExtractionService
{
    private readonly AzureLanguageSettings _settings;
    private readonly ILogger<AzureLanguageNerService> _logger;

    /// <summary>
    /// Initializes the Azure Language NER service.
    /// </summary>
    /// <param name="settings">Agent system settings containing Azure Language API key and endpoint.</param>
    /// <param name="logger">Logger instance for diagnostics and audit trail.</param>
    public AzureLanguageNerService(
        IOptions<AgentSystemSettings> settings,
        ILogger<AzureLanguageNerService> logger)
    {
        _settings = settings?.Value?.AzureLanguage ?? throw new ArgumentNullException(nameof(settings));
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
                Provider = "AzureLanguage",
                ErrorMessage = "Azure Language API key not configured."
            };
        }

        if (string.IsNullOrWhiteSpace(_settings.Endpoint))
        {
            return new EntityExtractionResult
            {
                IsSuccess = false,
                Provider = "AzureLanguage",
                ErrorMessage = "Azure Language endpoint not configured."
            };
        }

        try
        {
            // NER requires raw text to detect entities — PII redaction would defeat the purpose.
            // Log a warning so audit trails capture that unredacted text was sent externally.
            _logger.LogWarning(
                "NER: Sending text ({Length} chars) to Azure Language API. " +
                "PII redaction is NOT applied to preserve entity detection accuracy. " +
                "Azure does NOT train on customer data. Callers must handle extracted entities securely.",
                text.Length);

            var client = new TextAnalyticsClient(
                new Uri(_settings.Endpoint),
                new AzureKeyCredential(_settings.ApiKey));

            var response = await client.RecognizeEntitiesAsync(text, cancellationToken: cancellationToken);
            var azureEntities = response.Value;

            var entities = new List<ExtractedEntity>();
            var seen = new HashSet<string>(); // Deduplicate by (Type, Value)

            // Map Azure entity categories to standardized types
            foreach (var entity in azureEntities)
            {
                var mappedType = MapAzureCategory(entity.Category, entity.SubCategory);
                var key = $"{mappedType}:{entity.Text}";

                if (seen.Add(key))
                {
                    entities.Add(new ExtractedEntity
                    {
                        Type = mappedType,
                        Value = entity.Text,
                        Confidence = entity.ConfidenceScore,
                        StartIndex = entity.Offset,
                        EndIndex = entity.Offset + entity.Length
                    });
                }
            }

            // Post-process: extract insurance-specific entities via regex
            var insuranceEntities = ExtractInsuranceEntities(text);
            foreach (var insEntity in insuranceEntities)
            {
                var key = $"{insEntity.Type}:{insEntity.Value}";
                if (seen.Add(key))
                {
                    entities.Add(insEntity);
                }
            }

            _logger.LogInformation(
                "Azure Language NER completed. Azure entities: {AzureCount}, Insurance regex entities: {InsCount}, Total: {Total}",
                azureEntities.Count, insuranceEntities.Count, entities.Count);

            return new EntityExtractionResult
            {
                IsSuccess = true,
                Entities = entities,
                Provider = "AzureLanguage"
            };
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Azure Language NER request failed (HTTP {StatusCode})", ex.Status);
            return new EntityExtractionResult
            {
                IsSuccess = false,
                Provider = "AzureLanguage",
                ErrorMessage = $"Azure Language API error (HTTP {ex.Status}): {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure Language NER extraction failed");
            return new EntityExtractionResult
            {
                IsSuccess = false,
                Provider = "AzureLanguage",
                ErrorMessage = $"Entity extraction error: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Maps Azure Text Analytics entity categories to standardized entity type names.
    /// </summary>
    /// <param name="category">The Azure entity category.</param>
    /// <param name="subCategory">The Azure entity sub-category (nullable).</param>
    /// <returns>Standardized entity type string.</returns>
    private static string MapAzureCategory(EntityCategory category, string? subCategory)
    {
        if (category == EntityCategory.Person)
            return "PERSON";
        if (category == EntityCategory.Organization)
            return "ORGANIZATION";
        if (category == EntityCategory.Location)
            return "LOCATION";
        if (category == EntityCategory.DateTime)
            return "DATE";
        if (category == EntityCategory.Quantity && string.Equals(subCategory, "Currency", StringComparison.OrdinalIgnoreCase))
            return "MONEY";

        return category.ToString().ToUpperInvariant();
    }

    /// <summary>
    /// Insurance-specific entity patterns that Azure NER doesn't detect natively.
    /// These regex patterns extract policy numbers, claim numbers, SSNs,
    /// phone numbers, and email addresses from claim text.
    /// Matches the same patterns used by HuggingFaceNerService for consistency.
    /// </summary>
    private static readonly (Regex Pattern, string EntityType)[] _insurancePatterns =
    [
        // Policy numbers: HO-2024-789456, AUTO-12345, GL-2024-001
        (new Regex(@"\b[A-Z]{2,4}-?\d{6,10}\b", RegexOptions.Compiled), "POLICY_NUMBER"),

        // Claim numbers: CLM-2024001, CLAIM-12345678
        (new Regex(@"\b(?:CLM|CLAIM)-?\d{6,10}\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "CLAIM_NUMBER"),

        // SSN: 123-45-6789
        (new Regex(@"\b\d{3}-\d{2}-\d{4}\b", RegexOptions.Compiled), "SSN"),

        // Phone numbers: (555) 123-4567 or 555-123-4567 or +1-555-123-4567
        (new Regex(@"\b(?:\+1[-.]?)?\(?\d{3}\)?[-.\s]?\d{3}[-.\s]?\d{4}\b", RegexOptions.Compiled), "PHONE"),

        // Email addresses
        (new Regex(@"\b[\w.-]+@[\w.-]+\.\w{2,}\b", RegexOptions.Compiled), "EMAIL")
    ];

    /// <summary>
    /// Extracts insurance-specific entities from text using regex patterns.
    /// Supplements Azure NER with domain-specific entity types.
    /// </summary>
    /// <param name="text">The source text to scan for insurance entities.</param>
    /// <returns>List of extracted insurance-specific entities.</returns>
    private static List<ExtractedEntity> ExtractInsuranceEntities(string text)
    {
        var entities = new List<ExtractedEntity>();
        var seen = new HashSet<string>(); // Deduplicate by value+type

        foreach (var (pattern, entityType) in _insurancePatterns)
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
