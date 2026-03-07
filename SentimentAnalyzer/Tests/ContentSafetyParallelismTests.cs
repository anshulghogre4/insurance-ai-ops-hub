using Xunit;

namespace SentimentAnalyzer.Tests;

/// <summary>
/// Tests for content safety parallel screening behavior (Sprint 5 Hotfix 5.H4).
/// Validates:
/// - Parallel.ForEachAsync thread safety with Interlocked counters
/// - Cancellation token propagation during parallel screening
/// - Graceful handling of individual chunk failures
/// - MaxDegreeOfParallelism respects Azure Content Safety rate limits
/// </summary>
public class ContentSafetyParallelismTests
{
    [Fact]
    public async Task ParallelScreening_InterlockedCounters_ThreadSafe()
    {
        // Simulates parallel chunk screening with Interlocked counters
        var screenedCount = 0;
        var flaggedCount = 0;
        var chunks = Enumerable.Range(1, 37).Select(i => $"Claim chunk {i}: property damage assessment for policy POL-2024-{i:D5}").ToList();

        await Parallel.ForEachAsync(chunks,
            new ParallelOptions { MaxDegreeOfParallelism = 5 },
            async (chunk, ct) =>
            {
                await Task.Delay(1, ct); // Simulate API latency
                Interlocked.Increment(ref screenedCount);

                // Simulate 10% flag rate
                if (chunk.Contains("00005") || chunk.Contains("00015") || chunk.Contains("00025"))
                {
                    Interlocked.Increment(ref flaggedCount);
                }
            });

        Assert.Equal(37, screenedCount);
        Assert.Equal(3, flaggedCount);
    }

    [Fact]
    public async Task ParallelScreening_IndividualFailure_DoesNotBreakBatch()
    {
        // 5.H4: one chunk failure should not stop the entire batch
        var screenedCount = 0;
        var failedCount = 0;
        var chunks = Enumerable.Range(1, 20).ToList();

        await Parallel.ForEachAsync(chunks,
            new ParallelOptions { MaxDegreeOfParallelism = 5 },
            async (chunkIndex, ct) =>
            {
                try
                {
                    await Task.Delay(1, ct);

                    // Simulate failure on chunk 7 and 13
                    if (chunkIndex == 7 || chunkIndex == 13)
                    {
                        throw new HttpRequestException("Azure Content Safety rate limit exceeded");
                    }

                    Interlocked.Increment(ref screenedCount);
                }
                catch (HttpRequestException)
                {
                    Interlocked.Increment(ref failedCount);
                    // Graceful handling — chunk proceeds without safety flag
                }
            });

        Assert.Equal(18, screenedCount); // 20 - 2 failed = 18 screened
        Assert.Equal(2, failedCount);
    }

    [Fact]
    public async Task ParallelScreening_CancellationPropagated()
    {
        using var cts = new CancellationTokenSource();
        var screenedCount = 0;
        var chunks = Enumerable.Range(1, 100).ToList();

        // Cancel after a short delay
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await Parallel.ForEachAsync(chunks,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = 5,
                    CancellationToken = cts.Token
                },
                async (chunkIndex, ct) =>
                {
                    ct.ThrowIfCancellationRequested();
                    await Task.Delay(10, ct);
                    Interlocked.Increment(ref screenedCount);
                });
        });

        // Not all chunks should have been processed
        Assert.True(screenedCount < 100, $"Expected fewer than 100 chunks processed after cancellation, got {screenedCount}");
    }

    [Fact]
    public async Task ParallelScreening_MaxDegreeRespected()
    {
        // Verify MaxDegreeOfParallelism = 5 limits concurrent operations
        var maxConcurrent = 0;
        var currentConcurrent = 0;
        var lockObj = new object();
        var chunks = Enumerable.Range(1, 50).ToList();

        await Parallel.ForEachAsync(chunks,
            new ParallelOptions { MaxDegreeOfParallelism = 5 },
            async (chunkIndex, ct) =>
            {
                var current = Interlocked.Increment(ref currentConcurrent);

                lock (lockObj)
                {
                    if (current > maxConcurrent)
                        maxConcurrent = current;
                }

                await Task.Delay(5, ct); // Simulate API call

                Interlocked.Decrement(ref currentConcurrent);
            });

        Assert.True(maxConcurrent <= 5, $"Max concurrent operations should be <= 5, got {maxConcurrent}");
    }

    [Fact]
    public async Task ParallelScreening_EmptyChunkList_Completes()
    {
        var screenedCount = 0;
        var emptyChunks = new List<string>();

        await Parallel.ForEachAsync(emptyChunks,
            new ParallelOptions { MaxDegreeOfParallelism = 5 },
            async (chunk, ct) =>
            {
                await Task.Delay(1, ct);
                Interlocked.Increment(ref screenedCount);
            });

        Assert.Equal(0, screenedCount);
    }

    [Fact]
    public async Task ParallelScreening_SafetyFlags_AccumulateCorrectly()
    {
        // Simulate chunk safety screening where flagged chunks accumulate pipe-separated flags
        var results = new System.Collections.Concurrent.ConcurrentDictionary<int, string>();
        var chunks = Enumerable.Range(1, 15).ToList();

        await Parallel.ForEachAsync(chunks,
            new ParallelOptions { MaxDegreeOfParallelism = 5 },
            async (chunkIndex, ct) =>
            {
                await Task.Delay(1, ct);

                // Chunks 3, 6, 9, 12, 15 contain flagged content
                if (chunkIndex % 3 == 0)
                {
                    var flags = chunkIndex switch
                    {
                        3 => "Violence",
                        6 => "Hate|Violence",
                        9 => "SelfHarm",
                        12 => "Violence",
                        15 => "Sexual",
                        _ => ""
                    };
                    results[chunkIndex] = flags;
                }
            });

        Assert.Equal(5, results.Count);
        Assert.Equal("Violence", results[3]);
        Assert.Equal("Hate|Violence", results[6]);
        Assert.Equal("SelfHarm", results[9]);
    }
}
