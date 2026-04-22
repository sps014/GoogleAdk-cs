using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Context.Summarizers;
using GoogleAdk.Core.Events;

namespace GoogleAdk.Core.Context;

/// <summary>
/// A context compactor that uses token count to determine when to compact events.
/// Oldest events are summarized into a CompactedEvent when the session
/// history exceeds the token threshold.
/// </summary>
public class TokenBasedContextCompactor : IContextCompactor
{
    private readonly int _tokenThreshold;
    private readonly int _eventRetentionSize;
    private readonly IBaseSummarizer _summarizer;

    public TokenBasedContextCompactor(int tokenThreshold, int eventRetentionSize, IBaseSummarizer summarizer)
    {
        _tokenThreshold = tokenThreshold;
        _eventRetentionSize = eventRetentionSize;
        _summarizer = summarizer;
    }

    public Task<bool> ShouldCompactAsync(InvocationContext invocationContext)
    {
        var events = invocationContext.Session.Events;
        var activeEvents = GetActiveEvents(events);
        var rawEvents = activeEvents.Where(e => !CompactedEvent.IsCompactedEvent(e)).ToList();

        if (rawEvents.Count <= _eventRetentionSize)
            return Task.FromResult(false);

        var totalTokens = activeEvents.Sum(GetEventTokens);
        return Task.FromResult(totalTokens > _tokenThreshold);
    }

    public async Task CompactAsync(InvocationContext invocationContext)
    {
        var events = invocationContext.Session.Events;
        var activeEvents = GetActiveEvents(events);
        var rawEvents = activeEvents.Where(e => !CompactedEvent.IsCompactedEvent(e)).ToList();

        if (rawEvents.Count <= _eventRetentionSize)
            return;

        // Determine the baseline index to retain from the active raw events.
        var retainStartIndex = Math.Max(0, rawEvents.Count - _eventRetentionSize);

        // Prevent splitting between a tool call and its response.
        while (retainStartIndex > 0)
        {
            var eventToRetain = rawEvents[retainStartIndex];
            var previousEvent = rawEvents[retainStartIndex - 1];

            if (HasFunctionResponse(eventToRetain) && HasFunctionCall(previousEvent))
                retainStartIndex--;
            else
                break;
        }

        if (retainStartIndex == 0)
            return;

        var rawEventsToCompact = rawEvents.Take(retainStartIndex).ToList();
        var compactedEventPresent = activeEvents.FirstOrDefault(CompactedEvent.IsCompactedEvent);

        var eventsToCompact = compactedEventPresent != null
            ? new List<Event> { compactedEventPresent }.Concat(rawEventsToCompact).ToList()
            : rawEventsToCompact;

        var compactedEvent = await _summarizer.SummarizeAsync(eventsToCompact);

        compactedEvent.Actions ??= new EventActions();
        invocationContext.Session.Events.Add(compactedEvent);
    }

    private static List<Event> GetActiveEvents(List<Event> events)
    {
        CompactedEvent? latest = null;

        for (int i = events.Count - 1; i >= 0; i--)
        {
            if (events[i] is CompactedEvent ce && ce.IsCompacted)
            {
                if (latest == null || ce.EndTime > latest.EndTime)
                    latest = ce;
            }
        }

        if (latest == null)
            return events;

        var activeRaw = events
            .Where(e => !CompactedEvent.IsCompactedEvent(e) && e.Timestamp > latest.EndTime)
            .ToList();

        return new List<Event> { latest }.Concat(activeRaw).ToList();
    }

    private static int GetEventTokens(Event evt)
    {
        if (evt is LlmResponse lr && lr.UsageMetadata?.PromptTokenCount != null)
            return lr.UsageMetadata.PromptTokenCount.Value;

        // Estimate: 4 chars per token
        var contentStr = StringifyContent(evt);
        return (int)Math.Ceiling(contentStr.Length / 4.0);
    }

    private static string StringifyContent(Event evt)
    {
        return evt.StringifyContent();
    }

    private static bool HasFunctionCall(Event evt)
        => evt.Content?.Parts?.Any(p => p.FunctionCall != null) == true;

    private static bool HasFunctionResponse(Event evt)
        => evt.Content?.Parts?.Any(p => p.FunctionResponse != null) == true;
}
