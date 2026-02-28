using SentimentAnalyzer.Agents.Orchestration;
using SentimentAnalyzer.API.Models;

namespace SentimentAnalyzer.API.Services.Claims;

/// <summary>
/// Processes batch CSV uploads of insurance claims.
/// Parses CSV rows, validates required fields, redacts PII from descriptions,
/// and generates simulated triage results to avoid burning LLM quotas.
/// </summary>
public class BatchClaimService : IBatchClaimService
{
    private readonly IPIIRedactor _piiRedactor;
    private readonly ILogger<BatchClaimService> _logger;

    /// <summary>Expected column headers in the CSV file (case-insensitive).</summary>
    private static readonly string[] RequiredHeaders =
        ["ClaimId", "ClaimType", "Description", "EstimatedAmount", "IncidentDate"];

    public BatchClaimService(
        IPIIRedactor piiRedactor,
        ILogger<BatchClaimService> logger)
    {
        _piiRedactor = piiRedactor ?? throw new ArgumentNullException(nameof(piiRedactor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task<BatchClaimUploadResult> ProcessBatchAsync(Stream csvStream, CancellationToken ct = default)
    {
        var batchId = $"BATCH-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}";
        var result = new BatchClaimUploadResult { BatchId = batchId };

        _logger.LogInformation("Starting batch claim processing: {BatchId}", batchId);

        using var reader = new StreamReader(csvStream);
        var headerLine = reader.ReadLine();

        if (string.IsNullOrWhiteSpace(headerLine))
        {
            result.Status = "Failed";
            result.Errors.Add(new BatchClaimError
            {
                RowNumber = 0,
                Field = "File",
                ErrorMessage = "CSV file is empty or contains no header row."
            });
            return Task.FromResult(result);
        }

        // Parse and validate headers
        var headers = ParseCsvLine(headerLine);
        var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Length; i++)
        {
            headerMap[headers[i].Trim()] = i;
        }

        var missingHeaders = RequiredHeaders
            .Where(h => !headerMap.ContainsKey(h))
            .ToList();

        if (missingHeaders.Count > 0)
        {
            result.Status = "Failed";
            result.Errors.Add(new BatchClaimError
            {
                RowNumber = 0,
                Field = "Headers",
                ErrorMessage = $"Missing required columns: {string.Join(", ", missingHeaders)}. Expected: {string.Join(", ", RequiredHeaders)}"
            });
            return Task.FromResult(result);
        }

        var rowNumber = 1; // 1-based, header is row 0
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            ct.ThrowIfCancellationRequested();
            rowNumber++;

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            result.TotalCount++;

            var fields = ParseCsvLine(line);
            ProcessRow(result, fields, headerMap, rowNumber);
        }

        if (result.TotalCount == 0)
        {
            result.Status = "Failed";
            result.Errors.Add(new BatchClaimError
            {
                RowNumber = 0,
                Field = "File",
                ErrorMessage = "CSV file contains headers but no data rows."
            });
            return Task.FromResult(result);
        }

        result.ProcessedCount = result.SuccessCount + result.ErrorCount;
        result.Status = result.ErrorCount == result.TotalCount ? "Failed" : "Completed";

        _logger.LogInformation(
            "Batch {BatchId} complete: {Total} rows, {Success} succeeded, {Errors} errors",
            batchId, result.TotalCount, result.SuccessCount, result.ErrorCount);

        return Task.FromResult(result);
    }

    /// <summary>
    /// Validates and processes a single CSV data row.
    /// Adds either a result or an error to the batch result.
    /// </summary>
    private void ProcessRow(
        BatchClaimUploadResult batch,
        string[] fields,
        Dictionary<string, int> headerMap,
        int rowNumber)
    {
        // Extract field values safely
        var claimId = GetField(fields, headerMap, "ClaimId");
        var claimType = GetField(fields, headerMap, "ClaimType");
        var description = GetField(fields, headerMap, "Description");
        var amountStr = GetField(fields, headerMap, "EstimatedAmount");
        var dateStr = GetField(fields, headerMap, "IncidentDate");

        // Validate required fields
        if (string.IsNullOrWhiteSpace(claimId))
        {
            batch.Errors.Add(new BatchClaimError
            {
                RowNumber = rowNumber,
                Field = "ClaimId",
                ErrorMessage = "ClaimId is required and cannot be empty."
            });
            batch.ErrorCount++;
            return;
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            batch.Errors.Add(new BatchClaimError
            {
                RowNumber = rowNumber,
                Field = "Description",
                ErrorMessage = "Description is required and cannot be empty."
            });
            batch.ErrorCount++;
            return;
        }

        if (string.IsNullOrWhiteSpace(claimType))
        {
            batch.Errors.Add(new BatchClaimError
            {
                RowNumber = rowNumber,
                Field = "ClaimType",
                ErrorMessage = "ClaimType is required and cannot be empty."
            });
            batch.ErrorCount++;
            return;
        }

        // Validate numeric amount
        if (!string.IsNullOrWhiteSpace(amountStr))
        {
            // Strip currency symbols and commas for parsing
            var cleanAmount = amountStr.Replace("$", "").Replace(",", "").Trim();
            if (!decimal.TryParse(cleanAmount, out var amount) || amount < 0)
            {
                batch.Errors.Add(new BatchClaimError
                {
                    RowNumber = rowNumber,
                    Field = "EstimatedAmount",
                    ErrorMessage = $"EstimatedAmount '{amountStr}' is not a valid positive number."
                });
                batch.ErrorCount++;
                return;
            }
        }

        // Validate date format
        if (!string.IsNullOrWhiteSpace(dateStr))
        {
            if (!DateTime.TryParse(dateStr, out _))
            {
                batch.Errors.Add(new BatchClaimError
                {
                    RowNumber = rowNumber,
                    Field = "IncidentDate",
                    ErrorMessage = $"IncidentDate '{dateStr}' is not a valid date format."
                });
                batch.ErrorCount++;
                return;
            }
        }

        // PII redaction on description before processing
        var redactedDescription = _piiRedactor.Redact(description);
        _logger.LogDebug("Row {Row}: PII redacted description for claim {ClaimId}", rowNumber, claimId);

        // Generate simulated triage result (avoids burning LLM quotas)
        var triageResult = SimulateTriageResult(claimId, claimType, redactedDescription, rowNumber);
        batch.Results.Add(triageResult);
        batch.SuccessCount++;
    }

    /// <summary>
    /// Generates a realistic simulated triage result based on claim type and description keywords.
    /// This avoids calling the actual LLM provider chain for batch operations.
    /// </summary>
    private static BatchClaimItemResult SimulateTriageResult(
        string claimId, string claimType, string description, int rowNumber)
    {
        // Deterministic severity based on claim type keywords
        var severity = DetermineSeverity(claimType, description);
        var fraudScore = DetermineFraudScore(claimType, description);

        return new BatchClaimItemResult
        {
            RowNumber = rowNumber,
            ClaimId = claimId,
            Severity = severity,
            FraudScore = fraudScore,
            Status = "Triaged"
        };
    }

    /// <summary>
    /// Determines severity from claim type and description keywords.
    /// Uses realistic insurance heuristics for simulation.
    /// </summary>
    private static string DetermineSeverity(string claimType, string description)
    {
        var lowerType = claimType.ToLowerInvariant();
        var lowerDesc = description.ToLowerInvariant();

        if (lowerType.Contains("fire") || lowerType.Contains("total loss") ||
            lowerDesc.Contains("catastrophic") || lowerDesc.Contains("total loss") ||
            lowerDesc.Contains("structure fire"))
        {
            return "Critical";
        }

        if (lowerType.Contains("flood") || lowerType.Contains("water damage") ||
            lowerType.Contains("theft") || lowerDesc.Contains("significant") ||
            lowerDesc.Contains("extensive") || lowerDesc.Contains("hospitalized"))
        {
            return "High";
        }

        if (lowerType.Contains("auto") || lowerType.Contains("collision") ||
            lowerType.Contains("liability") || lowerDesc.Contains("moderate") ||
            lowerDesc.Contains("repair"))
        {
            return "Medium";
        }

        return "Low";
    }

    /// <summary>
    /// Determines a fraud risk score (0-100) from claim type and description keywords.
    /// </summary>
    private static int DetermineFraudScore(string claimType, string description)
    {
        var score = 15; // baseline
        var lowerDesc = description.ToLowerInvariant();
        var lowerType = claimType.ToLowerInvariant();

        if (lowerDesc.Contains("cash") || lowerDesc.Contains("no receipt"))
            score += 25;
        if (lowerDesc.Contains("recently purchased") || lowerDesc.Contains("new policy"))
            score += 20;
        if (lowerType.Contains("theft") || lowerType.Contains("fire"))
            score += 15;
        if (lowerDesc.Contains("total loss") || lowerDesc.Contains("everything destroyed"))
            score += 20;
        if (lowerDesc.Contains("witness") || lowerDesc.Contains("police report"))
            score -= 10;

        return Math.Clamp(score, 0, 100);
    }

    /// <summary>
    /// Safely retrieves a field value from the parsed CSV row by header name.
    /// </summary>
    private static string GetField(string[] fields, Dictionary<string, int> headerMap, string headerName)
    {
        if (headerMap.TryGetValue(headerName, out var index) && index < fields.Length)
        {
            return fields[index].Trim();
        }
        return string.Empty;
    }

    /// <summary>
    /// Parses a single CSV line respecting quoted fields (handles commas within quotes).
    /// Implements RFC 4180 basic parsing without external NuGet packages.
    /// </summary>
    internal static string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];

            if (inQuotes)
            {
                if (ch == '"')
                {
                    // Check for escaped quote (double quote "")
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++; // skip next quote
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(ch);
                }
            }
            else
            {
                if (ch == '"')
                {
                    inQuotes = true;
                }
                else if (ch == ',')
                {
                    fields.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(ch);
                }
            }
        }

        fields.Add(current.ToString());
        return fields.ToArray();
    }
}
