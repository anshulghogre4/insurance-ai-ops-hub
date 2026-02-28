using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SentimentAnalyzer.API.Data.Entities;
using SentimentAnalyzer.API.Models;

namespace SentimentAnalyzer.API.Data;

/// <summary>
/// EF Core implementation of <see cref="ICxConversationRepository"/>.
/// Persists CX Copilot conversation sessions with sliding-window message history.
/// All stored messages must be PII-redacted before reaching this layer.
/// </summary>
public class SqliteCxConversationRepository : ICxConversationRepository
{
    private readonly InsuranceAnalysisDbContext _db;
    private readonly ILogger<SqliteCxConversationRepository> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Initializes a new instance of <see cref="SqliteCxConversationRepository"/>.
    /// </summary>
    /// <param name="db">The EF Core database context.</param>
    /// <param name="logger">Logger instance.</param>
    public SqliteCxConversationRepository(
        InsuranceAnalysisDbContext db,
        ILogger<SqliteCxConversationRepository> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<string> CreateSessionAsync()
    {
        var sessionId = Guid.NewGuid().ToString();

        var record = new CxConversationRecord
        {
            SessionId = sessionId,
            MessagesJson = "[]",
            LastActivityUtc = DateTime.UtcNow,
            TurnCount = 0
        };

        _db.CxConversations.Add(record);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Created CX conversation session {SessionId}", sessionId);
        return sessionId;
    }

    /// <inheritdoc />
    public async Task<List<CxMessageRecord>> GetRecentTurnsAsync(string sessionId, int maxTurns = 10)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        maxTurns = Math.Clamp(maxTurns, 1, 50);

        var record = await _db.CxConversations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.SessionId == sessionId);

        if (record == null)
        {
            _logger.LogWarning("CX conversation session {SessionId} not found", sessionId);
            return [];
        }

        var messages = DeserializeMessages(record.MessagesJson);

        // Return last N messages (sliding window)
        return messages.Count <= maxTurns
            ? messages
            : messages.Skip(messages.Count - maxTurns).ToList();
    }

    /// <inheritdoc />
    public async Task AppendTurnAsync(string sessionId, string role, string content, int maxTurns = 10)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        maxTurns = Math.Clamp(maxTurns, 1, 50);

        var record = await _db.CxConversations
            .FirstOrDefaultAsync(c => c.SessionId == sessionId);

        if (record == null)
        {
            _logger.LogWarning("CX conversation session {SessionId} not found — creating on-the-fly", sessionId);
            record = new CxConversationRecord
            {
                SessionId = sessionId,
                MessagesJson = "[]",
                LastActivityUtc = DateTime.UtcNow,
                TurnCount = 0
            };
            _db.CxConversations.Add(record);
        }

        var messages = DeserializeMessages(record.MessagesJson);

        messages.Add(new CxMessageRecord
        {
            Role = role,
            Content = content,
            Timestamp = DateTime.UtcNow
        });

        // Sliding window: trim oldest messages if exceeding max
        while (messages.Count > maxTurns)
        {
            messages.RemoveAt(0);
        }

        record.MessagesJson = JsonSerializer.Serialize(messages, JsonOptions);
        record.LastActivityUtc = DateTime.UtcNow;
        record.TurnCount = messages.Count;

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Appended {Role} turn to CX session {SessionId} (total turns: {TurnCount})",
            role, sessionId, record.TurnCount);
    }

    /// <inheritdoc />
    public async Task<bool> SessionExistsAsync(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        return await _db.CxConversations.AnyAsync(c => c.SessionId == sessionId);
    }

    /// <summary>
    /// Deserializes the JSON message array from the database record.
    /// Returns an empty list on deserialization failure (defensive).
    /// </summary>
    private List<CxMessageRecord> DeserializeMessages(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<CxMessageRecord>>(json, JsonOptions) ?? [];
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize CX conversation messages — returning empty list");
            return [];
        }
    }
}
