// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Agents;

namespace GoogleAdk.Core.Context;

/// <summary>
/// A simple context compactor that truncates the oldest events to remain
/// under a given threshold.
/// </summary>
public class TruncatingContextCompactor : IContextCompactor
{
    private readonly int _threshold;
    private readonly int _preserveLeadingEvents;

    /// <summary>
    /// Creates a TruncatingContextCompactor.
    /// </summary>
    /// <param name="threshold">The maximum number of events to retain.</param>
    /// <param name="preserveLeadingEvents">Keep the first N events (often useful for grounding prompts). Default: 0.</param>
    public TruncatingContextCompactor(int threshold, int preserveLeadingEvents = 0)
    {
        _threshold = threshold;
        _preserveLeadingEvents = preserveLeadingEvents;
    }

    public Task<bool> ShouldCompactAsync(InvocationContext invocationContext)
    {
        var eventsLength = invocationContext.Session.Events.Count;
        return Task.FromResult(eventsLength > _threshold + Math.Max(0, _preserveLeadingEvents));
    }

    public Task CompactAsync(InvocationContext invocationContext)
    {
        var events = invocationContext.Session.Events;
        var excess = events.Count - _threshold - Math.Max(0, _preserveLeadingEvents);
        if (excess <= 0)
            return Task.CompletedTask;

        var startIndexToRemove = Math.Max(0, _preserveLeadingEvents);
        events.RemoveRange(startIndexToRemove, excess);

        return Task.CompletedTask;
    }
}
