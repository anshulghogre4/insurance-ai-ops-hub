using System.Text.Json;
using MediatR;
using SentimentAnalyzer.API.Features.CustomerExperience.Commands;
using SentimentAnalyzer.API.Models;
using SentimentAnalyzer.API.Services.CustomerExperience;

namespace SentimentAnalyzer.API.Endpoints;

/// <summary>
/// Minimal API endpoint mappings for the Customer Experience Copilot.
/// Provides non-streaming JSON chat and SSE streaming chat endpoints.
/// </summary>
public static class CustomerExperienceEndpoints
{
    /// <summary>
    /// Maximum allowed message length (characters).
    /// </summary>
    private const int MaxMessageLength = 5000;

    /// <summary>
    /// Maps the CX Copilot endpoints under /api/insurance/cx.
    /// </summary>
    /// <param name="app">The web application to register endpoints on.</param>
    /// <param name="requireAuth">When true, all endpoints require JWT authorization.</param>
    /// <returns>The route group builder for chaining.</returns>
    public static RouteGroupBuilder MapCustomerExperienceEndpoints(this WebApplication app, bool requireAuth = false)
    {
        var group = app.MapGroup("/api/insurance/cx")
            .WithTags("Customer Experience");

        var chatEndpoint = group.MapPost("/chat", ChatAsync)
            .WithName("CxChat")
            .WithDescription("Non-streaming CX Copilot chat. Returns a single JSON response with tone and escalation analysis.")
            .RequireRateLimiting("analyze");

        var streamEndpoint = group.MapPost("/stream", StreamChatAsync)
            .WithName("CxStream")
            .WithDescription("SSE streaming CX Copilot chat. Returns text/event-stream with content chunks and final metadata.")
            .RequireRateLimiting("analyze");

        if (requireAuth)
        {
            chatEndpoint.RequireAuthorization();
            streamEndpoint.RequireAuthorization();
        }

        return group;
    }

    /// <summary>
    /// Non-streaming chat endpoint. Validates input, delegates to MediatR ChatCommand,
    /// and returns a complete JSON response. Returns 503 if the LLM provider fails.
    /// </summary>
    private static async Task<IResult> ChatAsync(
        CustomerExperienceRequest request,
        IMediator mediator,
        CancellationToken ct)
    {
        var validationError = ValidateRequest(request);
        if (validationError != null)
            return validationError;

        var command = new ChatCommand(request.Message, request.ClaimContext);
        var result = await mediator.Send(command, ct);

        // UX-H1: Return 503 Service Unavailable when LLM provider failed
        if (string.Equals(result.LlmProvider, "Error", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Json(result, statusCode: 503);
        }

        return Results.Ok(result);
    }

    /// <summary>
    /// SSE streaming chat endpoint. Validates input, then streams response chunks
    /// using Server-Sent Events (text/event-stream) format.
    /// </summary>
    private static async Task<IResult> StreamChatAsync(
        CustomerExperienceRequest request,
        ICustomerExperienceService cxService,
        CancellationToken ct)
    {
        var validationError = ValidateRequest(request);
        if (validationError != null)
            return validationError;

        return Results.Stream(async stream =>
        {
            var writer = new StreamWriter(stream) { AutoFlush = false };
            var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

            await foreach (var chunk in cxService.StreamChatAsync(request.Message, request.ClaimContext, ct))
            {
                if (ct.IsCancellationRequested)
                    break;

                var json = JsonSerializer.Serialize(chunk, jsonOptions);
                await writer.WriteLineAsync($"data: {json}");
                await writer.WriteLineAsync(); // Empty line separates SSE events
                await writer.FlushAsync(ct);
            }

            await writer.WriteLineAsync("data: [DONE]");
            await writer.FlushAsync(ct);
        }, contentType: "text/event-stream");
    }

    /// <summary>
    /// Validates the customer experience request. Returns a BadRequest result
    /// if validation fails, or null if the request is valid.
    /// </summary>
    /// <param name="request">The request to validate.</param>
    /// <returns>A BadRequest IResult if invalid, or null if valid.</returns>
    private static IResult? ValidateRequest(CustomerExperienceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return Results.BadRequest(new { error = "Message cannot be empty.", status = 400 });
        }

        if (request.Message.Length > MaxMessageLength)
        {
            return Results.BadRequest(new { error = $"Message cannot exceed {MaxMessageLength} characters.", status = 400 });
        }

        return null;
    }
}
