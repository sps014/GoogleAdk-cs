// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;

namespace GoogleAdk.Core.Tests;

public class EventTests
{
    [Fact]
    public void Event_Create_GeneratesId()
    {
        var evt = Event.Create();
        Assert.NotNull(evt.Id);
        Assert.NotEmpty(evt.Id);
        Assert.True(evt.Timestamp > 0);
    }

    [Fact]
    public void Event_Create_WithConfigure()
    {
        var evt = Event.Create(e =>
        {
            e.Author = "test";
            e.Content = new Content
            {
                Role = "model",
                Parts = new List<Part> { new Part { Text = "hello" } }
            };
        });

        Assert.Equal("test", evt.Author);
        Assert.Equal("hello", evt.Content?.Parts?[0].Text);
    }

    [Fact]
    public void Event_IsFinalResponse_TrueForTextOnly()
    {
        var evt = Event.Create(e =>
        {
            e.Content = new Content
            {
                Role = "model",
                Parts = new List<Part> { new Part { Text = "done" } }
            };
        });

        Assert.True(evt.IsFinalResponse());
    }

    [Fact]
    public void Event_IsFinalResponse_FalseForFunctionCall()
    {
        var evt = Event.Create(e =>
        {
            e.Content = new Content
            {
                Role = "model",
                Parts = new List<Part>
                {
                    new Part
                    {
                        FunctionCall = new FunctionCall { Name = "search", Args = new() }
                    }
                }
            };
        });

        Assert.False(evt.IsFinalResponse());
    }

    [Fact]
    public void Event_GetFunctionCalls_ReturnsAll()
    {
        var evt = Event.Create(e =>
        {
            e.Content = new Content
            {
                Role = "model",
                Parts = new List<Part>
                {
                    new Part { FunctionCall = new FunctionCall { Name = "fn1" } },
                    new Part { Text = "some text" },
                    new Part { FunctionCall = new FunctionCall { Name = "fn2" } },
                }
            };
        });

        var calls = evt.GetFunctionCalls();
        Assert.Equal(2, calls.Count);
        Assert.Equal("fn1", calls[0].Name);
        Assert.Equal("fn2", calls[1].Name);
    }

    [Fact]
    public void EventActions_Merge()
    {
        var target = new EventActions
        {
            StateDelta = new Dictionary<string, object?> { ["a"] = 1 }
        };

        var source = new EventActions
        {
            StateDelta = new Dictionary<string, object?> { ["b"] = 2 },
            Escalate = true,
        };

        var merged = EventActions.Merge(new[] { source }, target);

        Assert.Equal(1, merged.StateDelta!["a"]);
        Assert.Equal(2, merged.StateDelta!["b"]);
        Assert.True(merged.Escalate);
    }
}
