using Microsoft.EntityFrameworkCore;
using SentimentAnalyzer.Agents.Configuration;
using SentimentAnalyzer.API.Data;
using Microsoft.Extensions.Options;

namespace SentimentAnalyzer.API.Endpoints;

/// <summary>
/// Health check endpoints for liveness and readiness probes.
/// Used by Docker, Kubernetes, and load balancers.
/// </summary>
public static class HealthEndpoints
{
    public static WebApplication MapHealthEndpoints(this WebApplication app)
    {
        // Liveness probe — confirms the process is running
        app.MapGet("/health", () => Results.Ok(new { status = "Healthy", timestamp = DateTime.UtcNow }))
            .WithName("HealthLiveness")
            .WithDescription("Liveness probe — confirms the process is running.")
            .WithTags("Health")
            .AllowAnonymous();

        // Readiness probe — checks DB connectivity and at least 1 LLM provider
        app.MapGet("/health/ready", async (
            InsuranceAnalysisDbContext db,
            IOptions<AgentSystemSettings> settings,
            ILogger<Program> logger) =>
        {
            var checks = new Dictionary<string, object>();
            var isReady = true;

            // Check 1: Database connectivity
            try
            {
                var canConnect = await db.Database.CanConnectAsync();
                checks["database"] = canConnect ? "Connected" : "Unreachable";
                if (!canConnect) isReady = false;
            }
            catch (Exception ex)
            {
                checks["database"] = $"Error: {ex.Message}";
                isReady = false;
                logger.LogWarning(ex, "Health readiness: database check failed");
            }

            // Check 2: At least 1 LLM provider configured
            var config = settings.Value;
            var llmProviders = new List<string>();
            if (!string.IsNullOrWhiteSpace(config.Groq.ApiKey)) llmProviders.Add("Groq");
            if (!string.IsNullOrWhiteSpace(config.Cerebras.ApiKey)) llmProviders.Add("Cerebras");
            if (!string.IsNullOrWhiteSpace(config.Mistral.ApiKey)) llmProviders.Add("Mistral");
            if (!string.IsNullOrWhiteSpace(config.Gemini.ApiKey)) llmProviders.Add("Gemini");
            if (!string.IsNullOrWhiteSpace(config.OpenRouter.ApiKey)) llmProviders.Add("OpenRouter");
            llmProviders.Add("Ollama"); // Always available

            checks["llmProviders"] = llmProviders;
            checks["llmProviderCount"] = llmProviders.Count;

            // Check 3: Multimodal services status
            var multimodal = new Dictionary<string, string>();
            multimodal["ner"] = !string.IsNullOrWhiteSpace(config.HuggingFace.ApiKey) ? "HuggingFace" :
                                !string.IsNullOrWhiteSpace(config.AzureLanguage.ApiKey) ? "AzureLanguage" : "Unconfigured";
            multimodal["contentSafety"] = !string.IsNullOrWhiteSpace(config.AzureContentSafety.ApiKey) ? "AzureContentSafety" : "Unconfigured";
            multimodal["stt"] = !string.IsNullOrWhiteSpace(config.Deepgram.ApiKey) ? "Deepgram" :
                                !string.IsNullOrWhiteSpace(config.AzureSpeech.ApiKey) ? "AzureSpeech" : "Unconfigured";
            multimodal["translation"] = !string.IsNullOrWhiteSpace(config.AzureTranslator.ApiKey) ? "AzureTranslator" : "Unconfigured";
            multimodal["ocr"] = "PdfPig"; // Always available (local)
            checks["multimodal"] = multimodal;

            var result = new
            {
                status = isReady ? "Ready" : "NotReady",
                timestamp = DateTime.UtcNow,
                checks
            };

            return isReady ? Results.Ok(result) : Results.Json(result, statusCode: 503);
        })
        .WithName("HealthReadiness")
        .WithDescription("Readiness probe — checks DB connectivity and provider configuration.")
        .WithTags("Health")
        .AllowAnonymous();

        return app;
    }
}
