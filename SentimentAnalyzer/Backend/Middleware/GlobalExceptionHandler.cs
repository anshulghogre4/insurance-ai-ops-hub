using System.Net;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;

namespace SentimentAnalyzer.API.Middleware;

/// <summary>
/// Global exception handler that provides consistent error responses
/// and centralized logging for all unhandled exceptions.
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);

        var (statusCode, message) = exception switch
        {
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Authentication required. Please sign in."),
            SecurityTokenExpiredException => (StatusCodes.Status401Unauthorized, "Your session has expired. Please sign in again."),
            SecurityTokenException => (StatusCodes.Status401Unauthorized, "Invalid authentication token."),
            HttpRequestException httpEx when httpEx.StatusCode == System.Net.HttpStatusCode.TooManyRequests =>
                (StatusCodes.Status429TooManyRequests, "AI provider rate limit exceeded. Please wait and try again."),
            HttpRequestException httpEx when httpEx.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable =>
                (StatusCodes.Status503ServiceUnavailable, "AI provider temporarily unavailable. Please try again shortly."),
            HttpRequestException => (StatusCodes.Status502BadGateway, "Failed to reach an upstream AI provider. Please try again."),
            OperationCanceledException => (StatusCodes.Status408RequestTimeout, "Request timed out."),
            ArgumentException => (StatusCodes.Status400BadRequest, exception.Message),
            InvalidOperationException => (StatusCodes.Status400BadRequest, exception.Message),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred. Please try again.")
        };

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(new
        {
            error = message,
            status = statusCode
        }, cancellationToken);

        return true;
    }
}
