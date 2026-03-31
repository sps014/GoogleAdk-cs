// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Abstractions.Sessions;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace GoogleAdk.Sessions.EfCore;

/// <summary>
/// EF Core-based session service supporting any database provider.
/// Implements three-tier state model: app → user → session.
/// </summary>
public class EfCoreSessionService : BaseSessionService
{
    private readonly IDbContextFactory<AdkSessionDbContext> _dbFactory;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public EfCoreSessionService(IDbContextFactory<AdkSessionDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public override async Task<Session> CreateSessionAsync(CreateSessionRequest request)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var sessionId = request.SessionId ?? Guid.NewGuid().ToString();
        var state = request.State ?? new Dictionary<string, object?>();

        var storageSession = new StorageSession
        {
            Id = sessionId,
            AppName = request.AppName,
            UserId = request.UserId ?? "default",
            StateJson = JsonSerializer.Serialize(ExtractSessionState(state), JsonOptions),
        };

        db.Sessions.Add(storageSession);

        // Upsert app state
        var appState = await db.AppStates.FindAsync(request.AppName);
        if (appState == null)
        {
            appState = new StorageAppState { AppName = request.AppName };
            db.AppStates.Add(appState);
        }
        var appStateDict = JsonSerializer.Deserialize<Dictionary<string, object?>>(appState.StateJson, JsonOptions) ?? new();
        MergeStateInto(appStateDict, state, "app:");
        appState.StateJson = JsonSerializer.Serialize(appStateDict, JsonOptions);
        appState.UpdateTime = DateTime.UtcNow;

        // Upsert user state
        var userId = request.UserId ?? "default";
        var userState = await db.UserStates.FindAsync(request.AppName, userId);
        if (userState == null)
        {
            userState = new StorageUserState { AppName = request.AppName, UserId = userId };
            db.UserStates.Add(userState);
        }
        var userStateDict = JsonSerializer.Deserialize<Dictionary<string, object?>>(userState.StateJson, JsonOptions) ?? new();
        MergeStateInto(userStateDict, state, "user:");
        userState.StateJson = JsonSerializer.Serialize(userStateDict, JsonOptions);
        userState.UpdateTime = DateTime.UtcNow;

        await db.SaveChangesAsync();

        // Merge all tiers
        var mergedState = MergeAllStates(appStateDict, userStateDict,
            JsonSerializer.Deserialize<Dictionary<string, object?>>(storageSession.StateJson, JsonOptions) ?? new());

        return Session.Create(sessionId, request.AppName, userId, mergedState);
    }

    public override async Task<Session?> GetSessionAsync(GetSessionRequest request)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var stored = await db.Sessions.FirstOrDefaultAsync(s =>
            s.AppName == request.AppName &&
            s.UserId == request.UserId &&
            s.Id == request.SessionId);

        if (stored == null) return null;

        var eventsQuery = db.Events
            .Where(e => e.AppName == request.AppName && e.UserId == request.UserId && e.SessionId == request.SessionId)
            .OrderBy(e => e.Timestamp);

        var storageEvents = await eventsQuery.ToListAsync();
        var events = storageEvents
            .Select(e => JsonSerializer.Deserialize<Event>(e.EventDataJson, JsonOptions)!)
            .ToList();

        var sessionState = JsonSerializer.Deserialize<Dictionary<string, object?>>(stored.StateJson, JsonOptions) ?? new();
        var appState = await GetAppStateAsync(db, request.AppName);
        var userState = await GetUserStateAsync(db, request.AppName, request.UserId);
        var mergedState = MergeAllStates(appState, userState, sessionState);

        var session2 = Session.Create(stored.Id, stored.AppName, stored.UserId, mergedState);
        session2.Events = events;
        return session2;
    }

    public override async Task<ListSessionsResponse> ListSessionsAsync(ListSessionsRequest request)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var query = db.Sessions
            .Where(s => s.AppName == request.AppName && s.UserId == request.UserId);

        var stored = await query.ToListAsync();
        var appState = await GetAppStateAsync(db, request.AppName);
        var userState = await GetUserStateAsync(db, request.AppName, request.UserId);

        var sessions = stored.Select(s =>
        {
            var sessionState = JsonSerializer.Deserialize<Dictionary<string, object?>>(s.StateJson, JsonOptions) ?? new();
            var mergedState = MergeAllStates(appState, userState, sessionState);
            return Session.Create(s.Id, s.AppName, s.UserId, mergedState);
        }).ToList();

        return new ListSessionsResponse { Sessions = sessions };
    }

    public override async Task DeleteSessionAsync(DeleteSessionRequest request)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var session = await db.Sessions.FirstOrDefaultAsync(s =>
            s.AppName == request.AppName &&
            s.UserId == request.UserId &&
            s.Id == request.SessionId);

        if (session != null)
        {
            db.Sessions.Remove(session); // Cascades to events
            await db.SaveChangesAsync();
        }
    }

    public override async Task<Event> AppendEventAsync(AppendEventRequest request)
    {
        var evt = request.Event;
        if (evt.Partial == true)
            return evt;

        await using var db = await _dbFactory.CreateDbContextAsync();

        var session = await db.Sessions.FirstOrDefaultAsync(s =>
            s.AppName == request.Session.AppName &&
            s.UserId == request.Session.UserId &&
            s.Id == request.Session.Id)
            ?? throw new InvalidOperationException($"Session not found: {request.Session.Id}");

        evt = TrimTempDeltaState(evt);

        // Handle state deltas
        if (evt.Actions?.StateDelta != null)
        {
            var sessionState = JsonSerializer.Deserialize<Dictionary<string, object?>>(session.StateJson, JsonOptions) ?? new();
            var appState = await GetOrCreateAppStateAsync(db, request.Session.AppName);
            var userState = await GetOrCreateUserStateAsync(db, request.Session.AppName, request.Session.UserId);

            var appStateDict = JsonSerializer.Deserialize<Dictionary<string, object?>>(appState.StateJson, JsonOptions) ?? new();
            var userStateDict = JsonSerializer.Deserialize<Dictionary<string, object?>>(userState.StateJson, JsonOptions) ?? new();

            foreach (var (key, value) in evt.Actions.StateDelta)
            {
                if (key.StartsWith("app:"))
                    appStateDict[key] = value;
                else if (key.StartsWith("user:"))
                    userStateDict[key] = value;
                else if (!key.StartsWith("temp:"))
                    sessionState[key] = value;
            }

            session.StateJson = JsonSerializer.Serialize(sessionState, JsonOptions);
            appState.StateJson = JsonSerializer.Serialize(appStateDict, JsonOptions);
            userState.StateJson = JsonSerializer.Serialize(userStateDict, JsonOptions);
            appState.UpdateTime = DateTime.UtcNow;
            userState.UpdateTime = DateTime.UtcNow;
        }

        session.UpdateTime = DateTime.UtcNow;

        db.Events.Add(new StorageEvent
        {
            Id = evt.Id,
            AppName = request.Session.AppName,
            UserId = request.Session.UserId,
            SessionId = request.Session.Id,
            InvocationId = evt.InvocationId,
            Timestamp = evt.Timestamp,
            EventDataJson = JsonSerializer.Serialize(evt, JsonOptions),
        });

        await db.SaveChangesAsync();

        // Update the in-memory session
        request.Session.Events.Add(evt);
        return evt;
    }

    private static Dictionary<string, object?> ExtractSessionState(Dictionary<string, object?> state)
    {
        return state
            .Where(kvp => !kvp.Key.StartsWith("app:") && !kvp.Key.StartsWith("user:"))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    private static void MergeStateInto(Dictionary<string, object?> target, Dictionary<string, object?> source, string prefix)
    {
        foreach (var (key, value) in source)
        {
            if (key.StartsWith(prefix))
                target[key] = value;
        }
    }

    private static Dictionary<string, object?> MergeAllStates(
        Dictionary<string, object?> appState,
        Dictionary<string, object?> userState,
        Dictionary<string, object?> sessionState)
    {
        var merged = new Dictionary<string, object?>(appState);
        foreach (var (key, value) in userState)
            merged[key] = value;
        foreach (var (key, value) in sessionState)
            merged[key] = value;
        return merged;
    }

    private static async Task<Dictionary<string, object?>> GetAppStateAsync(AdkSessionDbContext db, string appName)
    {
        var appState = await db.AppStates.FindAsync(appName);
        return appState != null
            ? JsonSerializer.Deserialize<Dictionary<string, object?>>(appState.StateJson, JsonOptions) ?? new()
            : new();
    }

    private static async Task<Dictionary<string, object?>> GetUserStateAsync(AdkSessionDbContext db, string appName, string userId)
    {
        var userState = await db.UserStates.FindAsync(appName, userId);
        return userState != null
            ? JsonSerializer.Deserialize<Dictionary<string, object?>>(userState.StateJson, JsonOptions) ?? new()
            : new();
    }

    private static async Task<StorageAppState> GetOrCreateAppStateAsync(AdkSessionDbContext db, string appName)
    {
        var state = await db.AppStates.FindAsync(appName);
        if (state == null)
        {
            state = new StorageAppState { AppName = appName };
            db.AppStates.Add(state);
        }
        return state;
    }

    private static async Task<StorageUserState> GetOrCreateUserStateAsync(AdkSessionDbContext db, string appName, string userId)
    {
        var state = await db.UserStates.FindAsync(appName, userId);
        if (state == null)
        {
            state = new StorageUserState { AppName = appName, UserId = userId };
            db.UserStates.Add(state);
        }
        return state;
    }
}
