using System.Text.RegularExpressions;
using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Memory;
using GoogleAdk.Core.Abstractions.Sessions;

namespace GoogleAdk.Core.Memory;

/// <summary>
/// An in-memory memory service for prototyping. Uses keyword matching.
/// </summary>
public class InMemoryMemoryService : IBaseMemoryService
{
    private readonly Dictionary<string, Dictionary<string, List<Event>>> _sessionEvents = new();
    private readonly Dictionary<string, List<MemoryEntry>> _directMemories = new();

    public Task AddSessionToMemoryAsync(Session session)
    {
        var userKey = $"{session.AppName}/{session.UserId}";

        if (!_sessionEvents.ContainsKey(userKey))
            _sessionEvents[userKey] = new();

        _sessionEvents[userKey][session.Id] = session.Events
            .Where(e => e.Content?.Parts is { Count: > 0 })
            .ToList();

        return Task.CompletedTask;
    }

    public Task AddEventsToMemoryAsync(string appName, string userId, IEnumerable<Event> events, string? sessionId = null, IDictionary<string, object>? customMetadata = null)
    {
        var userKey = $"{appName}/{userId}";
        if (!_sessionEvents.ContainsKey(userKey))
            _sessionEvents[userKey] = new();

        var sid = sessionId ?? "default";
        if (!_sessionEvents[userKey].ContainsKey(sid))
            _sessionEvents[userKey][sid] = new();

        _sessionEvents[userKey][sid].AddRange(events.Where(e => e.Content?.Parts is { Count: > 0 }));

        return Task.CompletedTask;
    }

    public Task AddMemoryAsync(string appName, string userId, IEnumerable<MemoryEntry> memories, IDictionary<string, object>? customMetadata = null)
    {
        var userKey = $"{appName}/{userId}";
        if (!_directMemories.ContainsKey(userKey))
            _directMemories[userKey] = new();

        _directMemories[userKey].AddRange(memories);
        return Task.CompletedTask;
    }

    public Task<SearchMemoryResponse> SearchMemoryAsync(SearchMemoryRequest request)
    {
        var userKey = $"{request.AppName}/{request.UserId}";

        _sessionEvents.TryGetValue(userKey, out var sessions);

        var queryWords = request.Query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var response = new SearchMemoryResponse();

        if (sessions != null)
        {
            foreach (var events in sessions.Values)
            {
                foreach (var evt in events)
                {
                    if (evt.Content?.Parts is not { Count: > 0 }) continue;

                    var joinedText = string.Join(" ",
                        evt.Content.Parts
                            .Where(p => !string.IsNullOrEmpty(p.Text))
                            .Select(p => p.Text!));

                    var words = ExtractWordsLower(joinedText);
                    if (words.Count == 0) continue;

                    if (queryWords.Any(qw => words.Contains(qw)))
                    {
                        response.Memories.Add(new MemoryEntry
                        {
                            Content = evt.Content,
                            Author = evt.Author,
                            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(evt.Timestamp).ToString("o")
                        });
                    }
                }
            }
        }

        if (_directMemories.TryGetValue(userKey, out var directMemories))
        {
            foreach (var mem in directMemories)
            {
                if (mem.Content?.Parts is not { Count: > 0 }) continue;

                var joinedText = string.Join(" ",
                    mem.Content.Parts
                        .Where(p => !string.IsNullOrEmpty(p.Text))
                        .Select(p => p.Text!));

                var words = ExtractWordsLower(joinedText);
                if (words.Count == 0) continue;

                if (queryWords.Any(qw => words.Contains(qw)))
                {
                    response.Memories.Add(mem);
                }
            }
        }

        return Task.FromResult(response);
    }

    private static HashSet<string> ExtractWordsLower(string text)
    {
        return new HashSet<string>(
            Regex.Matches(text, @"[A-Za-z]+")
                .Select(m => m.Value.ToLowerInvariant()));
    }
}
