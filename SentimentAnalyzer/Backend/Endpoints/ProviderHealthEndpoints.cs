using MediatR;
using SentimentAnalyzer.API.Features.Health.Queries;

namespace SentimentAnalyzer.API.Endpoints;

/// <summary>
/// Minimal API endpoint mappings for provider health monitoring.
/// </summary>
public static class ProviderHealthEndpoints
{
    public static RouteGroupBuilder MapProviderHealthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/insurance/health")
            .WithTags("Provider Health");

        // AllowAnonymous by design: health endpoints must be reachable by monitoring
        // tools, load balancers, and the frontend status widget without auth tokens.
        group.MapGet("/providers", GetProviderHealthAsync)
            .WithName("GetProviderHealth")
            .WithDescription("Get health status of all LLM providers and multimodal services.")
            .AllowAnonymous();

        return group;
    }

    private static async Task<IResult> GetProviderHealthAsync(IMediator mediator)
    {
        var result = await mediator.Send(new GetProviderHealthQuery());
        return Results.Ok(result);
    }
}
