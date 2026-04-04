using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Abstractions.Sessions;

namespace GoogleAdk.Core.Abstractions.Memory;

/// <summary>
/// Represents one memory entry.
/// </summary>
public class MemoryEntry
{
    /// <summary>The content of the memory entry.</summary>
    public Content Content { get; set; } = null!;

    /// <summary>The author of the memory.</summary>
    public string? Author { get; set; }

    /// <summary>
    /// The timestamp when the original content of this memory happened (ISO 8601 preferred).
    /// </summary>
    public string? Timestamp { get; set; }
}

public class SearchMemoryRequest
{
    public string AppName { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
}

public class SearchMemoryResponse
{
    public List<MemoryEntry> Memories { get; set; } = new();
}

/// <summary>
/// Base interface for memory services.
/// </summary>
public interface IBaseMemoryService
{
    /// <summary>Adds a session to the memory.</summary>
    Task AddSessionToMemoryAsync(Session session);

    /// <summary>Adds an explicit list of events to the memory service.</summary>
    Task AddEventsToMemoryAsync(string appName, string userId, IEnumerable<Event> events, string? sessionId = null, IDictionary<string, object>? customMetadata = null)
    {
        throw new NotImplementedException("This memory service does not support adding event deltas. Call AddSessionToMemoryAsync(session) to ingest the full session.");
    }

    /// <summary>Adds explicit memory items directly to the memory service.</summary>
    Task AddMemoryAsync(string appName, string userId, IEnumerable<MemoryEntry> memories, IDictionary<string, object>? customMetadata = null)
    {
        throw new NotImplementedException("This memory service does not support direct memory writes. Call AddEventsToMemoryAsync(...) or AddSessionToMemoryAsync(session) instead.");
    }

    /// <summary>Searches for memories that match the query.</summary>
    Task<SearchMemoryResponse> SearchMemoryAsync(SearchMemoryRequest request);
}
