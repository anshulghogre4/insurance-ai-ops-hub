using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using SentimentAnalyzer.Agents.Configuration;
using SentimentAnalyzer.Agents.Orchestration;

namespace SentimentAnalyzer.API.Services.Providers;

/// <summary>
/// Manages multiple Semantic Kernel instances (one per provider) and provides
/// automatic fallback when providers fail with 429/500/503 errors.
/// Implements exponential backoff cooldown per provider.
/// </summary>
public class ResilientKernelProvider : IResilientKernelProvider
{
    private readonly Dictionary<string, Kernel> _kernels = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ProviderHealthStatus> _healthStatus = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _fallbackOrder;
    private readonly ILogger<ResilientKernelProvider> _logger;
    private readonly object _lock = new();

    private static readonly TimeSpan _baseCooldown = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _maxCooldown = TimeSpan.FromSeconds(300);

    /// <summary>
    /// Initializes the resilient kernel provider by building one Kernel per configured provider.
    /// Only providers with valid API keys are included (Ollama is always included as local fallback).
    /// </summary>
    public ResilientKernelProvider(
        IOptions<AgentSystemSettings> settings,
        IConfiguration configuration,
        ILogger<ResilientKernelProvider> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var agentSettings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        var openAiKey = configuration["OpenAI:ApiKey"] ?? string.Empty;
        var openAiModel = configuration["OpenAI:Model"] ?? "gpt-4o-mini";

        _fallbackOrder = agentSettings.FallbackChain.Count > 0
            ? agentSettings.FallbackChain.ToList()
            : [agentSettings.Provider, "Ollama"];

        foreach (var providerName in _fallbackOrder)
        {
            var kernel = BuildKernel(providerName, agentSettings, openAiKey, openAiModel);
            if (kernel != null)
            {
                _kernels[providerName] = kernel;
                _healthStatus[providerName] = new ProviderHealthStatus
                {
                    ProviderName = providerName,
                    IsAvailable = true
                };
                _logger.LogInformation("Registered provider: {Provider}", providerName);
            }
            else
            {
                _logger.LogWarning("Skipped provider {Provider}: API key not configured", providerName);
            }
        }

        if (_kernels.Count == 0)
        {
            throw new InvalidOperationException(
                "No AI providers configured. At least one provider must have a valid API key. " +
                "Use 'dotnet user-secrets set \"AgentSystem:Groq:ApiKey\" \"your-key\"' to configure a provider.");
        }

        _logger.LogInformation("Resilient kernel provider initialized with {Count} providers: {Providers}",
            _kernels.Count, string.Join(" → ", _kernels.Keys));
    }

    /// <inheritdoc />
    public Kernel GetKernel()
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;

            foreach (var providerName in _fallbackOrder)
            {
                if (!_kernels.ContainsKey(providerName))
                    continue;

                var status = _healthStatus[providerName];

                // Check if cooldown has expired
                if (!status.IsAvailable && status.CooldownExpiresUtc.HasValue && now >= status.CooldownExpiresUtc.Value)
                {
                    status.IsAvailable = true;
                    _logger.LogInformation("Provider {Provider} cooldown expired, marking as available", providerName);
                }

                if (status.IsAvailable)
                {
                    return _kernels[providerName];
                }
            }

            // All providers in cooldown — return the last one (Ollama is typically last and always local)
            var lastProvider = _fallbackOrder.LastOrDefault(p => _kernels.ContainsKey(p));
            if (lastProvider != null)
            {
                _logger.LogWarning("All providers in cooldown. Forcing use of {Provider}", lastProvider);
                return _kernels[lastProvider];
            }

            // Should never happen given constructor validation, but be safe
            throw new InvalidOperationException("No AI providers available.");
        }
    }

    /// <inheritdoc />
    public void ReportFailure(string providerName, Exception ex)
    {
        lock (_lock)
        {
            if (!_healthStatus.TryGetValue(providerName, out var status))
                return;

            status.ConsecutiveFailures++;
            status.LastFailureUtc = DateTime.UtcNow;
            status.IsAvailable = false;

            // Exponential backoff: 30s, 60s, 120s, 240s, capped at 300s
            var backoffMultiplier = Math.Min(Math.Pow(2, status.ConsecutiveFailures - 1), _maxCooldown.TotalSeconds / _baseCooldown.TotalSeconds);
            status.CooldownDuration = TimeSpan.FromSeconds(Math.Min(_baseCooldown.TotalSeconds * backoffMultiplier, _maxCooldown.TotalSeconds));
            status.CooldownExpiresUtc = DateTime.UtcNow + status.CooldownDuration;

            _logger.LogWarning(
                "Provider {Provider} failed (attempt #{Failures}). Cooldown: {Cooldown}s. Error type: {ErrorType}",
                providerName, status.ConsecutiveFailures, status.CooldownDuration.TotalSeconds, ex.GetType().Name);
        }
    }

    /// <inheritdoc />
    public string ActiveProviderName
    {
        get
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                foreach (var providerName in _fallbackOrder)
                {
                    if (!_kernels.ContainsKey(providerName))
                        continue;

                    var status = _healthStatus[providerName];
                    if (status.IsAvailable || (status.CooldownExpiresUtc.HasValue && now >= status.CooldownExpiresUtc.Value))
                        return providerName;
                }

                return _fallbackOrder.LastOrDefault(p => _kernels.ContainsKey(p)) ?? "Unknown";
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, ProviderHealthStatus> GetHealthStatus()
    {
        lock (_lock)
        {
            // Return a snapshot to avoid concurrency issues
            return _healthStatus.ToDictionary(
                kvp => kvp.Key,
                kvp => new ProviderHealthStatus
                {
                    ProviderName = kvp.Value.ProviderName,
                    IsAvailable = kvp.Value.IsAvailable,
                    LastFailureUtc = kvp.Value.LastFailureUtc,
                    ConsecutiveFailures = kvp.Value.ConsecutiveFailures,
                    CooldownDuration = kvp.Value.CooldownDuration,
                    CooldownExpiresUtc = kvp.Value.CooldownExpiresUtc
                },
                StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Builds a Semantic Kernel for the specified provider using OpenAI-compatible endpoints.
    /// Returns null if the provider is not configured (missing API key).
    /// </summary>
    private Kernel? BuildKernel(string providerName, AgentSystemSettings settings, string openAiKey, string openAiModel)
    {
        try
        {
            var kernelBuilder = Kernel.CreateBuilder();

            switch (providerName)
            {
                case "Groq":
                    if (string.IsNullOrWhiteSpace(settings.Groq.ApiKey)) return null;
                    kernelBuilder.AddOpenAIChatCompletion(
                        modelId: settings.Groq.Model,
                        apiKey: settings.Groq.ApiKey,
                        endpoint: new Uri(settings.Groq.Endpoint));
                    break;

                case "Mistral":
                    if (string.IsNullOrWhiteSpace(settings.Mistral.ApiKey)) return null;
                    kernelBuilder.AddOpenAIChatCompletion(
                        modelId: settings.Mistral.Model,
                        apiKey: settings.Mistral.ApiKey,
                        endpoint: new Uri(settings.Mistral.Endpoint));
                    break;

                case "Gemini":
                    if (string.IsNullOrWhiteSpace(settings.Gemini.ApiKey)) return null;
                    kernelBuilder.AddOpenAIChatCompletion(
                        modelId: settings.Gemini.Model,
                        apiKey: settings.Gemini.ApiKey,
                        endpoint: new Uri("https://generativelanguage.googleapis.com/v1beta/openai/"));
                    break;

                case "OpenRouter":
                    if (string.IsNullOrWhiteSpace(settings.OpenRouter.ApiKey)) return null;
                    var openRouterHandler = new OpenRouterHeaderHandler(settings.OpenRouter.SiteUrl)
                    {
                        InnerHandler = new HttpClientHandler()
                    };
                    var openRouterHttpClient = new HttpClient(openRouterHandler);
                    kernelBuilder.AddOpenAIChatCompletion(
                        modelId: settings.OpenRouter.Model,
                        apiKey: settings.OpenRouter.ApiKey,
                        endpoint: new Uri(settings.OpenRouter.Endpoint),
                        httpClient: openRouterHttpClient);
                    break;

                case "Ollama":
                    // Ollama is local — always available, no API key needed
                    kernelBuilder.AddOpenAIChatCompletion(
                        modelId: settings.Ollama.Model,
                        apiKey: "ollama",
                        endpoint: new Uri(settings.Ollama.Endpoint + "/v1"));
                    break;

                case "OpenAI":
                    if (string.IsNullOrWhiteSpace(openAiKey)) return null;
                    kernelBuilder.AddOpenAIChatCompletion(
                        modelId: openAiModel,
                        apiKey: openAiKey);
                    break;

                default:
                    _logger.LogWarning("Unknown provider in fallback chain: {Provider}", providerName);
                    return null;
            }

            return kernelBuilder.Build();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build kernel for provider: {Provider}", providerName);
            return null;
        }
    }
}

/// <summary>
/// Delegating handler that injects the required HTTP-Referer and X-Title headers
/// for OpenRouter API requests.
/// </summary>
internal class OpenRouterHeaderHandler : DelegatingHandler
{
    private readonly string _siteUrl;

    public OpenRouterHeaderHandler(string siteUrl)
    {
        _siteUrl = siteUrl;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.TryAddWithoutValidation("HTTP-Referer", _siteUrl);
        request.Headers.TryAddWithoutValidation("X-Title", "InsureAI Hub");
        return base.SendAsync(request, cancellationToken);
    }
}
