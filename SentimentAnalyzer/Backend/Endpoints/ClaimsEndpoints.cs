using MediatR;
using SentimentAnalyzer.API.Features.Claims.Commands;
using SentimentAnalyzer.API.Features.Claims.Queries;
using SentimentAnalyzer.API.Models;

namespace SentimentAnalyzer.API.Endpoints;

/// <summary>
/// Minimal API endpoint mappings for the claims triage pipeline.
/// </summary>
public static class ClaimsEndpoints
{
    public static RouteGroupBuilder MapClaimsEndpoints(this WebApplication app, bool requireAuth = false)
    {
        var group = app.MapGroup("/api/insurance/claims")
            .WithTags("Claims Triage");

        var triageEndpoint = group.MapPost("/triage", TriageClaimAsync)
            .WithName("TriageClaim")
            .WithDescription("Submit a claim for AI-powered triage assessment.")
            .RequireRateLimiting("triage");

        var uploadEndpoint = group.MapPost("/upload", UploadEvidenceAsync)
            .WithName("UploadClaimEvidence")
            .WithDescription("Upload multimodal evidence (image/audio/PDF) for a claim.")
            .DisableAntiforgery()
            .RequireRateLimiting("upload");

        var getByIdEndpoint = group.MapGet("/{id:int}", GetClaimByIdAsync)
            .WithName("GetClaimById")
            .WithDescription("Retrieve a triaged claim by its ID.");

        var historyEndpoint = group.MapGet("/history", GetClaimsHistoryAsync)
            .WithName("GetClaimsHistory")
            .WithDescription("Retrieve claims history with optional filters.");

        if (requireAuth)
        {
            triageEndpoint.RequireAuthorization();
            uploadEndpoint.RequireAuthorization();
            getByIdEndpoint.RequireAuthorization();
            historyEndpoint.RequireAuthorization();
        }

        return group;
    }

    private static async Task<IResult> TriageClaimAsync(
        ClaimTriageRequest request,
        IMediator mediator)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return Results.BadRequest(new { error = "Claim text cannot be empty." });
        }

        if (request.Text.Length > 10000)
        {
            return Results.BadRequest(new { error = "Claim text cannot exceed 10,000 characters." });
        }

        var command = new TriageClaimCommand(request.Text, request.InteractionType);
        var result = await mediator.Send(command);
        return Results.Ok(result);
    }

    private static async Task<IResult> UploadEvidenceAsync(
        IFormFile file,
        int claimId,
        IMediator mediator)
    {
        if (file.Length == 0)
        {
            return Results.BadRequest(new { error = "File is empty." });
        }

        if (file.Length > 10 * 1024 * 1024) // 10 MB limit
        {
            return Results.BadRequest(new { error = "File size cannot exceed 10 MB." });
        }

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);

        var command = new UploadClaimEvidenceCommand(
            claimId, ms.ToArray(), file.ContentType, file.FileName);

        var result = await mediator.Send(command);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetClaimByIdAsync(
        int id,
        IMediator mediator)
    {
        var result = await mediator.Send(new GetClaimQuery(id));
        return result is null
            ? Results.NotFound(new { error = "Claim not found." })
            : Results.Ok(result);
    }

    private static async Task<IResult> GetClaimsHistoryAsync(
        IMediator mediator,
        string? severity = null,
        string? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int pageSize = 20,
        int page = 1)
    {
        var result = await mediator.Send(new GetClaimsHistoryQuery(severity, status, fromDate, toDate, pageSize, page));
        return Results.Ok(result);
    }
}
