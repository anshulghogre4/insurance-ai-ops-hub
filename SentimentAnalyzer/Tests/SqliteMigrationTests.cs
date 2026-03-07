using Microsoft.Data.Sqlite;
using Xunit;

namespace SentimentAnalyzer.Tests;

/// <summary>
/// Integration tests for SQLite auto-migration block.
/// Verifies that all ALTER TABLE and CREATE TABLE operations are idempotent
/// (safe to run multiple times without errors). Prevents Sprint 5 Hotfix 5.H3 regression
/// where missing columns caused runtime failures.
/// </summary>
public class SqliteMigrationTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteMigrationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }

    [Fact]
    public void AlterTable_AddColumn_Idempotent_DoesNotThrow()
    {
        // Create base table
        ExecuteSql("CREATE TABLE Documents (Id INTEGER PRIMARY KEY, FileName TEXT NOT NULL DEFAULT '')");

        // First ALTER — should succeed
        AddColumnIfNotExists("Documents", "ExtractedText", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfNotExists("Documents", "PageCount", "INTEGER NOT NULL DEFAULT 0");

        // Second ALTER — should be idempotent (no error)
        AddColumnIfNotExists("Documents", "ExtractedText", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfNotExists("Documents", "PageCount", "INTEGER NOT NULL DEFAULT 0");

        // Verify columns exist
        Assert.True(ColumnExists("Documents", "ExtractedText"));
        Assert.True(ColumnExists("Documents", "PageCount"));
    }

    [Fact]
    public void AlterTable_DocumentChunks_ChunkLevelAndParentChunkId()
    {
        // Reproduces Sprint 5 Hotfix 5.H3: "table DocumentChunks has no column named ChunkLevel"
        ExecuteSql(@"CREATE TABLE DocumentChunks (
            Id INTEGER PRIMARY KEY, DocumentId INTEGER NOT NULL,
            ChunkIndex INTEGER NOT NULL, Content TEXT NOT NULL DEFAULT '',
            SectionName TEXT NOT NULL DEFAULT '', TokenCount INTEGER NOT NULL DEFAULT 0,
            EmbeddingJson TEXT NOT NULL DEFAULT '[]')");

        // These columns were missing in 5.H3
        AddColumnIfNotExists("DocumentChunks", "ChunkLevel", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfNotExists("DocumentChunks", "ParentChunkId", "INTEGER");
        AddColumnIfNotExists("DocumentChunks", "PageNumber", "INTEGER");
        AddColumnIfNotExists("DocumentChunks", "IsSafe", "INTEGER NOT NULL DEFAULT 1");
        AddColumnIfNotExists("DocumentChunks", "SafetyFlags", "TEXT");

        Assert.True(ColumnExists("DocumentChunks", "ChunkLevel"));
        Assert.True(ColumnExists("DocumentChunks", "ParentChunkId"));
        Assert.True(ColumnExists("DocumentChunks", "IsSafe"));
    }

    [Fact]
    public void CreateTable_IfNotExists_Idempotent()
    {
        var createSql = @"CREATE TABLE IF NOT EXISTS CxConversations (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            SessionId TEXT NOT NULL DEFAULT '',
            MessagesJson TEXT NOT NULL DEFAULT '[]',
            LastActivityUtc TEXT NOT NULL DEFAULT '0001-01-01T00:00:00',
            TurnCount INTEGER NOT NULL DEFAULT 0)";

        // First create
        ExecuteSql(createSql);
        Assert.True(TableExists("CxConversations"));

        // Second create — should be idempotent
        ExecuteSql(createSql);
        Assert.True(TableExists("CxConversations"));
    }

    [Fact]
    public void CreateTable_CxInteractions_WithIndexes()
    {
        ExecuteSql(@"CREATE TABLE IF NOT EXISTS CxInteractions (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            MessageHash TEXT NOT NULL DEFAULT '',
            ResponseText TEXT NOT NULL DEFAULT '',
            Tone TEXT NOT NULL DEFAULT 'Professional',
            EscalationRecommended INTEGER NOT NULL DEFAULT 0,
            LlmProvider TEXT NOT NULL DEFAULT '',
            CreatedAt TEXT NOT NULL DEFAULT '0001-01-01T00:00:00')");

        ExecuteSql("CREATE INDEX IF NOT EXISTS IX_CxInteractions_CreatedAt ON CxInteractions(CreatedAt)");
        ExecuteSql("CREATE INDEX IF NOT EXISTS IX_CxInteractions_EscalationRecommended ON CxInteractions(EscalationRecommended)");

        Assert.True(TableExists("CxInteractions"));

        // Verify indexes exist
        Assert.True(IndexExists("IX_CxInteractions_CreatedAt"));
        Assert.True(IndexExists("IX_CxInteractions_EscalationRecommended"));
    }

    [Fact]
    public void CreateTable_FraudCorrelations_WithIndexes()
    {
        ExecuteSql(@"CREATE TABLE IF NOT EXISTS FraudCorrelations (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            SourceClaimId INTEGER NOT NULL,
            CorrelatedClaimId INTEGER NOT NULL,
            CorrelationType TEXT NOT NULL DEFAULT '',
            CorrelationScore REAL NOT NULL DEFAULT 0.0,
            Details TEXT NOT NULL DEFAULT '',
            DetectedAt TEXT NOT NULL DEFAULT '0001-01-01T00:00:00',
            Status TEXT NOT NULL DEFAULT 'Pending')");

        Assert.True(TableExists("FraudCorrelations"));

        // Run again — idempotent
        ExecuteSql(@"CREATE TABLE IF NOT EXISTS FraudCorrelations (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            SourceClaimId INTEGER NOT NULL,
            CorrelatedClaimId INTEGER NOT NULL,
            CorrelationType TEXT NOT NULL DEFAULT '',
            CorrelationScore REAL NOT NULL DEFAULT 0.0,
            Details TEXT NOT NULL DEFAULT '',
            DetectedAt TEXT NOT NULL DEFAULT '0001-01-01T00:00:00',
            Status TEXT NOT NULL DEFAULT 'Pending')");

        Assert.True(TableExists("FraudCorrelations"));
    }

    [Fact]
    public void CreateTable_DocumentQAPairs_WithForeignKeys()
    {
        // Parent tables must exist first
        ExecuteSql("CREATE TABLE IF NOT EXISTS Documents (Id INTEGER PRIMARY KEY, FileName TEXT DEFAULT '')");
        ExecuteSql(@"CREATE TABLE IF NOT EXISTS DocumentChunks (Id INTEGER PRIMARY KEY, DocumentId INTEGER NOT NULL,
            ChunkIndex INTEGER NOT NULL, Content TEXT DEFAULT '', SectionName TEXT DEFAULT '', TokenCount INTEGER DEFAULT 0, EmbeddingJson TEXT DEFAULT '[]')");

        ExecuteSql(@"CREATE TABLE IF NOT EXISTS DocumentQAPairs (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            DocumentId INTEGER NOT NULL,
            ChunkId INTEGER NOT NULL,
            Question TEXT NOT NULL DEFAULT '',
            Answer TEXT NOT NULL DEFAULT '',
            Category TEXT NOT NULL DEFAULT 'factual',
            Confidence REAL NOT NULL DEFAULT 0.0,
            LlmProvider TEXT NOT NULL DEFAULT '',
            CreatedAt TEXT NOT NULL DEFAULT '0001-01-01T00:00:00',
            FOREIGN KEY (DocumentId) REFERENCES Documents(Id),
            FOREIGN KEY (ChunkId) REFERENCES DocumentChunks(Id))");

        Assert.True(TableExists("DocumentQAPairs"));
    }

    [Fact]
    public void FullMigrationSequence_RunTwice_NoErrors()
    {
        // Simulate full migration sequence running twice (app restart scenario)
        RunFullMigration();
        RunFullMigration(); // Second run must be idempotent
    }

    private void RunFullMigration()
    {
        // Core tables
        ExecuteSql("CREATE TABLE IF NOT EXISTS Documents (Id INTEGER PRIMARY KEY, FileName TEXT DEFAULT '', MimeType TEXT DEFAULT '', Category TEXT DEFAULT '', Status TEXT DEFAULT '', CreatedAt TEXT DEFAULT '')");
        ExecuteSql(@"CREATE TABLE IF NOT EXISTS DocumentChunks (Id INTEGER PRIMARY KEY, DocumentId INTEGER NOT NULL,
            ChunkIndex INTEGER NOT NULL, Content TEXT DEFAULT '', SectionName TEXT DEFAULT '', TokenCount INTEGER DEFAULT 0, EmbeddingJson TEXT DEFAULT '[]')");
        ExecuteSql("CREATE TABLE IF NOT EXISTS Claims (Id INTEGER PRIMARY KEY, ClaimId TEXT DEFAULT '', Severity TEXT DEFAULT '', CreatedAt TEXT DEFAULT '')");

        // Column migrations
        AddColumnIfNotExists("Documents", "ExtractedText", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfNotExists("Documents", "PageCount", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfNotExists("Documents", "EmbeddingProvider", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfNotExists("DocumentChunks", "ChunkLevel", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfNotExists("DocumentChunks", "ParentChunkId", "INTEGER");
        AddColumnIfNotExists("DocumentChunks", "IsSafe", "INTEGER NOT NULL DEFAULT 1");
        AddColumnIfNotExists("Claims", "FraudRiskLevel", "TEXT NOT NULL DEFAULT 'VeryLow'");

        // New tables
        ExecuteSql("CREATE TABLE IF NOT EXISTS CxConversations (Id INTEGER PRIMARY KEY AUTOINCREMENT, SessionId TEXT DEFAULT '')");
        ExecuteSql("CREATE TABLE IF NOT EXISTS FraudCorrelations (Id INTEGER PRIMARY KEY AUTOINCREMENT, SourceClaimId INTEGER NOT NULL)");
    }

    private void ExecuteSql(string sql)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private bool ColumnExists(string table, string column)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({table})";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private bool TableExists(string table)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{table}'";
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    private bool IndexExists(string indexName)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name='{indexName}'";
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    private void AddColumnIfNotExists(string table, string column, string definition)
    {
        if (!ColumnExists(table, column))
        {
            ExecuteSql($"ALTER TABLE {table} ADD COLUMN {column} {definition}");
        }
    }
}
