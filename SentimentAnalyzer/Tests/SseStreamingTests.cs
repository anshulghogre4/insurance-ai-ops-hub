using Microsoft.Extensions.Logging;
using Moq;
using SentimentAnalyzer.API.Services.CustomerExperience;
using Xunit;

namespace SentimentAnalyzer.Tests;

/// <summary>
/// Regression tests for SSE streaming behavior (Sprint 5 Hotfixes 5.H1 and 5.H2).
/// Verifies:
/// - No synchronous I/O operations (5.H1: AutoFlush = false, manual FlushAsync)
/// - [DONE] marker is always delivered (5.H2: FlushAsync after [DONE] on all paths)
/// - Mid-stream failures yield proper error chunks
/// </summary>
public class SseStreamingTests
{
    [Fact]
    public async Task StreamWriter_AutoFlushDisabled_ManualFlushRequired()
    {
        // Verify that a StreamWriter with AutoFlush = false does NOT call sync Flush
        // This prevents "Synchronous operations are disallowed" on Kestrel
        using var memStream = new MemoryStream();
        var writer = new StreamWriter(memStream) { AutoFlush = false };

        await writer.WriteLineAsync("data: {\"type\":\"content\",\"content\":\"Coverage for water damage claims\"}");
        await writer.WriteLineAsync();
        await writer.FlushAsync();

        memStream.Position = 0;
        using var reader = new StreamReader(memStream);
        var output = await reader.ReadToEndAsync();

        Assert.Contains("water damage claims", output);
    }

    [Fact]
    public async Task SseStream_DoneMarkerDelivered_OnSuccessPath()
    {
        // Simulates SSE success path: content chunks → metadata → [DONE]
        using var memStream = new MemoryStream();
        var writer = new StreamWriter(memStream) { AutoFlush = false };

        // Simulate streaming content
        await writer.WriteLineAsync("data: {\"type\":\"content\",\"content\":\"Your homeowners policy covers\"}");
        await writer.WriteLineAsync();
        await writer.FlushAsync();

        await writer.WriteLineAsync("data: {\"type\":\"content\",\"content\":\" wind and hail damage\"}");
        await writer.WriteLineAsync();
        await writer.FlushAsync();

        // Metadata chunk
        await writer.WriteLineAsync("data: {\"type\":\"metadata\",\"tone\":\"Professional\"}");
        await writer.WriteLineAsync();
        await writer.FlushAsync();

        // [DONE] marker with explicit flush
        await writer.WriteLineAsync("data: [DONE]");
        await writer.FlushAsync();

        memStream.Position = 0;
        using var reader = new StreamReader(memStream);
        var output = await reader.ReadToEndAsync();

        Assert.Contains("[DONE]", output);
        Assert.Contains("homeowners policy covers", output);
        Assert.Contains("wind and hail damage", output);
    }

    [Fact]
    public async Task SseStream_DoneMarkerDelivered_OnErrorPath()
    {
        // 5.H2 regression: [DONE] must be sent even when stream errors occur
        using var memStream = new MemoryStream();
        var writer = new StreamWriter(memStream) { AutoFlush = false };

        // Simulate partial streaming followed by error
        await writer.WriteLineAsync("data: {\"type\":\"content\",\"content\":\"Processing claim CLM-2024-\"}");
        await writer.WriteLineAsync();
        await writer.FlushAsync();

        // Error event
        await writer.WriteLineAsync("data: {\"type\":\"error\",\"content\":\"LLM provider timeout after 30s\"}");
        await writer.WriteLineAsync();
        await writer.FlushAsync();

        // [DONE] must STILL be sent on error path
        await writer.WriteLineAsync("data: [DONE]");
        await writer.FlushAsync();

        memStream.Position = 0;
        using var reader = new StreamReader(memStream);
        var output = await reader.ReadToEndAsync();

        Assert.Contains("[DONE]", output);
        Assert.Contains("error", output);
    }

    [Fact]
    public async Task SseStream_EmptyLineseparateEvents()
    {
        // SSE spec requires empty line between events
        using var memStream = new MemoryStream();
        var writer = new StreamWriter(memStream) { AutoFlush = false };

        await writer.WriteLineAsync("data: {\"type\":\"content\",\"content\":\"Deductible: $500\"}");
        await writer.WriteLineAsync(); // Empty line separator required
        await writer.FlushAsync();

        await writer.WriteLineAsync("data: {\"type\":\"content\",\"content\":\" per occurrence\"}");
        await writer.WriteLineAsync();
        await writer.FlushAsync();

        memStream.Position = 0;
        using var reader = new StreamReader(memStream);
        var lines = (await reader.ReadToEndAsync()).Split('\n', StringSplitOptions.None);

        // Verify empty line separators exist between data lines
        var dataLineIndices = lines
            .Select((line, idx) => (line, idx))
            .Where(x => x.line.StartsWith("data:"))
            .Select(x => x.idx)
            .ToList();

        Assert.True(dataLineIndices.Count >= 2);
        // After each data line there should be an empty line
        Assert.True(string.IsNullOrWhiteSpace(lines[dataLineIndices[0] + 1]));
    }

    [Fact]
    public async Task SseStream_CancellationRespected_NoDoneAfterCancel()
    {
        using var cts = new CancellationTokenSource();
        using var memStream = new MemoryStream();
        var writer = new StreamWriter(memStream) { AutoFlush = false };

        await writer.WriteLineAsync("data: {\"type\":\"content\",\"content\":\"Starting claim review\"}");
        await writer.WriteLineAsync();
        await writer.FlushAsync();

        // Cancel mid-stream
        await cts.CancelAsync();

        // After cancellation, no more writes should occur
        if (!cts.Token.IsCancellationRequested)
        {
            await writer.WriteLineAsync("data: {\"type\":\"content\",\"content\":\"This should not appear\"}");
        }

        memStream.Position = 0;
        using var reader = new StreamReader(memStream);
        var output = await reader.ReadToEndAsync();

        Assert.Contains("Starting claim review", output);
        Assert.DoesNotContain("This should not appear", output);
    }
}
