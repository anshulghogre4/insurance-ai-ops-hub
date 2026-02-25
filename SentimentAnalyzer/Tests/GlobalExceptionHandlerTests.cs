using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Moq;
using SentimentAnalyzer.API.Middleware;
using Xunit;

namespace SentimentAnalyzer.Tests;

/// <summary>
/// Tests for GlobalExceptionHandler verifying correct status code mapping
/// for different exception types, including HttpRequestException variants.
/// </summary>
public class GlobalExceptionHandlerTests
{
    private readonly GlobalExceptionHandler _handler;

    public GlobalExceptionHandlerTests()
    {
        _handler = new GlobalExceptionHandler(
            new Mock<ILogger<GlobalExceptionHandler>>().Object);
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<(int StatusCode, string Error)> GetResponse(DefaultHttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        var json = JsonDocument.Parse(body);
        return (context.Response.StatusCode, json.RootElement.GetProperty("error").GetString()!);
    }

    [Fact]
    public async Task TryHandleAsync_HttpRequestException429_Returns429WithMessage()
    {
        var context = CreateHttpContext();
        var ex = new HttpRequestException("Rate limited", null, HttpStatusCode.TooManyRequests);

        var handled = await _handler.TryHandleAsync(context, ex, CancellationToken.None);

        Assert.True(handled);
        var (statusCode, error) = await GetResponse(context);
        Assert.Equal(StatusCodes.Status429TooManyRequests, statusCode);
        Assert.Contains("rate limit", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryHandleAsync_HttpRequestException503_Returns503WithMessage()
    {
        var context = CreateHttpContext();
        var ex = new HttpRequestException("Service down", null, HttpStatusCode.ServiceUnavailable);

        var handled = await _handler.TryHandleAsync(context, ex, CancellationToken.None);

        Assert.True(handled);
        var (statusCode, error) = await GetResponse(context);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, statusCode);
        Assert.Contains("unavailable", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryHandleAsync_GenericHttpRequestException_Returns502WithMessage()
    {
        var context = CreateHttpContext();
        var ex = new HttpRequestException("Connection refused");

        var handled = await _handler.TryHandleAsync(context, ex, CancellationToken.None);

        Assert.True(handled);
        var (statusCode, error) = await GetResponse(context);
        Assert.Equal(StatusCodes.Status502BadGateway, statusCode);
        Assert.Contains("upstream", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryHandleAsync_UnauthorizedAccessException_Returns401()
    {
        var context = CreateHttpContext();
        var ex = new UnauthorizedAccessException("Access denied");

        var handled = await _handler.TryHandleAsync(context, ex, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task TryHandleAsync_OperationCanceledException_Returns408()
    {
        var context = CreateHttpContext();
        var ex = new OperationCanceledException("Timed out");

        var handled = await _handler.TryHandleAsync(context, ex, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status408RequestTimeout, context.Response.StatusCode);
    }

    [Fact]
    public async Task TryHandleAsync_TaskCanceledException_Returns408()
    {
        var context = CreateHttpContext();
        var ex = new TaskCanceledException("Task was cancelled");

        var handled = await _handler.TryHandleAsync(context, ex, CancellationToken.None);

        Assert.True(handled);
        // TaskCanceledException inherits from OperationCanceledException
        Assert.Equal(StatusCodes.Status408RequestTimeout, context.Response.StatusCode);
    }

    [Fact]
    public async Task TryHandleAsync_ArgumentException_Returns400WithMessage()
    {
        var context = CreateHttpContext();
        var ex = new ArgumentException("Invalid input parameter.");

        var handled = await _handler.TryHandleAsync(context, ex, CancellationToken.None);

        Assert.True(handled);
        var (statusCode, error) = await GetResponse(context);
        Assert.Equal(StatusCodes.Status400BadRequest, statusCode);
        Assert.Equal("Invalid input parameter.", error);
    }

    [Fact]
    public async Task TryHandleAsync_SecurityTokenExpiredException_Returns401()
    {
        var context = CreateHttpContext();
        var ex = new SecurityTokenExpiredException("Token expired");

        var handled = await _handler.TryHandleAsync(context, ex, CancellationToken.None);

        Assert.True(handled);
        var (statusCode, error) = await GetResponse(context);
        Assert.Equal(StatusCodes.Status401Unauthorized, statusCode);
        Assert.Contains("expired", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryHandleAsync_UnexpectedException_Returns500()
    {
        var context = CreateHttpContext();
        var ex = new NullReferenceException("Object reference not set");

        var handled = await _handler.TryHandleAsync(context, ex, CancellationToken.None);

        Assert.True(handled);
        var (statusCode, error) = await GetResponse(context);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusCode);
        Assert.DoesNotContain("Object reference", error); // Must not leak internal details
    }

    [Fact]
    public async Task TryHandleAsync_ErrorResponseDoesNotLeakStackTrace()
    {
        var context = CreateHttpContext();
        var ex = new InvalidOperationException("No AI providers configured.");

        await _handler.TryHandleAsync(context, ex, CancellationToken.None);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();

        Assert.DoesNotContain("at SentimentAnalyzer", body);
        Assert.DoesNotContain("StackTrace", body);
    }
}
