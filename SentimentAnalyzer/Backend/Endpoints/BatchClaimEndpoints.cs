using SentimentAnalyzer.API.Services.Claims;

namespace SentimentAnalyzer.API.Endpoints;

/// <summary>
/// Minimal API endpoint mappings for batch CSV claim upload operations.
/// </summary>
public static class BatchClaimEndpoints
{
    /// <summary>Maximum allowed CSV file size: 5 MB.</summary>
    private const long MaxFileSizeBytes = 5 * 1024 * 1024;

    /// <summary>
    /// Maps the batch claim upload endpoint onto the application pipeline.
    /// </summary>
    public static RouteGroupBuilder MapBatchClaimEndpoints(this WebApplication app, bool requireAuth = false)
    {
        var group = app.MapGroup("/api/insurance/claims")
            .WithTags("Batch Claims");

        var batchEndpoint = group.MapPost("/batch", UploadBatchAsync)
            .WithName("BatchClaimUpload")
            .WithDescription("Upload a CSV file containing multiple claims for batch triage processing.")
            .DisableAntiforgery()
            .RequireRateLimiting("upload");

        if (requireAuth)
        {
            batchEndpoint.RequireAuthorization();
        }

        return group;
    }

    private static async Task<IResult> UploadBatchAsync(
        IFormFile file,
        IBatchClaimService batchService,
        CancellationToken ct)
    {
        // Validate file is present
        if (file is null || file.Length == 0)
        {
            return Results.BadRequest(new { error = "No file uploaded or file is empty." });
        }

        // Validate file size
        if (file.Length > MaxFileSizeBytes)
        {
            return Results.BadRequest(new { error = "File size exceeds the 5 MB limit." });
        }

        // Validate file type (CSV)
        var fileName = file.FileName ?? string.Empty;
        var isCsvContentType = file.ContentType?.Contains("csv", StringComparison.OrdinalIgnoreCase) == true
            || file.ContentType?.Contains("text/plain", StringComparison.OrdinalIgnoreCase) == true;
        var isCsvExtension = fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);

        if (!isCsvContentType && !isCsvExtension)
        {
            return Results.BadRequest(new { error = "Only CSV files are accepted. Please upload a .csv file." });
        }

        using var stream = file.OpenReadStream();
        var result = await batchService.ProcessBatchAsync(stream, ct);
        return Results.Ok(result);
    }
}
