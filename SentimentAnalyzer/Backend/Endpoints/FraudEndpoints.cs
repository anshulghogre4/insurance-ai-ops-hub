using MediatR;
using SentimentAnalyzer.API.Features.Fraud.Commands;
using SentimentAnalyzer.API.Features.Fraud.Queries;
using SentimentAnalyzer.API.Models;

namespace SentimentAnalyzer.API.Endpoints;

/// <summary>
/// Minimal API endpoint mappings for the fraud analysis pipeline.
/// Includes fraud scoring, alerting, cross-claim correlation analysis,
/// correlation review (confirm/dismiss), and correlation deletion.
/// </summary>
public static class FraudEndpoints
{
    /// <summary>Valid review statuses for correlation review endpoint.</summary>
    private static readonly HashSet<string> ValidReviewStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Pending", "Confirmed", "Dismissed"
    };

    /// <summary>
    /// Maps all fraud-related endpoints to the application's routing table.
    /// </summary>
    /// <param name="app">The web application instance.</param>
    /// <param name="requireAuth">Whether to require authorization on all endpoints.</param>
    /// <returns>The configured route group builder.</returns>
    public static RouteGroupBuilder MapFraudEndpoints(this WebApplication app, bool requireAuth = false)
    {
        var group = app.MapGroup("/api/insurance/fraud")
            .WithTags("Fraud Analysis");

        var analyzeEndpoint = group.MapPost("/analyze", AnalyzeFraudAsync)
            .WithName("AnalyzeFraud")
            .WithDescription("Run detailed fraud analysis on an existing claim.")
            .RequireRateLimiting("fraud");

        var scoreEndpoint = group.MapGet("/score/{claimId:int}", GetFraudScoreAsync)
            .WithName("GetFraudScore")
            .WithDescription("Get fraud score and risk level for a claim.");

        var alertsEndpoint = group.MapGet("/alerts", GetFraudAlertsAsync)
            .WithName("GetFraudAlerts")
            .WithDescription("Get claims flagged as potential fraud alerts.");

        var correlateEndpoint = group.MapPost("/correlate", AnalyzeFraudCorrelationAsync)
            .WithName("AnalyzeFraudCorrelation")
            .WithDescription("Analyze cross-claim fraud patterns for a claim.")
            .RequireRateLimiting("fraud");

        var getCorrelationsEndpoint = group.MapGet("/correlations/{claimId:int}", GetFraudCorrelationsAsync)
            .WithName("GetFraudCorrelations")
            .WithDescription("Get discovered fraud correlations for a claim (paginated).");

        var deleteCorrelationsEndpoint = group.MapDelete("/correlations/{claimId:int}", DeleteFraudCorrelationsAsync)
            .WithName("DeleteFraudCorrelations")
            .WithDescription("Delete all fraud correlations for a specific claim.");

        var reviewCorrelationEndpoint = group.MapMethods("/correlations/{id:int}/review", ["PATCH"], ReviewFraudCorrelationAsync)
            .WithName("ReviewFraudCorrelation")
            .WithDescription("Review (confirm/dismiss) a fraud correlation.");

        if (requireAuth)
        {
            analyzeEndpoint.RequireAuthorization();
            scoreEndpoint.RequireAuthorization();
            alertsEndpoint.RequireAuthorization();
            correlateEndpoint.RequireAuthorization();
            getCorrelationsEndpoint.RequireAuthorization();
            deleteCorrelationsEndpoint.RequireAuthorization();
            reviewCorrelationEndpoint.RequireAuthorization();
        }

        return group;
    }

    private static async Task<IResult> AnalyzeFraudAsync(
        AnalyzeFraudRequest request,
        IMediator mediator)
    {
        if (request.ClaimId <= 0)
        {
            return Results.BadRequest(new { error = "Valid claim ID is required." });
        }

        try
        {
            var result = await mediator.Send(new AnalyzeFraudCommand(request.ClaimId));
            return Results.Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
    }

    private static async Task<IResult> GetFraudScoreAsync(
        int claimId,
        IMediator mediator)
    {
        var result = await mediator.Send(new GetFraudScoreQuery(claimId));
        return result is null
            ? Results.NotFound(new { error = "Claim not found." })
            : Results.Ok(result);
    }

    private static async Task<IResult> GetFraudAlertsAsync(
        IMediator mediator,
        double minScore = 55,
        int pageSize = 50)
    {
        var result = await mediator.Send(new GetFraudAlertsQuery(minScore, pageSize));
        return Results.Ok(result);
    }

    private static async Task<IResult> AnalyzeFraudCorrelationAsync(
        AnalyzeFraudRequest request,
        IMediator mediator)
    {
        if (request.ClaimId <= 0)
            return Results.BadRequest(new { error = "Valid claim ID is required.", status = 400 });

        try
        {
            var result = await mediator.Send(new AnalyzeFraudCorrelationCommand(request.ClaimId));
            return Results.Ok(new { claimId = request.ClaimId, correlations = result, count = result.Count });
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(new { error = ex.Message, status = 404 });
        }
    }

    private static async Task<IResult> GetFraudCorrelationsAsync(
        int claimId,
        IMediator mediator,
        int page = 1,
        int pageSize = 20)
    {
        var result = await mediator.Send(new GetFraudCorrelationsQuery(claimId, page, pageSize));
        return Results.Ok(result);
    }

    /// <summary>
    /// Deletes all fraud correlations for a specific claim. Returns 204 No Content on success.
    /// </summary>
    private static async Task<IResult> DeleteFraudCorrelationsAsync(
        int claimId,
        IMediator mediator)
    {
        if (claimId <= 0)
            return Results.BadRequest(new { error = "Valid claim ID is required." });

        await mediator.Send(new DeleteFraudCorrelationsCommand(claimId));
        return Results.NoContent();
    }

    /// <summary>
    /// Reviews (confirms/dismisses) a fraud correlation. Validates that the status is one of:
    /// Pending, Confirmed, or Dismissed. DismissalReason is required when status is Dismissed.
    /// </summary>
    private static async Task<IResult> ReviewFraudCorrelationAsync(
        int id,
        ReviewCorrelationRequest request,
        IMediator mediator)
    {
        if (id <= 0)
            return Results.BadRequest(new { error = "Valid correlation ID is required." });

        if (!ValidReviewStatuses.Contains(request.Status))
        {
            return Results.BadRequest(new
            {
                error = $"Invalid status '{request.Status}'. Must be one of: Pending, Confirmed, Dismissed."
            });
        }

        if (string.Equals(request.Status, "Dismissed", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(request.DismissalReason))
        {
            return Results.BadRequest(new { error = "DismissalReason is required when status is 'Dismissed'." });
        }

        try
        {
            var success = await mediator.Send(new ReviewFraudCorrelationCommand(
                id, request.Status, request.ReviewedBy, request.DismissalReason));

            return success
                ? Results.Ok(new { id, status = request.Status, message = "Correlation review saved." })
                : Results.NotFound(new { error = $"Fraud correlation {id} not found." });
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
    }
}

/// <summary>
/// Request body for the fraud analysis endpoint.
/// </summary>
public class AnalyzeFraudRequest
{
    /// <summary>ID of the claim to analyze for fraud.</summary>
    public int ClaimId { get; set; }
}
