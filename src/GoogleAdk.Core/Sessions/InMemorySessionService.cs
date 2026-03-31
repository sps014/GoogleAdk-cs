// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Sessions;

namespace GoogleAdk.Core.Sessions;

/// <summary>
/// An in-memory implementation of the session service.
/// </summary>
public class InMemorySessionService : BaseSessionService
{
    // appName -> userId -> sessionId -> Session
    private readonly Dictionary<string, Dictionary<string, Dictionary<string, Session>>> _sessions = new();
    // appName -> userId -> state
    private readonly Dictionary<string, Dictionary<string, Dictionary<string, object?>>> _userState = new();
    // appName -> state
    private readonly Dictionary<string, Dictionary<string, object?>> _appState = new();

    public override Task<Session> CreateSessionAsync(CreateSessionRequest request)
    {
        var session = Session.Create(
            id: request.SessionId ?? Guid.NewGuid().ToString(),
            appName: request.AppName,
            userId: request.UserId,
            state: request.State != null ? new Dictionary<string, object?>(request.State) : null
        );
        session.LastUpdateTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        _sessions.TryAdd(request.AppName, new());
        _sessions[request.AppName].TryAdd(request.UserId, new());
        _sessions[request.AppName][request.UserId][session.Id] = session;

        var copied = DeepClone(session);
        copied.State = MergeStates(
            _appState.GetValueOrDefault(request.AppName),
            _userState.GetValueOrDefault(request.AppName)?.GetValueOrDefault(request.UserId),
            copied.State);

        return Task.FromResult(copied);
    }

    public override Task<Session?> GetSessionAsync(GetSessionRequest request)
    {
        if (!_sessions.TryGetValue(request.AppName, out var byUser) ||
            !byUser.TryGetValue(request.UserId, out var bySession) ||
            !bySession.TryGetValue(request.SessionId, out var session))
        {
            return Task.FromResult<Session?>(null);
        }

        var copied = DeepClone(session);

        if (request.Config != null)
        {
            if (request.Config.NumRecentEvents.HasValue)
            {
                var n = request.Config.NumRecentEvents.Value;
                copied.Events = copied.Events.Count > n
                    ? copied.Events.GetRange(copied.Events.Count - n, n)
                    : copied.Events;
            }

            if (request.Config.AfterTimestamp.HasValue)
            {
                var ts = request.Config.AfterTimestamp.Value;
                int i = copied.Events.Count - 1;
                while (i >= 0 && copied.Events[i].Timestamp >= ts) i--;
                if (i >= 0)
                    copied.Events = copied.Events.GetRange(i + 1, copied.Events.Count - i - 1);
            }
        }

        copied.State = MergeStates(
            _appState.GetValueOrDefault(request.AppName),
            _userState.GetValueOrDefault(request.AppName)?.GetValueOrDefault(request.UserId),
            copied.State);

        return Task.FromResult<Session?>(copied);
    }

    public override Task<ListSessionsResponse> ListSessionsAsync(ListSessionsRequest request)
    {
        if (!_sessions.TryGetValue(request.AppName, out var byUser) ||
            !byUser.TryGetValue(request.UserId, out var bySession))
        {
            return Task.FromResult(new ListSessionsResponse());
        }

        var sessions = bySession.Values.Select(s => Session.Create(s.Id, s.AppName, s.UserId)).ToList();
        foreach (var s in sessions)
            s.LastUpdateTime = bySession[s.Id].LastUpdateTime;

        return Task.FromResult(new ListSessionsResponse { Sessions = sessions });
    }

    public override Task DeleteSessionAsync(DeleteSessionRequest request)
    {
        if (_sessions.TryGetValue(request.AppName, out var byUser) &&
            byUser.TryGetValue(request.UserId, out var bySession))
        {
            bySession.Remove(request.SessionId);
        }
        return Task.CompletedTask;
    }

    public override async Task<Event> AppendEventAsync(AppendEventRequest request)
    {
        var evt = await base.AppendEventAsync(request);
        request.Session.LastUpdateTime = evt.Timestamp;

        var appName = request.Session.AppName;
        var userId = request.Session.UserId;
        var sessionId = request.Session.Id;

        if (!_sessions.TryGetValue(appName, out var byUser) ||
            !byUser.TryGetValue(userId, out var bySession) ||
            !bySession.TryGetValue(sessionId, out var storageSession))
        {
            return evt;
        }

        // Handle app and user state scoping
        if (evt.Actions?.StateDelta != null)
        {
            foreach (var key in evt.Actions.StateDelta.Keys)
            {
                if (key.StartsWith(State.AppPrefix))
                {
                    _appState.TryAdd(appName, new());
                    _appState[appName][key[State.AppPrefix.Length..]] = evt.Actions.StateDelta[key];
                }

                if (key.StartsWith(State.UserPrefix))
                {
                    _userState.TryAdd(appName, new());
                    _userState[appName].TryAdd(userId, new());
                    _userState[appName][userId][key[State.UserPrefix.Length..]] = evt.Actions.StateDelta[key];
                }
            }
        }

        await base.AppendEventAsync(new AppendEventRequest { Session = storageSession, Event = evt });
        storageSession.LastUpdateTime = evt.Timestamp;

        return evt;
    }

    private static Session DeepClone(Session session)
    {
        var json = JsonSerializer.Serialize(session);
        return JsonSerializer.Deserialize<Session>(json)!;
    }
}
