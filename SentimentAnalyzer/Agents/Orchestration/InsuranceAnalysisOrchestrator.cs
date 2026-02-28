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
    private readonly IOrchestrationProfileFactory _profileFactory;
    private readonly AgentConfiguration _agentConfig;
    private readonly ILogger<InsuranceAnalysisOrchestrator> _logger;
    private readonly IPIIRedactor? _piiRedactor;

    public InsuranceAnalysisOrchestrator(
        IResilientKernelProvider kernelProvider,
        IOrchestrationProfileFactory profileFactory,
        IOptions<AgentConfiguration> agentConfig,
        ILogger<InsuranceAnalysisOrchestrator> logger,
        IPIIRedactor? piiRedactor = null)
    {
        _kernelProvider = kernelProvider ?? throw new ArgumentNullException(nameof(kernelProvider));
        _profileFactory = profileFactory ?? throw new ArgumentNullException(nameof(profileFactory));
        _agentConfig = agentConfig?.Value ?? throw new ArgumentNullException(nameof(agentConfig));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _piiRedactor = piiRedactor;
    }

    /// <inheritdoc />
    public async Task<AgentAnalysisResult> AnalyzeAsync(string text, InteractionType interactionType = InteractionType.General)
    {
        _logger.LogInformation("Starting multi-agent insurance analysis for interaction type: {InteractionType}, Provider: {Provider}",
            interactionType, _kernelProvider.ActiveProviderName);

        var sanitizedText = SanitizeText(text);

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

            // Report failure and walk the entire fallback chain — retry all remaining providers
            _kernelProvider.ReportFailure(_kernelProvider.ActiveProviderName, ex);
            var maxRetries = Math.Max(1, _kernelProvider.GetHealthStatus().Count - 1);

            for (var attempt = 1; attempt <= maxRetries; attempt++)
            {
                var nextProvider = _kernelProvider.ActiveProviderName;
                try
                {
                    _logger.LogInformation("Retry #{Attempt} with provider: {Provider}", attempt, nextProvider);
                    return await RunMultiAgentAnalysis(sanitizedText, interactionType);
                }
                catch (Exception retryEx)
                {
                    _logger.LogWarning(retryEx, "Retry #{Attempt} failed with provider {Provider}", attempt, nextProvider);
                    _kernelProvider.ReportFailure(nextProvider, retryEx);
                }
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
    public async Task<AgentAnalysisResult> AnalyzeAsync(string text, OrchestrationProfile profile, InteractionType interactionType = InteractionType.General)
    {
        // For SentimentAnalysis profile, use the standard multi-agent pipeline
        if (profile == OrchestrationProfile.SentimentAnalysis)
        {
            return await AnalyzeAsync(text, interactionType);
        }

        _logger.LogInformation("Starting profile-aware analysis. Profile: {Profile}, Provider: {Provider}",
            profile, _kernelProvider.ActiveProviderName);

        var sanitizedText = SanitizeText(text);

        try
        {
            return await RunProfileAgentAnalysis(sanitizedText, profile, interactionType);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Profile {Profile} analysis timed out after {Timeout}s", profile, _agentConfig.TimeoutSeconds);

            if (_agentConfig.FallbackToSimpleAnalysis)
            {
                _logger.LogInformation("Falling back to single-agent analysis for profile {Profile}", profile);
                return await RunSingleAgentFallback(sanitizedText, interactionType);
            }

            return CreateDefaultResult(sanitizedText, interactionType, $"Profile {profile} analysis timed out");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Profile-aware analysis failed for profile {Profile} with provider {Provider}",
                profile, _kernelProvider.ActiveProviderName);

            // Report failure and walk the entire fallback chain — retry all remaining providers
            _kernelProvider.ReportFailure(_kernelProvider.ActiveProviderName, ex);
            var maxRetries = Math.Max(1, _kernelProvider.GetHealthStatus().Count - 1);

            for (var attempt = 1; attempt <= maxRetries; attempt++)
            {
                var nextProvider = _kernelProvider.ActiveProviderName;
                try
                {
                    _logger.LogInformation("Retry #{Attempt} for profile {Profile} with provider: {Provider}",
                        attempt, profile, nextProvider);
                    return await RunProfileAgentAnalysis(sanitizedText, profile, interactionType);
                }
                catch (Exception retryEx)
                {
                    _logger.LogWarning(retryEx, "Retry #{Attempt} failed for profile {Profile} with provider {Provider}",
                        attempt, profile, nextProvider);
                    _kernelProvider.ReportFailure(nextProvider, retryEx);
                }
            }

            if (_agentConfig.FallbackToSimpleAnalysis)
            {
                _logger.LogInformation("Falling back to profile-aware single-agent analysis for {Profile} after provider failures", profile);
                try
                {
                    return await RunProfileSingleAgentFallback(sanitizedText, profile, interactionType);
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(fallbackEx, "Profile-aware single-agent fallback also failed for {Profile}", profile);
                }
            }

            return CreateDefaultResult(sanitizedText, interactionType,
                $"Profile-aware analysis failed for {profile}: {ex.Message}");
        }
    }

    /// <summary>
    /// Runs the multi-agent analysis using a specific orchestration profile.
    /// Only creates agents relevant to the profile, reducing token usage.
    /// </summary>
    private async Task<AgentAnalysisResult> RunProfileAgentAnalysis(
        string text, OrchestrationProfile profile, InteractionType interactionType)
    {
        var agentNames = _profileFactory.GetAgentNamesForProfile(profile);
        var maxTurns = _profileFactory.GetMaxTurnsForProfile(profile);
        var minTurns = _profileFactory.GetMinTurnsForProfile(profile);

        _logger.LogInformation("Profile {Profile}: {Count} agents ({Agents}), max {Max} turns, min {Min} turns",
            profile, agentNames.Count, string.Join(", ", agentNames), maxTurns, minTurns);

        // Create only the agents needed for this profile
        var agents = new List<ChatCompletionAgent>();
        foreach (var agentName in agentNames)
        {
            var (name, prompt) = ResolveAgentDefinition(agentName);
            if (prompt != null)
            {
                agents.Add(CreateAgent(name, prompt));
            }
            else
            {
                _logger.LogWarning("No prompt found for agent: {Agent}. Skipping.", agentName);
            }
        }

        if (agents.Count == 0)
        {
            _logger.LogError("No agents could be created for profile {Profile}", profile);
            return CreateDefaultResult(text, interactionType, $"No agents available for profile {profile}");
        }

        var terminationStrategy = new AnalysisTerminationStrategy(maxTurns, minTurns);
        var selectionStrategy = new AgentSelectionStrategy();

        var chat = new AgentGroupChat(agents.ToArray())
        {
            ExecutionSettings = new()
            {
                SelectionStrategy = selectionStrategy,
                TerminationStrategy = terminationStrategy
            }
        };

        var userMessage = BuildProfileUserMessage(text, profile, interactionType);
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
                _logger.LogInformation("[{Profile}] Agent [{Agent}] (turn {Turn}): {Content}",
                    profile, response.AuthorName ?? "Unknown", agentTurnCount,
                    response.Content?.Length > 200 ? response.Content[..200] + "..." : response.Content);

                messages.AppendLine($"[{response.AuthorName}]: {response.Content}");

                // Try to extract JSON from each agent's output
                var json = ExtractJson(response.Content ?? "");
                if (json != null)
                {
                    finalJson = json;
                }
            }
        }
        catch (Exception ex) when (finalJson != null)
        {
            _logger.LogWarning(ex, "Profile agent conversation error at turn {Turn}, but JSON was already extracted.", agentTurnCount);
        }

        _logger.LogInformation("Profile {Profile} conversation completed after {Turns} turns", profile, agentTurnCount);

        if (finalJson != null)
        {
            return ParseAnalysisResult(finalJson, messages.ToString());
        }

        // Try extracting from the full conversation
        finalJson = ExtractLastJsonForProfile(messages.ToString(), profile);
        if (finalJson != null)
        {
            return ParseAnalysisResult(finalJson, messages.ToString());
        }

        _logger.LogWarning("Profile {Profile} did not produce valid JSON after {Turns} turns", profile, agentTurnCount);
        return CreateDefaultResult(text, interactionType, $"Profile {profile} analysis did not produce JSON output");
    }

    /// <summary>
    /// Resolves an agent name to its (name, prompt) pair.
    /// </summary>
    internal static (string Name, string? Prompt) ResolveAgentDefinition(string agentName)
    {
        return agentName switch
        {
            AgentDefinitions.CTOAgentName => (agentName, AgentDefinitions.CTOAgentPrompt),
            AgentDefinitions.BAAgentName => (agentName, AgentDefinitions.BAAgentPrompt),
            AgentDefinitions.DeveloperAgentName => (agentName, AgentDefinitions.DeveloperAgentPrompt),
            AgentDefinitions.QAAgentName => (agentName, AgentDefinitions.QAAgentPrompt),
            AgentDefinitions.AIExpertAgentName => (agentName, AgentDefinitions.AIExpertAgentPrompt),
            AgentDefinitions.UXDesignerAgentName => (agentName, AgentDefinitions.UXDesignerAgentPrompt),
            AgentDefinitions.ArchitectAgentName => (agentName, AgentDefinitions.ArchitectAgentPrompt),
            AgentDefinitions.ClaimsTriageAgentName => (agentName, AgentDefinitions.ClaimsTriageAgentPrompt),
            AgentDefinitions.FraudDetectionAgentName => (agentName, AgentDefinitions.FraudDetectionAgentPrompt),
            AgentDefinitions.CustomerExperienceAgentName => (agentName, AgentDefinitions.CustomerExperienceAgentPrompt),
            _ => (agentName, null)
        };
    }

    /// <summary>
    /// Builds a profile-specific user message with appropriate instructions.
    /// </summary>
    internal static string BuildProfileUserMessage(string text, OrchestrationProfile profile, InteractionType interactionType)
    {
        return profile switch
        {
            OrchestrationProfile.ClaimsTriage => $$"""
                Triage the following insurance claim. Assess severity, urgency, claim type, and preliminary fraud risk.
                Interaction Type: {{interactionType}}

                Claim Description:
                ---
                {{text}}
                ---

                Provide a complete triage assessment. Output ONLY raw JSON matching this schema (NO markdown code fences):
                {
                  "claimTriage": {
                    "severity": "Critical|High|Medium|Low",
                    "urgency": "Immediate|Urgent|Standard|Low",
                    "claimType": "string",
                    "claimSubType": "string",
                    "estimatedLossRange": "string",
                    "preliminaryFraudRisk": "VeryLow|Low|Medium|High|VeryHigh",
                    "fraudFlags": ["string"],
                    "recommendedActions": [{ "action": "string", "priority": "High|Standard|Low", "reasoning": "string" }]
                  }
                }
                """,
            OrchestrationProfile.FraudScoring => $$"""
                Perform a detailed fraud analysis on the following insurance claim.
                Interaction Type: {{interactionType}}

                Claim Description:
                ---
                {{text}}
                ---

                Analyze for fraud indicators and provide a risk assessment. Output ONLY raw JSON matching this schema (NO markdown code fences):
                {
                  "fraudAnalysis": {
                    "fraudProbabilityScore": 0-100,
                    "riskLevel": "VeryLow|Low|Medium|High|VeryHigh",
                    "indicators": [{ "category": "Timing|Behavioral|Financial|Pattern|Documentation", "description": "string", "severity": "Low|Medium|High" }],
                    "recommendedActions": [{ "action": "string", "priority": "High|Standard|Low", "reasoning": "string" }],
                    "referToSIU": true|false,
                    "siuReferralReason": "string",
                    "confidenceInAssessment": 0.0-1.0
                  }
                }
                """,
            OrchestrationProfile.CustomerExperience => $$"""
                You are part of an insurance customer experience team. Analyze the following customer message and provide a helpful, empathetic response.

                Customer Message:
                ---
                {{text}}
                ---

                Interaction Context: {{interactionType}}

                Respond with ONLY a JSON object (no markdown fences):
                {
                  "response": "Your empathetic, helpful response to the customer",
                  "tone": "Professional|Empathetic|Urgent|Informational",
                  "escalationRecommended": true/false,
                  "escalationReason": "reason or null",
                  "customerIntent": "Information|ComplaintResolution|ClaimStatus|PolicyChange|Escalation",
                  "sentiment": "Positive|Neutral|Negative|Mixed",
                  "confidenceScore": 0.0-1.0,
                  "explanation": "Brief analysis of the customer's concern",
                  "suggestedFollowUp": ["Follow-up actions for the support team"],
                  "quality": {
                    "isValid": true,
                    "qualityScore": 0-100,
                    "issues": [],
                    "suggestions": ["Improvement suggestions"]
                  }
                }
                """,
            _ => $"""
                Analyze the following customer interaction for insurance domain insights.
                Interaction Type: {interactionType}

                Customer Text:
                ---
                {text}
                ---

                Provide a complete analysis. Output ONLY raw JSON, NO markdown code fences.
                """
        };
    }

    /// <summary>
    /// Extracts the last valid JSON from profile agent conversations.
    /// For claims/fraud profiles, looks for claimTriage or fraudAnalysis keys.
    /// </summary>
    internal static string? ExtractLastJsonForProfile(string allMessages, OrchestrationProfile profile)
    {
        string? lastJson = null;
        var normalized = NormalizeForJsonExtraction(allMessages);
        var searchFrom = 0;

        // Determine which JSON keys to look for based on profile
        var expectedKeys = profile switch
        {
            OrchestrationProfile.ClaimsTriage => new[] { "claimTriage", "severity", "urgency" },
            OrchestrationProfile.FraudScoring => new[] { "fraudAnalysis", "fraudProbabilityScore", "riskLevel" },
            OrchestrationProfile.CustomerExperience => new[] { "response", "tone", "customerIntent", "escalationRecommended" },
            _ => new[] { "sentiment", "confidenceScore" }
        };

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
                    if (expectedKeys.Any(k => candidate.Contains(k)) && IsValidJson(candidate))
                    {
                        lastJson = candidate;
                    }
                    searchFrom = i + 1;
                    break;
                }
            }

            if (depth != 0) break;
        }

        return lastJson;
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

    /// <summary>
    /// Profile-aware single-agent fallback. Uses the profile-specific prompt schema
    /// so FraudScoring profiles produce fraudAnalysis JSON, not generic sentiment.
    /// </summary>
    private async Task<AgentAnalysisResult> RunProfileSingleAgentFallback(
        string text, OrchestrationProfile profile, InteractionType interactionType)
    {
        // For non-specialized profiles, delegate to the generic fallback
        if (profile == OrchestrationProfile.SentimentAnalysis)
        {
            return await RunSingleAgentFallback(text, interactionType);
        }

        var userMessage = BuildProfileUserMessage(text, profile, interactionType);

        var chatService = _kernelProvider.GetKernel().GetRequiredService<IChatCompletionService>();
        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(
            "You are an expert insurance analyst. Analyze the input and return ONLY valid JSON matching the requested schema. No markdown code fences.");
        chatHistory.AddUserMessage(userMessage);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var response = await chatService.GetChatMessageContentAsync(chatHistory, cancellationToken: cts.Token);

        var json = ExtractJson(response.Content ?? "");
        if (json != null)
        {
            var result = ParseAnalysisResult(json, $"[ProfileFallback-{profile}]: {response.Content}");
            result.Quality ??= new QualityMetadata();
            result.Quality.Suggestions = [$"Analysis produced using single-agent fallback for {profile} profile"];
            return result;
        }

        return CreateDefaultResult(text, interactionType, $"Profile-aware fallback for {profile} did not produce valid JSON");
    }

    /// <summary>
    /// Redacts PII from text before sending to external AI providers.
    /// </summary>
    private string SanitizeText(string text)
    {
        if (_piiRedactor == null)
        {
            _logger.LogWarning("PII redactor not configured. Sending unredacted text to AI providers.");
            return text;
        }
        return _piiRedactor.Redact(text);
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

    internal static readonly Regex MarkdownFenceRegex = new(
        @"```(?:json|JSON)?\s*\n?",
        RegexOptions.Compiled);

    /// <summary>
    /// Strips markdown code fences and normalizes LLM output before JSON extraction.
    /// Handles ```json ... ```, ```JSON ... ```, and plain ``` ... ``` wrappers.
    /// </summary>
    internal static string NormalizeForJsonExtraction(string text)
    {
        return MarkdownFenceRegex.Replace(text, "").Trim();
    }

    /// <summary>
    /// Extracts the first valid JSON object from a text string.
    /// Strips markdown fences, then uses brace-counting to find candidates,
    /// then validates with JsonDocument.Parse.
    /// </summary>
    internal static string? ExtractJson(string text)
    {
        var normalized = NormalizeForJsonExtraction(text);
        return ExtractJsonFromNormalized(normalized);
    }

    /// <summary>
    /// Brace-counting JSON extraction from normalized (fence-stripped) text.
    /// </summary>
    internal static string? ExtractJsonFromNormalized(string text)
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
    internal static string? ExtractLastJson(string allMessages)
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
    internal static bool IsValidJson(string text)
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
                Quality = ExtractQuality(root),
                ClaimTriage = ExtractClaimTriage(root),
                FraudAnalysis = ExtractFraudAnalysis(root)
            };

            _logger.LogInformation("Agent analysis JSON parsed via manual field extraction. Sentiment={Sentiment}, Confidence={Confidence}, HasInsuranceAnalysis={HasIA}, HasClaimTriage={HasCT}, HasFraudAnalysis={HasFA}",
                result.Sentiment, result.ConfidenceScore, result.InsuranceAnalysis != null, result.ClaimTriage != null, result.FraudAnalysis != null);
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
        // LLMs may output 0-1 scale (e.g., 0.85) or 0-100 scale (e.g., 85).
        // Treat values > 0 and <= 1.0 as normalized 0-1 range (1.0 = 100%).
        if (value > 0.0 && value <= 1.0)
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

    /// <summary>
    /// Extracts ClaimTriageDetail from the root JSON element.
    /// Handles both nested "claimTriage" wrapper and flat top-level fields.
    /// </summary>
    private static ClaimTriageDetail? ExtractClaimTriage(JsonElement root)
    {
        JsonElement ct;
        if (!root.TryGetProperty("claimTriage", out ct) &&
            !root.TryGetProperty("ClaimTriage", out ct))
        {
            // Check if claim triage fields are at the top level — require claimType
            // in addition to severity+urgency to avoid false positives from sentiment results
            if (!root.TryGetProperty("severity", out _) ||
                !root.TryGetProperty("urgency", out _) ||
                !root.TryGetProperty("claimType", out _))
                return null;
            ct = root;
        }

        if (ct.ValueKind != JsonValueKind.Object) return null;

        return new ClaimTriageDetail
        {
            Severity = GetStringProp(ct, "severity", "Medium"),
            Urgency = GetStringProp(ct, "urgency", "Standard"),
            ClaimType = GetStringProp(ct, "claimType", ""),
            ClaimSubType = GetStringProp(ct, "claimSubType", ""),
            EstimatedLossRange = GetStringProp(ct, "estimatedLossRange", ""),
            PreliminaryFraudRisk = GetStringProp(ct, "preliminaryFraudRisk", "None"),
            FraudFlags = ExtractStringList(ct, "fraudFlags"),
            AdditionalNotes = GetStringProp(ct, "additionalNotes", ""),
            RecommendedActions = ExtractRecommendedActions(ct)
        };
    }

    /// <summary>
    /// Extracts FraudAnalysisDetail from the root JSON element.
    /// Handles both nested "fraudAnalysis" wrapper and flat top-level fields.
    /// </summary>
    private static FraudAnalysisDetail? ExtractFraudAnalysis(JsonElement root)
    {
        JsonElement fa;
        if (!root.TryGetProperty("fraudAnalysis", out fa) &&
            !root.TryGetProperty("FraudAnalysis", out fa))
        {
            // Check if fraud fields are at top level
            if (!root.TryGetProperty("fraudProbabilityScore", out _))
                return null;
            fa = root;
        }

        if (fa.ValueKind != JsonValueKind.Object) return null;

        return new FraudAnalysisDetail
        {
            FraudProbabilityScore = GetIntProp(fa, "fraudProbabilityScore", 0),
            RiskLevel = GetStringProp(fa, "riskLevel", "VeryLow"),
            ReferToSIU = GetBoolProp(fa, "referToSIU", false),
            SiuReferralReason = GetStringProp(fa, "siuReferralReason", ""),
            ConfidenceInAssessment = GetDoubleProp(fa, "confidenceInAssessment", 0.5),
            AdditionalNotes = GetStringProp(fa, "additionalNotes", ""),
            Indicators = ExtractFraudIndicators(fa),
            RecommendedActions = ExtractRecommendedActions(fa)
        };
    }

    /// <summary>
    /// Extracts a list of RecommendedAction from a JSON element.
    /// </summary>
    private static List<RecommendedAction> ExtractRecommendedActions(JsonElement element)
    {
        var actions = new List<RecommendedAction>();
        JsonElement arr;
        if (!element.TryGetProperty("recommendedActions", out arr) &&
            !element.TryGetProperty("RecommendedActions", out arr))
            return actions;

        if (arr.ValueKind != JsonValueKind.Array) return actions;

        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object)
            {
                actions.Add(new RecommendedAction
                {
                    Action = GetStringProp(item, "action", ""),
                    Priority = GetStringProp(item, "priority", "Standard"),
                    Reasoning = GetStringProp(item, "reasoning", "")
                });
            }
            else if (item.ValueKind == JsonValueKind.String)
            {
                var text = item.GetString() ?? "";
                if (!string.IsNullOrWhiteSpace(text))
                {
                    actions.Add(new RecommendedAction { Action = text, Priority = "Standard" });
                }
            }
        }
        return actions;
    }

    /// <summary>
    /// Extracts a list of FraudIndicator from a JSON element.
    /// </summary>
    private static List<FraudIndicator> ExtractFraudIndicators(JsonElement element)
    {
        var indicators = new List<FraudIndicator>();
        JsonElement arr;
        if (!element.TryGetProperty("indicators", out arr) &&
            !element.TryGetProperty("Indicators", out arr))
            return indicators;

        if (arr.ValueKind != JsonValueKind.Array) return indicators;

        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object)
            {
                indicators.Add(new FraudIndicator
                {
                    Category = GetStringProp(item, "category", ""),
                    Description = GetStringProp(item, "description", ""),
                    Severity = GetStringProp(item, "severity", "Low")
                });
            }
        }
        return indicators;
    }

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes)[..16];
    }

    #endregion

    internal static AgentAnalysisResult CreateDefaultResult(string text, InteractionType interactionType, string notes)
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
