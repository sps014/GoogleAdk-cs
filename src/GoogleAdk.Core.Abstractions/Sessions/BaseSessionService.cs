// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Events;

namespace GoogleAdk.Core.Abstractions.Sessions;

/// <summary>
/// Configuration for getting a session.
/// </summary>
public class GetSessionConfig
{
    /// <summary>The number of recent events to retrieve.</summary>
    public int? NumRecentEvents { get; set; }

    /// <summary>Retrieve events after this timestamp.</summary>
    public long? AfterTimestamp { get; set; }
}

public class CreateSessionRequest
{
    public string AppName { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public Dictionary<string, object?>? State { get; set; }
    public string? SessionId { get; set; }
}

public class GetSessionRequest
{
    public string AppName { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public GetSessionConfig? Config { get; set; }
}

public class ListSessionsRequest
{
    public string AppName { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
}

public class DeleteSessionRequest
{
    public string AppName { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
}

public class AppendEventRequest
{
    public Session Session { get; set; } = null!;
    public Event Event { get; set; } = null!;
}

public class ListSessionsResponse
{
    public List<Session> Sessions { get; set; } = new();
}

/// <summary>
/// Base class for session services.
/// </summary>
public abstract class BaseSessionService
{
    /// <summary>Creates a new session.</summary>
    public abstract Task<Session> CreateSessionAsync(CreateSessionRequest request);

    /// <summary>Gets a session.</summary>
    public abstract Task<Session?> GetSessionAsync(GetSessionRequest request);

    /// <summary>
    /// Gets a session or creates one if it doesn't exist.
    /// </summary>
    public virtual async Task<Session> GetOrCreateSessionAsync(CreateSessionRequest request)
    {
        if (string.IsNullOrEmpty(request.SessionId))
            return await CreateSessionAsync(request);

        var session = await GetSessionAsync(new GetSessionRequest
        {
            AppName = request.AppName,
            UserId = request.UserId,
            SessionId = request.SessionId
        });

        return session ?? await CreateSessionAsync(request);
    }

    /// <summary>Lists sessions for a user.</summary>
    public abstract Task<ListSessionsResponse> ListSessionsAsync(ListSessionsRequest request);

    /// <summary>Deletes a session.</summary>
    public abstract Task DeleteSessionAsync(DeleteSessionRequest request);

    /// <summary>
    /// Appends an event to a session.
    /// </summary>
    public virtual async Task<Event> AppendEventAsync(AppendEventRequest request)
    {
        var evt = request.Event;
        if (evt.Partial == true)
            return evt;

        evt = TrimTempDeltaState(evt);
        UpdateSessionState(request.Session, evt);
        request.Session.Events.Add(evt);
        return evt;
    }

    private void UpdateSessionState(Session session, Event evt)
    {
        if (evt.Actions?.StateDelta == null)
            return;

        foreach (var kv in evt.Actions.StateDelta)
        {
            if (kv.Key.StartsWith(Sessions.State.TempPrefix))
                continue;
            session.State[kv.Key] = kv.Value;
        }
    }

    /// <summary>
    /// Removes temporary state delta keys from the event.
    /// </summary>
    public static Event TrimTempDeltaState(Event evt)
    {
        if (evt.Actions?.StateDelta == null)
            return evt;

        var filtered = new Dictionary<string, object?>();
        foreach (var kv in evt.Actions.StateDelta)
        {
            if (!kv.Key.StartsWith(Sessions.State.TempPrefix))
                filtered[kv.Key] = kv.Value;
        }
        evt.Actions.StateDelta = filtered;
        return evt;
    }

    /// <summary>
    /// Merges app state, user state, and session state.
    /// </summary>
    public static Dictionary<string, object?> MergeStates(
        Dictionary<string, object?>? appState = null,
        Dictionary<string, object?>? userState = null,
        Dictionary<string, object?>? sessionState = null)
    {
        var merged = sessionState != null
            ? new Dictionary<string, object?>(sessionState)
            : new Dictionary<string, object?>();

        if (appState != null)
        {
            foreach (var kv in appState)
                merged[Sessions.State.AppPrefix + kv.Key] = kv.Value;
        }

        if (userState != null)
        {
            foreach (var kv in userState)
                merged[Sessions.State.UserPrefix + kv.Key] = kv.Value;
        }

        return merged;
    }
}
