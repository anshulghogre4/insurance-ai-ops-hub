using MediatR;
using SentimentAnalyzer.API.Models;
using SentimentAnalyzer.API.Services.CustomerExperience;

namespace SentimentAnalyzer.API.Features.CustomerExperience.Commands;

/// <summary>
/// MediatR command for a single-turn CX Copilot chat interaction.
/// Uses the CustomerExperience orchestration profile with direct kernel access for low latency.
/// </summary>
/// <param name="Message">The customer's message or question.</param>
/// <param name="ClaimContext">Optional claim/policy context to ground the response.</param>
public record ChatCommand(string Message, string? ClaimContext = null) : IRequest<CustomerExperienceResponse>;

/// <summary>
/// Handler that delegates to the Customer Experience service for single-turn chat.
/// </summary>
public class ChatCommandHandler : IRequestHandler<ChatCommand, CustomerExperienceResponse>
{
    private readonly ICustomerExperienceService _cxService;
    private readonly ILogger<ChatCommandHandler> _logger;

    /// <summary>
    /// Initializes the chat command handler.
    /// </summary>
    /// <param name="cxService">Customer experience service for AI-powered chat.</param>
    /// <param name="logger">Structured logger for this handler.</param>
    public ChatCommandHandler(ICustomerExperienceService cxService, ILogger<ChatCommandHandler> logger)
    {
        _cxService = cxService ?? throw new ArgumentNullException(nameof(cxService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Handles the chat command by delegating to the CX service.
    /// </summary>
    /// <param name="command">The chat command containing message and optional context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The customer experience response with tone and escalation metadata.</returns>
    public async Task<CustomerExperienceResponse> Handle(ChatCommand command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("CX ChatCommand: {MessageLength} chars, hasContext={HasContext}",
            command.Message.Length, command.ClaimContext != null);

        var result = await _cxService.ChatAsync(command.Message, command.ClaimContext, cancellationToken);

        _logger.LogInformation("CX ChatCommand completed: provider={Provider}, tone={Tone}, escalation={Escalation}, elapsed={Elapsed}ms",
            result.LlmProvider, result.Tone, result.EscalationRecommended, result.ElapsedMilliseconds);

        return result;
    }
}
