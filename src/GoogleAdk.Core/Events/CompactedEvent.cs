// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Events;

namespace GoogleAdk.Core.Events;

/// <summary>
/// A specialized Event that represents a synthesized summary of past events.
/// Used to compress session history without losing critical context.
/// </summary>
public class CompactedEvent : Event
{
    /// <summary>Identifies this event as a compacted event.</summary>
    public bool IsCompacted { get; set; } = true;

    /// <summary>The start time of the context that was compacted.</summary>
    public long StartTime { get; set; }

    /// <summary>The end time of the context that was compacted.</summary>
    public long EndTime { get; set; }

    /// <summary>The summarized content of the compacted events.</summary>
    public string CompactedContent { get; set; } = string.Empty;

    /// <summary>
    /// Creates a new CompactedEvent with default values.
    /// </summary>
    public static CompactedEvent CreateCompacted(Action<CompactedEvent>? configure = null)
    {
        var evt = new CompactedEvent
        {
            Id = Event.Create().Id,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Actions = new EventActions(),
            IsCompacted = true,
        };
        configure?.Invoke(evt);
        return evt;
    }

    /// <summary>
    /// Checks whether a given event is a CompactedEvent.
    /// </summary>
    public static bool IsCompactedEvent(Event evt) => evt is CompactedEvent { IsCompacted: true };
}
