// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Abstractions.Sessions;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Context;
using GoogleAdk.Core.Events;

namespace GoogleAdk.Core.Tests;

public class ContextCompactionTests
{
    private static InvocationContext CreateContextWithEvents(int count)
    {
        var session = Session.Create("s1", "app", "user");
        for (int i = 0; i < count; i++)
        {
            session.Events.Add(Event.Create(e =>
            {
                e.Author = i % 2 == 0 ? "user" : "model";
                e.Content = new Content
                {
                    Role = i % 2 == 0 ? "user" : "model",
                    Parts = new List<Part> { new() { Text = $"Message {i}" } }
                };
            }));
        }

        return new InvocationContext
        {
            Session = session,
            Agent = new TestAgent("root"),
        };
    }

    // --- TruncatingContextCompactor ---

    [Fact]
    public async Task Truncating_ShouldCompact_WhenOverThreshold()
    {
        var compactor = new TruncatingContextCompactor(threshold: 5);
        var ctx = CreateContextWithEvents(10);

        Assert.True(await compactor.ShouldCompactAsync(ctx));
    }

    [Fact]
    public async Task Truncating_ShouldNotCompact_WhenUnderThreshold()
    {
        var compactor = new TruncatingContextCompactor(threshold: 10);
        var ctx = CreateContextWithEvents(5);

        Assert.False(await compactor.ShouldCompactAsync(ctx));
    }

    [Fact]
    public async Task Truncating_ReducesEventCount()
    {
        var compactor = new TruncatingContextCompactor(threshold: 3);
        var ctx = CreateContextWithEvents(10);

        await compactor.CompactAsync(ctx);

        Assert.Equal(3, ctx.Session.Events.Count);
    }

    [Fact]
    public async Task Truncating_PreservesLeadingEvents()
    {
        var compactor = new TruncatingContextCompactor(threshold: 3, preserveLeadingEvents: 2);
        var ctx = CreateContextWithEvents(10);

        var firstEventText = ctx.Session.Events[0].Content?.Parts?[0].Text;
        var secondEventText = ctx.Session.Events[1].Content?.Parts?[0].Text;

        await compactor.CompactAsync(ctx);

        Assert.Equal(5, ctx.Session.Events.Count); // 2 preserved + 3 threshold
        Assert.Equal(firstEventText, ctx.Session.Events[0].Content?.Parts?[0].Text);
        Assert.Equal(secondEventText, ctx.Session.Events[1].Content?.Parts?[0].Text);
    }

    [Fact]
    public async Task Truncating_NoOp_WhenAlreadyUnderLimit()
    {
        var compactor = new TruncatingContextCompactor(threshold: 20);
        var ctx = CreateContextWithEvents(5);

        await compactor.CompactAsync(ctx);

        Assert.Equal(5, ctx.Session.Events.Count);
    }

    // --- CompactedEvent ---

    [Fact]
    public void CompactedEvent_Create_HasDefaults()
    {
        var evt = CompactedEvent.CreateCompacted();

        Assert.NotNull(evt.Id);
        Assert.True(evt.IsCompacted);
        Assert.True(evt.Timestamp > 0);
        Assert.NotNull(evt.Actions);
    }

    [Fact]
    public void CompactedEvent_Create_WithConfigure()
    {
        var evt = CompactedEvent.CreateCompacted(e =>
        {
            e.Author = "system";
            e.CompactedContent = "Summary of events";
            e.StartTime = 1000;
            e.EndTime = 2000;
        });

        Assert.Equal("system", evt.Author);
        Assert.Equal("Summary of events", evt.CompactedContent);
        Assert.Equal(1000, evt.StartTime);
        Assert.Equal(2000, evt.EndTime);
    }

    [Fact]
    public void CompactedEvent_IsCompactedEvent_TrueForCompacted()
    {
        var evt = CompactedEvent.CreateCompacted();
        Assert.True(CompactedEvent.IsCompactedEvent(evt));
    }

    [Fact]
    public void CompactedEvent_IsCompactedEvent_FalseForNormal()
    {
        var evt = Event.Create();
        Assert.False(CompactedEvent.IsCompactedEvent(evt));
    }
}
