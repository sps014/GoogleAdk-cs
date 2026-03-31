// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Abstractions.Sessions;
using GoogleAdk.Core.Sessions;

namespace GoogleAdk.Core.Tests;

public class SessionTests
{
    [Fact]
    public async Task CreateSession_ReturnsSessionWithId()
    {
        var service = new InMemorySessionService();
        var session = await service.CreateSessionAsync(new CreateSessionRequest
        {
            AppName = "test-app",
            UserId = "user-1"
        });

        Assert.NotNull(session);
        Assert.Equal("test-app", session.AppName);
        Assert.Equal("user-1", session.UserId);
        Assert.NotNull(session.Id);
    }

    [Fact]
    public async Task GetSession_ReturnsCreatedSession()
    {
        var service = new InMemorySessionService();
        var created = await service.CreateSessionAsync(new CreateSessionRequest
        {
            AppName = "test-app",
            UserId = "user-1"
        });

        var retrieved = await service.GetSessionAsync(new GetSessionRequest
        {
            AppName = "test-app",
            UserId = "user-1",
            SessionId = created.Id
        });

        Assert.NotNull(retrieved);
        Assert.Equal(created.Id, retrieved!.Id);
    }

    [Fact]
    public async Task AppendEvent_AddsToSession()
    {
        var service = new InMemorySessionService();
        var session = await service.CreateSessionAsync(new CreateSessionRequest
        {
            AppName = "test-app",
            UserId = "user-1"
        });

        var evt = Event.Create(e =>
        {
            e.Author = "user";
            e.Content = new Content
            {
                Role = "user",
                Parts = new List<Part> { new Part { Text = "Hello" } }
            };
        });

        await service.AppendEventAsync(new AppendEventRequest
        {
            Session = session,
            Event = evt
        });

        Assert.Single(session.Events);
        Assert.Equal("Hello", session.Events[0].Content?.Parts?[0].Text);
    }

    [Fact]
    public async Task DeleteSession_RemovesSession()
    {
        var service = new InMemorySessionService();
        var session = await service.CreateSessionAsync(new CreateSessionRequest
        {
            AppName = "test-app",
            UserId = "user-1"
        });

        await service.DeleteSessionAsync(new DeleteSessionRequest
        {
            AppName = "test-app",
            UserId = "user-1",
            SessionId = session.Id
        });

        var retrieved = await service.GetSessionAsync(new GetSessionRequest
        {
            AppName = "test-app",
            UserId = "user-1",
            SessionId = session.Id
        });

        Assert.Null(retrieved);
    }

    [Fact]
    public void State_SetAndGet()
    {
        var state = new State(new Dictionary<string, object?>(), new Dictionary<string, object?>());
        state.Set("key1", "value1");
        state.Set("key2", 42);

        Assert.Equal("value1", state.Get<string>("key1"));
        Assert.Equal(42, state.Get<int>("key2"));
        Assert.True(state.Has("key1"));
        Assert.False(state.Has("nonexistent"));
    }

    [Fact]
    public void State_DeltaTracking()
    {
        var baseState = new Dictionary<string, object?> { ["existing"] = "old" };
        var delta = new Dictionary<string, object?>();
        var state = new State(baseState, delta);

        state.Set("new_key", "new_value");

        var stateDelta = state.GetDelta();
        Assert.True(stateDelta.ContainsKey("new_key"));
        Assert.False(stateDelta.ContainsKey("existing"));
    }
}
