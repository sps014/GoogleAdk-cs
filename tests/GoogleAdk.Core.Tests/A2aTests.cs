// Copyright 2026 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.A2a;
using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;

namespace GoogleAdk.Core.Tests;

public class A2aTests
{
    [Fact]
    public void PartConverter_RoundTripsTextAndThought()
    {
        var part = new Part { Text = "hello", Thought = true };
        var a2a = PartConverterUtils.ToA2aPart(part, new List<string>());
        var roundTrip = PartConverterUtils.ToPart(a2a);

        Assert.Equal("text", a2a.Kind);
        Assert.Equal("hello", roundTrip.Text);
        Assert.True(roundTrip.Thought);
    }

    [Fact]
    public void PartConverter_FileData_ToA2a()
    {
        var part = new Part
        {
            FileData = new FileData
            {
                FileUri = "https://example.com/file.png",
                MimeType = "image/png",
                Name = "file.png",
            },
        };

        var a2a = PartConverterUtils.ToA2aPart(part, new List<string>());
        Assert.Equal("file", a2a.Kind);
        Assert.Equal("https://example.com/file.png", a2a.File?.Uri);
        Assert.Equal("image/png", a2a.File?.MimeType);
        Assert.Equal("file.png", a2a.File?.Name);
    }

    [Fact]
    public void EventConverter_ToA2aMessage_UsesMetadata()
    {
        var evt = Event.Create(e =>
        {
            e.Author = "user";
            e.Content = new Content
            {
                Role = "user",
                Parts = new List<Part> { new Part { Text = "hi" } },
            };
        });

        var msg = EventConverterUtils.ToA2aMessage(evt, "app", "user-1", "session-1");
        Assert.Equal("message", msg.Kind);
        Assert.Equal("user", msg.Role);
        Assert.Equal("app", msg.Metadata?[A2aMetadataKeys.AppName]);
        Assert.Equal("user-1", msg.Metadata?[A2aMetadataKeys.UserId]);
        Assert.Equal("session-1", msg.Metadata?[A2aMetadataKeys.SessionId]);
    }
}

