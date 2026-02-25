namespace SentimentAnalyzer.API.Models;

/// <summary>
/// Generic pagination wrapper for list responses.
/// Includes total count, page number, and page size for frontend pagination.
/// </summary>
public class PaginatedResponse<T>
{
    /// <summary>Items for the current page.</summary>
    public List<T> Items { get; set; } = [];

    /// <summary>Total number of items across all pages.</summary>
    public int TotalCount { get; set; }

    /// <summary>Current page number (1-based).</summary>
    public int Page { get; set; }

    /// <summary>Number of items per page.</summary>
    public int PageSize { get; set; }

    /// <summary>Total number of pages.</summary>
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}
