using MediatR;
using SentimentAnalyzer.API.Features.Insurance.Commands;
using SentimentAnalyzer.API.Features.Insurance.Queries;
using SentimentAnalyzer.API.Models;

namespace SentimentAnalyzer.API.Endpoints;

public static class InsuranceEndpoints
{
    private static readonly string[] ValidInteractionTypes =
        ["General", "Email", "Call", "Chat", "Review", "Complaint"];

    public static RouteGroupBuilder MapInsuranceEndpoints(this WebApplication app, bool requireAuth = false)
    {
        var group = app.MapGroup("/api/insurance")
            .WithTags("Insurance Analysis");

        var analyzeEndpoint = group.MapPost("/analyze", AnalyzeAsync)
            .WithName("AnalyzeInsurance")
            .WithDescription("Analyzes customer interaction text for insurance-specific insights using multi-agent system.");

        var dashboardEndpoint = group.MapGet("/dashboard", GetDashboardAsync)
            .WithName("GetDashboard")
            .WithDescription("Returns aggregated dashboard data: metrics, sentiment distribution, top personas.");

        var historyEndpoint = group.MapGet("/history", GetHistoryAsync)
            .WithName("GetHistory")
            .WithDescription("Returns recent analysis history.");

        var getByIdEndpoint = group.MapGet("/{id:int}", GetByIdAsync)
            .WithName("GetAnalysisById")
            .WithDescription("Returns a single analysis by its ID.");

        group.MapGet("/health", () => Results.Ok(new
        {
            status = "healthy",
            service = "Insurance Analysis v2",
            agentSystem = "Semantic Kernel Multi-Agent",
            timestamp = DateTime.UtcNow
        }))
            .WithName("InsuranceHealth")
            .WithDescription("Health check endpoint for the insurance analysis service.")
            .AllowAnonymous();

        if (requireAuth)
        {
            analyzeEndpoint.RequireAuthorization();
            dashboardEndpoint.RequireAuthorization();
            historyEndpoint.RequireAuthorization();
            getByIdEndpoint.RequireAuthorization();
        }

        return group;
    }

    private static async Task<IResult> AnalyzeAsync(
        InsuranceAnalysisRequest request,
        IMediator mediator)
    {
        // Input validation
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return Results.BadRequest(new { error = "Text cannot be empty." });
        }

        if (request.Text.Length > 10000)
        {
            return Results.BadRequest(new { error = "Text cannot exceed 10,000 characters." });
        }

        if (!string.IsNullOrEmpty(request.InteractionType) &&
            !ValidInteractionTypes.Contains(request.InteractionType, StringComparer.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new { error = $"Invalid interaction type. Must be one of: {string.Join(", ", ValidInteractionTypes)}" });
        }

        var command = new AnalyzeInsuranceCommand(
            request.Text,
            request.InteractionType,
            request.CustomerId);

        var result = await mediator.Send(command);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetDashboardAsync(IMediator mediator)
    {
        var result = await mediator.Send(new GetDashboardQuery());
        return Results.Ok(result);
    }

    private static async Task<IResult> GetHistoryAsync(
        IMediator mediator,
        int count = 20)
    {
        var result = await mediator.Send(new GetHistoryQuery(count));
        return Results.Ok(result);
    }

    private static async Task<IResult> GetByIdAsync(
        int id,
        IMediator mediator)
    {
        var result = await mediator.Send(new GetAnalysisByIdQuery(id));
        return result is null ? Results.NotFound(new { error = "Analysis not found." }) : Results.Ok(result);
    }
}
