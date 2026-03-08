using Xunit;

namespace SentimentAnalyzer.Tests;

/// <summary>
/// Validates smoke test script exists and has correct structure.
/// </summary>
public class SmokeTestScriptTests
{
    private static readonly string ScriptPath = Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "scripts", "smoke-test.sh");

    [Fact]
    public void SmokeTestScript_Exists()
    {
        var fullPath = Path.GetFullPath(ScriptPath);
        Assert.True(File.Exists(fullPath), $"Smoke test script not found at {fullPath}");
    }

    [Fact]
    public void SmokeTestScript_ContainsRequiredHealthChecks()
    {
        var fullPath = Path.GetFullPath(ScriptPath);
        var content = File.ReadAllText(fullPath);

        // Must check liveness, readiness, and provider health endpoints
        Assert.Contains("/health", content);
        Assert.Contains("/health/ready", content);
        Assert.Contains("/api/insurance/health/providers", content);

        // Must check frontend loads
        Assert.Contains("app-root", content);

        // Must exit with failure code on any check failure
        Assert.Contains("exit 1", content);
    }
}
