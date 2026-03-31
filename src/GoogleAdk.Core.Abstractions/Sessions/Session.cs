// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Events;

namespace GoogleAdk.Core.Abstractions.Sessions;

/// <summary>
/// Represents a session in a conversation between agents and users.
/// </summary>
public class Session
{
    /// <summary>The unique identifier of the session.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The name of the app.</summary>
    public string AppName { get; set; } = string.Empty;

    /// <summary>The id of the user.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>The state of the session.</summary>
    public Dictionary<string, object?> State { get; set; } = new();

    /// <summary>
    /// The events of the session, e.g. user input, model response, function call/response, etc.
    /// </summary>
    public List<Event> Events { get; set; } = new();

    /// <summary>The last update time of the session (Unix milliseconds).</summary>
    public long LastUpdateTime { get; set; }

    /// <summary>
    /// Creates a session with the required fields.
    /// </summary>
    public static Session Create(string id, string appName, string userId = "", Dictionary<string, object?>? state = null)
    {
        return new Session
        {
            Id = id,
            AppName = appName,
            UserId = userId,
            State = state ?? new Dictionary<string, object?>(),
            Events = new List<Event>(),
            LastUpdateTime = 0
        };
    }
}
