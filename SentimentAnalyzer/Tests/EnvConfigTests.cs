using Microsoft.Extensions.Configuration;
using Xunit;

namespace SentimentAnalyzer.Tests;

/// <summary>
/// Validates environment-based configuration loading for VPS deployment.
/// </summary>
public class EnvConfigTests
{
    [Fact]
    public void Configuration_LoadsEnvironmentVariables_ForDatabaseProvider()
    {
        // Simulate Docker Compose env: Database__Provider=PostgreSQL
        Environment.SetEnvironmentVariable("Database__Provider", "PostgreSQL");

        var config = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        var provider = config["Database:Provider"];

        Assert.Equal("PostgreSQL", provider);

        // Cleanup
        Environment.SetEnvironmentVariable("Database__Provider", null);
    }

    [Fact]
    public void Configuration_FallsBackToDefault_WhenEnvVarMissing()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "Sqlite"
            })
            .AddEnvironmentVariables()
            .Build();

        // Default from in-memory (simulates appsettings.json)
        var provider = config["Database:Provider"];

        Assert.Equal("Sqlite", provider);
    }
}
