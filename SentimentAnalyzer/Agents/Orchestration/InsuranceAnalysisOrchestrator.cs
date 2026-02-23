using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using SentimentAnalyzer.Agents.Configuration;
using SentimentAnalyzer.Agents.Definitions;
using SentimentAnalyzer.Agents.Models;
using SentimentAnalyzer.Domain.Enums;

namespace SentimentAnalyzer.Agents.Orchestration;

/// <summary>
/// Orchestrates the multi-agent insurance analysis pipeline using Semantic Kernel AgentGroupChat.
/// Uses IResilientKernelProvider for automatic provider fallback on failures.
/// </summary>
public class InsuranceAnalysisOrchestrator : IAnalysisOrchestrator
{
    private readonly IResilientKernelProvider _kernelProvider;
    private readonly AgentConfiguration _agentConfig;
    private readonly ILogger<InsuranceAnalysisOrchestrator> _logger;
    private readonly IPIIRedactor? _piiRedactor;

    public InsuranceAnalysisOrchestrator(
        IResilientKernelProvider kernelProvider,
        IOptions<AgentConfiguration> agentConfig,
        ILogger<InsuranceAnalysisOrchestrator> logger,
        IPIIRedactor? piiRedactor = null)
    {
        _kernelProvider = kernelProvider ?? throw new ArgumentNullException(nameof(kernelProvider));
        _agentConfig = agentConfig?.Value ?? throw new ArgumentNullException(nameof(agentConfig));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _piiRedactor = piiRedactor;
    }

    /// <inheritdoc />
    public async Task<AgentAnalysisResult> AnalyzeAsync(string text, InteractionType interactionType = InteractionType.General)
    {
        _logger.LogInformation("Starting multi-agent insurance analysis for interaction type: {InteractionType}, Provider: {Provider}",
            interactionType, _kernelProvider.ActiveProviderName);

        // Redact PII before sending to external AI providers
        if (_piiRedactor == null)
        {
            _logger.LogWarning("PII redactor not configured. Sending unredacted text to AI providers.");
        }
        var sanitizedText = _piiRedactor?.Redact(text) ?? text;

        try
        {
            return await RunMultiAgentAnalysis(sanitizedText, interactionType);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Multi-agent analysis timed out after {Timeout}s", _agentConfig.TimeoutSeconds);

            if (_agentConfig.FallbackToSimpleAnalysis)
            {
                _logger.LogInformation("Falling back to single-agent analysis");
                return await RunSingleAgentFallback(sanitizedText, interactionType);
            }

            return CreateDefaultResult(sanitizedText, interactionType, "Analysis timed out");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during multi-agent insurance analysis with provider {Provider}", _kernelProvider.ActiveProviderName);

            // Report failure to trigger provider fallback
            _kernelProvider.ReportFailure(_kernelProvider.ActiveProviderName, ex);
            _logger.LogInformation("Provider fallback triggered. New active provider: {Provider}", _kernelProvider.ActiveProviderName);

            // Retry once with fallback provider
            try
            {
                _logger.LogInformation("Retrying multi-agent analysis with fallback provider: {Provider}", _kernelProvider.ActiveProviderName);
                return await RunMultiAgentAnalysis(sanitizedText, interactionType);
            }
            catch (Exception retryEx)
            {
                _logger.LogWarning(retryEx, "Retry with fallback provider {Provider} also failed", _kernelProvider.ActiveProviderName);
                _kernelProvider.ReportFailure(_kernelProvider.ActiveProviderName, retryEx);
            }

            if (_agentConfig.FallbackToSimpleAnalysis)
            {
                _logger.LogInformation("Falling back to single-agent analysis after provider failures");
                try
                {
                    return await RunSingleAgentFallback(sanitizedText, interactionType);
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(fallbackEx, "Single-agent fallback also failed");
                }
            }

            return CreateDefaultResult(sanitizedText, interactionType,
                $"All analysis paths failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public Task<AgentAnalysisResult> AnalyzeAsync(string text, OrchestrationProfile profile, InteractionType interactionType = InteractionType.General)
    {
        _logger.LogInformation("Analysis requested with profile: {Profile}. Delegating to default analysis pipeline.", profile);
        // Profile-aware agent selection will be implemented in Week 2 when claims/fraud agent prompts are ready.
        // For now, delegate to the standard analysis to maintain backward compatibility.
        return AnalyzeAsync(text, interactionType);
    }

    private async Task<AgentAnalysisResult> RunMultiAgentAnalysis(string text, InteractionType interactionType)
    {
        // Create agents
        var ctoAgent = CreateAgent(AgentDefinitions.CTOAgentName, AgentDefinitions.CTOAgentPrompt);
        var baAgent = CreateAgent(AgentDefinitions.BAAgentName, AgentDefinitions.BAAgentPrompt);
        var devAgent = CreateAgent(AgentDefinitions.DeveloperAgentName, AgentDefinitions.DeveloperAgentPrompt);
        var qaAgent = CreateAgent(AgentDefinitions.QAAgentName, AgentDefinitions.QAAgentPrompt);
        var architectAgent = CreateAgent(AgentDefinitions.ArchitectAgentName, AgentDefinitions.ArchitectAgentPrompt);
        var uxDesignerAgent = CreateAgent(AgentDefinitions.UXDesignerAgentName, AgentDefinitions.UXDesignerAgentPrompt);
        var aiExpertAgent = CreateAgent(AgentDefinitions.AIExpertAgentName, AgentDefinitions.AIExpertAgentPrompt);

        // Build the agent list based on configuration
        var agents = new List<ChatCompletionAgent> { ctoAgent, baAgent, devAgent, qaAgent, aiExpertAgent, uxDesignerAgent };
        if (_agentConfig.IncludeArchitectAgent)
        {
            agents.Add(architectAgent);
        }

        var terminationStrategy = new AnalysisTerminationStrategy(_agentConfig.MaxAgentTurns);
        var selectionStrategy = new AgentSelectionStrategy();

        // Create the group chat
        var chat = new AgentGroupChat(agents.ToArray())
        {
            ExecutionSettings = new()
            {
                SelectionStrategy = selectionStrategy,
                TerminationStrategy = terminationStrategy
            }
        };

        var userMessage = $"""
            Analyze the following customer interaction for insurance domain insights.
            Interaction Type: {interactionType}

            Customer Text:
            ---
            {text}
            ---

            Provide a complete analysis including sentiment, purchase intent, customer persona, journey stage, risk indicators, emotion breakdown, policy recommendations, and key topics.
            """;

        chat.AddChatMessage(new ChatMessageContent(AuthorRole.User, userMessage));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_agentConfig.TimeoutSeconds));
        var messages = new StringBuilder();
        string? finalJson = null;

        var agentTurnCount = 0;
        try
        {
            await foreach (var response in chat.InvokeAsync(cts.Token))
            {
                agentTurnCount++;
                _logger.LogInformation("Agent [{Agent}] (turn {Turn}): {Content}",
                    response.AuthorName ?? "Unknown",
                    agentTurnCount,
                    response.Content?.Length > 200 ? response.Content[..200] + "..." : response.Content);

                messages.AppendLine($"[{response.AuthorName}]: {response.Content}");

                // Capture the CTO's final output containing ANALYSIS_COMPLETE
                if (response.AuthorName == AgentDefinitions.CTOAgentName &&
                    response.Content?.Contains("ANALYSIS_COMPLETE") == true)
                {
                    finalJson = ExtractJson(response.Content);
                    _logger.LogInformation("CTO ANALYSIS_COMPLETE detected. JSON extracted: {Found}, Content length: {Length}",
                        finalJson != null, response.Content?.Length ?? 0);
                }
            }

            _logger.LogInformation("Agent conversation completed after {Turns} turns", agentTurnCount);
        }
        catch (Exception ex) when (finalJson != null)
        {
            // If we already have JSON from an earlier turn but a later turn hit an error
            // (e.g., rate limit 429), use the extracted JSON rather than failing entirely.
            _logger.LogWarning(ex, "Agent conversation error at turn {Turn}, but JSON was already extracted. Using captured JSON.",
                agentTurnCount);
        }

        // If CTO didn't produce final JSON, try to extract from last developer message
        if (finalJson == null)
        {
            _logger.LogWarning("No JSON from CTO's ANALYSIS_COMPLETE. Searching all {Length} chars of agent conversation...",
                messages.Length);
            finalJson = ExtractLastJson(messages.ToString());
            _logger.LogInformation("ExtractLastJson result: {Found}", finalJson != null);
        }

        if (finalJson != null)
        {
            _logger.LogInformation("Proceeding to parse extracted JSON. Length: {Length}, Preview: {Preview}",
                finalJson.Length,
                finalJson.Length > 300 ? finalJson[..300] + "..." : finalJson);
            return ParseAnalysisResult(finalJson, messages.ToString());
        }

        _logger.LogWarning("Multi-agent analysis did not produce valid JSON output after {Turns} turns. Full conversation length: {Length} chars. Returning default result.",
            agentTurnCount, messages.Length);
        return CreateDefaultResult(text, interactionType, messages.ToString());
    }

    /// <summary>
    /// Single-agent fallback when multi-agent orchestration fails or times out.
    /// Uses only the BA agent with developer formatting instructions combined.
    /// </summary>
    private async Task<AgentAnalysisResult> RunSingleAgentFallback(string text, InteractionType interactionType)
    {
        var combinedPrompt = $"""
            {AgentDefinitions.BAAgentPrompt}

            IMPORTANT: You must also format your output as the Developer agent would.
            {AgentDefinitions.DeveloperAgentPrompt}
            """;

        var chatService = _kernelProvider.GetKernel().GetRequiredService<IChatCompletionService>();
        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(combinedPrompt);
        chatHistory.AddUserMessage($"""
            Analyze this customer interaction for insurance domain insights.
            Interaction Type: {interactionType}

            Customer Text:
            ---
            {text}
            ---

            Return ONLY a valid JSON object with the analysis.
            """);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var response = await chatService.GetChatMessageContentAsync(chatHistory, cancellationToken: cts.Token);

        var json = ExtractJson(response.Content ?? "");
        if (json != null)
        {
            var result = ParseAnalysisResult(json, $"[FallbackAnalyst]: {response.Content}");
            result.Quality ??= new QualityMetadata();
            result.Quality.Suggestions = ["Analysis produced using single-agent fallback mode"];
            return result;
        }

        return CreateDefaultResult(text, interactionType, "Single-agent fallback did not produce valid JSON");
    }

    private ChatCompletionAgent CreateAgent(string name, string instructions)
    {
        return new ChatCompletionAgent
        {
            Name = name,
            Instructions = instructions,
            Kernel = _kernelProvider.GetKernel()
        };
    }

    private static readonly Regex MarkdownFenceRegex = new(
        @"```(?:json|JSON)?\s*\n?",
        RegexOptions.Compiled);

    /// <summary>
    /// Strips markdown code fences and normalizes LLM output before JSON extraction.
    /// Handles ```json ... ```, ```JSON ... ```, and plain ``` ... ``` wrappers.
    /// </summary>
    private static string NormalizeForJsonExtraction(string text)
    {
        return MarkdownFenceRegex.Replace(text, "").Trim();
    }

    /// <summary>
    /// Extracts the first valid JSON object from a text string.
    /// Strips markdown fences, then uses brace-counting to find candidates,
    /// then validates with JsonDocument.Parse.
    /// </summary>
    private static string? ExtractJson(string text)
    {
        var normalized = NormalizeForJsonExtraction(text);
        return ExtractJsonFromNormalized(normalized);
    }

    /// <summary>
    /// Brace-counting JSON extraction from normalized (fence-stripped) text.
    /// </summary>
    private static string? ExtractJsonFromNormalized(string text)
    {
        var startIndex = text.IndexOf('{');
        if (startIndex < 0) return null;

        var depth = 0;
        var inString = false;
        var escape = false;

        for (var i = startIndex; i < text.Length; i++)
        {
            var c = text[i];

            if (escape)
            {
                escape = false;
                continue;
            }

            if (c == '\\' && inString)
            {
                escape = true;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString) continue;

            if (c == '{') depth++;
            else if (c == '}') depth--;

            if (depth == 0)
            {
                var candidate = text[startIndex..(i + 1)];
                if (IsValidJson(candidate))
                {
                    return candidate;
                }
                // Not valid JSON, keep searching
                startIndex = text.IndexOf('{', i + 1);
                if (startIndex < 0) return null;
                i = startIndex - 1;
                depth = 0;
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts the last valid JSON object containing analysis fields from all agent messages.
    /// Searches each agent message block individually for better extraction accuracy.
    /// </summary>
    private static string? ExtractLastJson(string allMessages)
    {
        string? lastJson = null;
        var normalized = NormalizeForJsonExtraction(allMessages);
        var searchFrom = 0;

        while (searchFrom < normalized.Length)
        {
            var startIndex = normalized.IndexOf('{', searchFrom);
            if (startIndex < 0) break;

            var depth = 0;
            var inString = false;
            var escape = false;

            for (var i = startIndex; i < normalized.Length; i++)
            {
                var c = normalized[i];

                if (escape) { escape = false; continue; }
                if (c == '\\' && inString) { escape = true; continue; }
                if (c == '"') { inString = !inString; continue; }
                if (inString) continue;

                if (c == '{') depth++;
                else if (c == '}') depth--;

                if (depth == 0)
                {
                    var candidate = normalized[startIndex..(i + 1)];
                    // Validate it's actual JSON and contains expected analysis fields
                    if (candidate.Contains("sentiment") && candidate.Contains("confidenceScore")
                        && IsValidJson(candidate))
                    {
                        lastJson = candidate;
                    }
                    searchFrom = i + 1;
                    break;
                }
            }

            if (depth != 0) break; // Unterminated JSON
        }

        return lastJson;
    }

    /// <summary>
    /// Validates whether a string is well-formed JSON.
    /// </summary>
    private static bool IsValidJson(string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private AgentAnalysisResult ParseAnalysisResult(string json, string agentConversation)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        _logger.LogInformation("ParseAnalysisResult: Input JSON length={Length}, first 500 chars: {Preview}",
            json.Length, json.Length > 500 ? json[..500] + "..." : json);

        // Attempt 1: Strict deserialization
        try
        {
            var result = JsonSerializer.Deserialize<AgentAnalysisResult>(json, options);
            if (result != null)
            {
                result.RawAgentConversation = agentConversation;
                result.IsSuccess = true;
                _logger.LogInformation("Agent analysis JSON parsed successfully via strict deserialization. Sentiment={Sentiment}, Confidence={Confidence}",
                    result.Sentiment, result.ConfidenceScore);
                return result;
            }
            _logger.LogWarning("Strict deserialization returned null for JSON length: {Length}", json.Length);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Strict deserialization failed at {Path}. JSON length: {Length}, SHA256: {Hash}",
                ex.Path ?? "unknown", json.Length, ComputeSha256(json));
        }

        // Attempt 2: Manual field extraction from raw JsonDocument
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });
            var root = doc.RootElement;

            // Log available top-level properties for debugging
            var topLevelProps = new List<string>();
            foreach (var prop in root.EnumerateObject())
            {
                topLevelProps.Add($"{prop.Name}({prop.Value.ValueKind})");
            }
            _logger.LogInformation("Manual extraction: top-level JSON properties: [{Properties}]",
                string.Join(", ", topLevelProps));

            var result = new AgentAnalysisResult
            {
                IsSuccess = true,
                RawAgentConversation = agentConversation,
                Sentiment = GetStringProp(root, "sentiment", "Neutral"),
                ConfidenceScore = GetDoubleProp(root, "confidenceScore", 0.5),
                Explanation = GetStringProp(root, "explanation", "Analysis completed via manual extraction."),
                EmotionBreakdown = ExtractEmotionBreakdown(root),
                InsuranceAnalysis = ExtractInsuranceAnalysis(root),
                Quality = ExtractQuality(root)
            };

            _logger.LogInformation("Agent analysis JSON parsed via manual field extraction. Sentiment={Sentiment}, Confidence={Confidence}, HasInsuranceAnalysis={HasIA}",
                result.Sentiment, result.ConfidenceScore, result.InsuranceAnalysis != null);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Manual JSON extraction also failed. JSON length: {Length}, SHA256: {Hash}, Preview: {Preview}",
                json.Length, ComputeSha256(json), json.Length > 300 ? json[..300] + "..." : json);
        }

        return new AgentAnalysisResult
        {
            IsSuccess = false,
            RawAgentConversation = agentConversation,
            Sentiment = "Neutral",
            Explanation = "Analysis completed but output parsing failed. Raw agent conversation is available."
        };
    }

    #region Manual JSON extraction helpers

    private static string GetStringProp(JsonElement element, string name, string fallback)
    {
        if (element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
            return prop.GetString() ?? fallback;
        // Try PascalCase variant
        var pascal = char.ToUpperInvariant(name[0]) + name[1..];
        if (element.TryGetProperty(pascal, out prop) && prop.ValueKind == JsonValueKind.String)
            return prop.GetString() ?? fallback;
        return fallback;
    }

    private static double GetDoubleProp(JsonElement element, string name, double fallback)
    {
        if (element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Number)
            return prop.GetDouble();
        var pascal = char.ToUpperInvariant(name[0]) + name[1..];
        if (element.TryGetProperty(pascal, out prop) && prop.ValueKind == JsonValueKind.Number)
            return prop.GetDouble();
        return fallback;
    }

    private static int GetIntProp(JsonElement element, string name, int fallback)
    {
        if (element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Number)
            return NormalizeToInt100(prop.GetDouble());
        var pascal = char.ToUpperInvariant(name[0]) + name[1..];
        if (element.TryGetProperty(pascal, out prop) && prop.ValueKind == JsonValueKind.Number)
            return NormalizeToInt100(prop.GetDouble());
        return fallback;
    }

    /// <summary>
    /// Normalizes a numeric value to an integer in the 0-100 range.
    /// Handles LLM output that may be 0-1 (normalized) or 0-100 (percentage) scale.
    /// </summary>
    private static int NormalizeToInt100(double value)
    {
        if (value > 0.0 && value < 1.0)
            return (int)Math.Round(value * 100);
        return (int)Math.Round(Math.Clamp(value, 0, 100));
    }

    private static bool GetBoolProp(JsonElement element, string name, bool fallback)
    {
        if (element.TryGetProperty(name, out var prop) &&
            (prop.ValueKind == JsonValueKind.True || prop.ValueKind == JsonValueKind.False))
            return prop.GetBoolean();
        return fallback;
    }

    private static Dictionary<string, double> ExtractEmotionBreakdown(JsonElement root)
    {
        var emotions = new Dictionary<string, double>();
        JsonElement emotionEl;
        if (!root.TryGetProperty("emotionBreakdown", out emotionEl) &&
            !root.TryGetProperty("EmotionBreakdown", out emotionEl) &&
            !root.TryGetProperty("emotion_breakdown", out emotionEl))
            return emotions;

        if (emotionEl.ValueKind != JsonValueKind.Object) return emotions;

        foreach (var prop in emotionEl.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.Number)
                emotions[prop.Name] = prop.Value.GetDouble();
        }
        return emotions;
    }

    private static InsuranceAnalysisDetail ExtractInsuranceAnalysis(JsonElement root)
    {
        JsonElement ins;
        if (!root.TryGetProperty("insuranceAnalysis", out ins) &&
            !root.TryGetProperty("InsuranceAnalysis", out ins) &&
            !root.TryGetProperty("insurance_analysis", out ins))
            return new InsuranceAnalysisDetail();

        if (ins.ValueKind != JsonValueKind.Object) return new InsuranceAnalysisDetail();

        var detail = new InsuranceAnalysisDetail
        {
            PurchaseIntentScore = GetIntProp(ins, "purchaseIntentScore", 50),
            CustomerPersona = GetStringProp(ins, "customerPersona", "NewBuyer"),
            JourneyStage = GetStringProp(ins, "journeyStage", "Awareness"),
            InteractionType = GetStringProp(ins, "interactionType", "General"),
            KeyTopics = ExtractStringList(ins, "keyTopics"),
            PolicyRecommendations = ExtractPolicyRecommendations(ins)
        };

        // Risk indicators - handle both object and array formats from LLM
        JsonElement risk;
        if (ins.TryGetProperty("riskIndicators", out risk) ||
            ins.TryGetProperty("RiskIndicators", out risk))
        {
            if (risk.ValueKind == JsonValueKind.Object)
            {
                detail.RiskIndicators = new RiskIndicatorDetail
                {
                    ChurnRisk = GetStringProp(risk, "churnRisk", "Low"),
                    ComplaintEscalationRisk = GetStringProp(risk, "complaintEscalationRisk", "Low"),
                    FraudIndicators = GetStringProp(risk, "fraudIndicators", "None")
                };
            }
            else if (risk.ValueKind == JsonValueKind.Array)
            {
                detail.RiskIndicators = MapRiskIndicatorsFromArray(risk);
            }
        }

        return detail;
    }

    /// <summary>
    /// Maps a flat string array of risk indicators to the structured RiskIndicatorDetail model.
    /// </summary>
    private static RiskIndicatorDetail MapRiskIndicatorsFromArray(JsonElement arrayElement)
    {
        var detail = new RiskIndicatorDetail();
        foreach (var item in arrayElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String) continue;
            var text = (item.GetString() ?? "").ToLowerInvariant();

            if (text.Contains("churn") || text.Contains("switch") || text.Contains("cancel") || text.Contains("leave"))
                detail.ChurnRisk = InferSeverity(text);
            else if (text.Contains("complaint") || text.Contains("escalat") || text.Contains("attorney") || text.Contains("regulator"))
                detail.ComplaintEscalationRisk = InferSeverity(text);
            else if (text.Contains("fraud") || text.Contains("suspicious") || text.Contains("misrepresent"))
                detail.FraudIndicators = InferSeverity(text);
        }
        return detail;
    }

    private static string InferSeverity(string text)
    {
        if (text.Contains("no ") || text.Contains("none") || text.Contains("not "))
            return "None";
        if (text.Contains("high") || text.Contains("critical") || text.Contains("severe") || text.Contains("immediate"))
            return "High";
        if (text.Contains("medium") || text.Contains("moderate") || text.Contains("potential"))
            return "Medium";
        return "Low";
    }

    private static List<string> ExtractStringList(JsonElement element, string name)
    {
        var list = new List<string>();
        JsonElement arr;
        if (!element.TryGetProperty(name, out arr) &&
            !element.TryGetProperty(char.ToUpperInvariant(name[0]) + name[1..], out arr))
            return list;

        if (arr.ValueKind != JsonValueKind.Array) return list;

        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
                list.Add(item.GetString() ?? "");
        }
        return list;
    }

    private static List<PolicyRecommendationDetail> ExtractPolicyRecommendations(JsonElement element)
    {
        var recs = new List<PolicyRecommendationDetail>();
        JsonElement arr;
        if (!element.TryGetProperty("policyRecommendations", out arr) &&
            !element.TryGetProperty("PolicyRecommendations", out arr))
            return recs;

        if (arr.ValueKind != JsonValueKind.Array) return recs;

        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object)
            {
                recs.Add(new PolicyRecommendationDetail
                {
                    Product = GetStringProp(item, "product", ""),
                    Reasoning = GetStringProp(item, "reasoning", "")
                });
            }
            else if (item.ValueKind == JsonValueKind.String)
            {
                var text = item.GetString() ?? "";
                if (!string.IsNullOrWhiteSpace(text))
                {
                    recs.Add(new PolicyRecommendationDetail
                    {
                        Product = "General Recommendation",
                        Reasoning = text
                    });
                }
            }
        }
        return recs;
    }

    private static QualityMetadata? ExtractQuality(JsonElement root)
    {
        JsonElement q;
        if (!root.TryGetProperty("quality", out q) &&
            !root.TryGetProperty("Quality", out q))
            return null;

        if (q.ValueKind != JsonValueKind.Object) return null;

        return new QualityMetadata
        {
            IsValid = GetBoolProp(q, "isValid", true),
            QualityScore = GetIntProp(q, "qualityScore", 80),
            Issues = ExtractQualityIssues(q),
            Suggestions = ExtractStringList(q, "suggestions")
        };
    }

    private static List<QualityIssue> ExtractQualityIssues(JsonElement element)
    {
        var issues = new List<QualityIssue>();
        JsonElement arr;
        if (!element.TryGetProperty("issues", out arr) &&
            !element.TryGetProperty("Issues", out arr))
            return issues;

        if (arr.ValueKind != JsonValueKind.Array) return issues;

        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object)
            {
                issues.Add(new QualityIssue
                {
                    Severity = GetStringProp(item, "severity", "info"),
                    Field = GetStringProp(item, "field", ""),
                    Message = GetStringProp(item, "message", "")
                });
            }
        }
        return issues;
    }

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes)[..16];
    }

    #endregion

    private static AgentAnalysisResult CreateDefaultResult(string text, InteractionType interactionType, string notes)
    {
        return new AgentAnalysisResult
        {
            IsSuccess = false,
            Sentiment = "Neutral",
            ConfidenceScore = 0.0,
            Explanation = "Multi-agent analysis could not complete. " + notes,
            EmotionBreakdown = new Dictionary<string, double>(),
            InsuranceAnalysis = new InsuranceAnalysisDetail
            {
                PurchaseIntentScore = 50,
                CustomerPersona = "NewBuyer",
                JourneyStage = "Awareness",
                InteractionType = interactionType.ToString(),
                KeyTopics = []
            },
            RawAgentConversation = notes
        };
    }
}
