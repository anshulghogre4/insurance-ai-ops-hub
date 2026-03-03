using System.IO;
using MediatR;
using SentimentAnalyzer.API.Features.Documents.Commands;
using SentimentAnalyzer.API.Features.Documents.Queries;
using SentimentAnalyzer.API.Models;
using SentimentAnalyzer.API.Services.Documents;

namespace SentimentAnalyzer.API.Endpoints;

/// <summary>
/// Minimal API endpoint mappings for the Document Intelligence (RAG) pipeline.
/// </summary>
public static class DocumentEndpoints
{
    private static readonly string[] ValidCategories =
        ["Policy", "Claim", "Endorsement", "Correspondence", "Other"];

    public static RouteGroupBuilder MapDocumentEndpoints(this WebApplication app, bool requireAuth = false)
    {
        var group = app.MapGroup("/api/insurance/documents")
            .WithTags("Document Intelligence");

        var uploadEndpoint = group.MapPost("/upload", UploadDocumentAsync)
            .WithName("UploadDocument")
            .WithDescription("Upload and process an insurance document through the RAG pipeline.")
            .DisableAntiforgery()
            .RequireRateLimiting("upload");

        var streamUploadEndpoint = group.MapPost("/upload/stream", StreamUploadDocumentAsync)
            .WithName("StreamUploadDocument")
            .WithDescription("Upload and process a document with real-time SSE progress events.")
            .DisableAntiforgery()
            .RequireRateLimiting("upload");

        var queryEndpoint = group.MapPost("/query", QueryDocumentAsync)
            .WithName("QueryDocument")
            .WithDescription("Query indexed documents using natural language (RAG Q&A).")
            .RequireRateLimiting("analyze");

        var getByIdEndpoint = group.MapGet("/{id:int}", GetDocumentByIdAsync)
            .WithName("GetDocumentById")
            .WithDescription("Retrieve a document's metadata and chunk details by ID.");

        var historyEndpoint = group.MapGet("/history", GetDocumentHistoryAsync)
            .WithName("GetDocumentHistory")
            .WithDescription("List indexed documents with optional category filter.");

        var deleteEndpoint = group.MapDelete("/{id:int}", DeleteDocumentAsync)
            .WithName("DeleteDocument")
            .WithDescription("Delete a document and its indexed chunks.");

        var generateQaEndpoint = group.MapPost("/{id:int}/generate-qa", async (int id, ISyntheticQAService qaService, CancellationToken ct) =>
        {
            var result = await qaService.GenerateQAPairsAsync(id, ct);
            // Return 200 even on partial success (some pairs generated despite some chunk failures)
            return result.TotalPairsGenerated > 0 || result.ErrorMessage == null
                ? Results.Ok(result)
                : Results.BadRequest(result);
        }).WithName("GenerateQAPairs")
          .WithDescription("Generate synthetic Q&A pairs from a document's chunks for fine-tuning preparation.")
          .WithOpenApi();

        var getQaPairsEndpoint = group.MapGet("/{id:int}/qa-pairs", async (int id, ISyntheticQAService qaService) =>
        {
            var result = await qaService.GetQAPairsAsync(id);
            return result.ErrorMessage != null ? Results.NotFound(result) : Results.Ok(result);
        }).WithName("GetQAPairs")
          .WithDescription("Retrieve previously generated synthetic Q&A pairs for a document.")
          .WithOpenApi();

        if (requireAuth)
        {
            uploadEndpoint.RequireAuthorization();
            streamUploadEndpoint.RequireAuthorization();
            queryEndpoint.RequireAuthorization();
            getByIdEndpoint.RequireAuthorization();
            historyEndpoint.RequireAuthorization();
            deleteEndpoint.RequireAuthorization();
            generateQaEndpoint.RequireAuthorization();
            getQaPairsEndpoint.RequireAuthorization();
        }

        return group;
    }

    private static async Task<IResult> UploadDocumentAsync(
        IFormFile file, IMediator mediator, IConfiguration config, string category = "Other")
    {
        // Free-tier document limits (configurable via appsettings.json)
        var maxDocs = config.GetValue("DocumentLimits:MaxDocuments", 20);
        var maxFileSize = config.GetValue("DocumentLimits:MaxFileSizeBytes", 5 * 1024 * 1024);
        var maxChunksTotal = config.GetValue("DocumentLimits:MaxChunksTotal", 500);
        var maxPages = config.GetValue("DocumentLimits:MaxPagesPerDocument", 20);

        if (file.Length == 0)
            return Results.BadRequest(new { error = "File is empty.", status = 400 });

        if (file.Length > maxFileSize)
            return Results.BadRequest(new { error = $"File size cannot exceed {maxFileSize / (1024 * 1024)} MB (free-tier limit).", status = 400 });

        var allowedTypes = new[] { "application/pdf", "image/png", "image/jpeg", "image/tiff" };
        if (!allowedTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            return Results.BadRequest(new { error = $"Unsupported file type. Allowed: {string.Join(", ", allowedTypes)}", status = 400 });

        if (!ValidCategories.Contains(category, StringComparer.OrdinalIgnoreCase))
            return Results.BadRequest(new { error = $"Invalid category. Must be one of: {string.Join(", ", ValidCategories)}", status = 400 });

        // Free-tier document count limit — prevents OCR/embedding quota exhaustion
        var existingCount = await mediator.Send(new GetDocumentHistoryQuery(null, 1, 1));
        if (existingCount.TotalCount >= maxDocs)
            return Results.Json(new
            {
                error = $"Document limit reached ({maxDocs} documents). Delete existing documents or upgrade plan.",
                status = 429,
                currentCount = existingCount.TotalCount,
                maxDocuments = maxDocs
            }, statusCode: 429);

        // Free-tier total chunk limit — prevents embedding API quota exhaustion
        var allDocs = await mediator.Send(new GetDocumentHistoryQuery(null, maxDocs, 1));
        var currentChunkTotal = allDocs.Items.Sum(d => d.ChunkCount);
        if (currentChunkTotal >= maxChunksTotal)
            return Results.Json(new
            {
                error = $"Total chunk limit reached ({maxChunksTotal} chunks). Delete existing documents to free embedding quota.",
                status = 429,
                currentChunks = currentChunkTotal,
                maxChunks = maxChunksTotal
            }, statusCode: 429);

        // Sanitize filename: strip path separators, limit length, remove control characters
        var sanitizedFileName = SanitizeFileName(file.FileName);

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);

        var command = new UploadDocumentCommand(ms.ToArray(), file.ContentType, sanitizedFileName, category);
        var result = await mediator.Send(command);

        if (result.Status == "Failed")
            return Results.UnprocessableEntity(new { error = result.ErrorMessage ?? "Document processing failed.", status = 422, result });

        // Enforce MaxPagesPerDocument: reject documents with too many pages (after OCR)
        if (result.PageCount > maxPages)
            return Results.Json(new
            {
                error = $"Document exceeds {maxPages}-page limit (free tier). Detected {result.PageCount} pages.",
                status = 422,
                pageCount = result.PageCount,
                maxPages
            }, statusCode: 422);

        return Results.Created($"/api/insurance/documents/{result.DocumentId}", result);
    }

    private static async Task<IResult> StreamUploadDocumentAsync(
        IFormFile file, IDocumentIntelligenceService documentService, IConfiguration config, string category = "Other")
    {
        // Same validation as UploadDocumentAsync
        var maxFileSize = config.GetValue("DocumentLimits:MaxFileSizeBytes", 5 * 1024 * 1024);
        var maxPages = config.GetValue("DocumentLimits:MaxPagesPerDocument", 20);

        if (file.Length == 0)
            return Results.BadRequest(new { error = "File is empty.", status = 400 });

        if (file.Length > maxFileSize)
            return Results.BadRequest(new { error = $"File size cannot exceed {maxFileSize / (1024 * 1024)} MB.", status = 400 });

        var allowedTypes = new[] { "application/pdf", "image/png", "image/jpeg", "image/tiff" };
        if (!allowedTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            return Results.BadRequest(new { error = $"Unsupported file type. Allowed: {string.Join(", ", allowedTypes)}", status = 400 });

        if (!ValidCategories.Contains(category, StringComparer.OrdinalIgnoreCase))
            return Results.BadRequest(new { error = $"Invalid category.", status = 400 });

        var sanitizedFileName = SanitizeFileName(file.FileName);

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var fileData = ms.ToArray();

        return Results.Stream(async stream =>
        {
            var writer = new StreamWriter(stream);
            try
            {
                await foreach (var progressEvent in documentService.UploadWithProgressAsync(
                    fileData, file.ContentType, sanitizedFileName, category))
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(progressEvent,
                        new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
                    await writer.WriteAsync($"data: {json}\n\n");
                    await writer.FlushAsync();
                }
                await writer.WriteAsync("data: [DONE]\n\n");
                await writer.FlushAsync();
            }
            catch (Exception ex)
            {
                var errorEvent = new DocumentProgressEvent
                {
                    Phase = "Error", Progress = 0,
                    Message = "Processing failed.", ErrorMessage = ex.Message
                };
                var errorJson = System.Text.Json.JsonSerializer.Serialize(errorEvent,
                    new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
                await writer.WriteAsync($"data: {errorJson}\n\n");
                await writer.WriteAsync("data: [DONE]\n\n");
                await writer.FlushAsync();
            }
        }, contentType: "text/event-stream");
    }

    private static async Task<IResult> QueryDocumentAsync(
        DocumentQueryRequest request, IMediator mediator)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return Results.BadRequest(new { error = "Question cannot be empty.", status = 400 });

        if (request.Question.Length > 2000)
            return Results.BadRequest(new { error = "Question cannot exceed 2,000 characters.", status = 400 });

        var command = new QueryDocumentCommand(request.Question, request.DocumentId);
        var result = await mediator.Send(command);

        // Detect service-level failures encoded in the result
        if (result.Confidence == 0 && result.Answer.StartsWith("Unable to", StringComparison.Ordinal))
            return Results.Json(new { error = result.Answer, status = 503, result }, statusCode: 503);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetDocumentByIdAsync(int id, IMediator mediator)
    {
        var result = await mediator.Send(new GetDocumentQuery(id));
        return result is null
            ? Results.NotFound(new { error = "Document not found.", status = 404 })
            : Results.Ok(result);
    }

    private static async Task<IResult> GetDocumentHistoryAsync(
        IMediator mediator, string? category = null, int pageSize = 20, int page = 1)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);

        var result = await mediator.Send(new GetDocumentHistoryQuery(category, pageSize, page));
        return Results.Ok(result);
    }

    private static async Task<IResult> DeleteDocumentAsync(int id, IMediator mediator)
    {
        var existing = await mediator.Send(new GetDocumentQuery(id));
        if (existing is null)
            return Results.NotFound(new { error = "Document not found.", status = 404 });

        await mediator.Send(new DeleteDocumentCommand(id));
        return Results.NoContent();
    }

    /// <summary>Sanitizes filename by stripping path separators and control characters.</summary>
    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "unnamed-document";

        // Strip path separators (prevents path traversal)
        var name = Path.GetFileName(fileName);

        // Remove control characters
        name = new string(name.Where(c => !char.IsControl(c)).ToArray());

        // Limit length
        if (name.Length > 200)
            name = name[..200];

        return string.IsNullOrWhiteSpace(name) ? "unnamed-document" : name;
    }
}
