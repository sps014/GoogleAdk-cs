using System.Collections.Concurrent;
using System.Diagnostics;

namespace GoogleAdk.ApiServer;

/// <summary>
/// Collects Activity (span) data in memory, indexed by event ID and session ID,
/// matching the JS ADK's ApiServerSpanExporter + InMemoryExporter behavior.
/// </summary>
public sealed class InMemoryTraceCollector : IDisposable
{
    private readonly ActivityListener _listener;

    /// <summary>eventId → flat attributes dict (matches JS ApiServerSpanExporter.traceDict)</summary>
    private readonly ConcurrentDictionary<string, Dictionary<string, object?>> _traceByEvent = new();

    /// <summary>sessionId → list of trace IDs</summary>
    private readonly ConcurrentDictionary<string, ConcurrentBag<string>> _sessionTraceIds = new();

    /// <summary>All finished spans (for session trace lookup)</summary>
    private readonly ConcurrentBag<SpanRecord> _allSpans = new();

    public InMemoryTraceCollector()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "gcp.vertex.agent",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = OnActivityStopped,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    private void OnActivityStopped(Activity activity)
    {
        var opName = activity.OperationName;

        // Only index call_llm, send_data, execute_tool* spans for event trace
        // (matches JS ApiServerSpanExporter filter)
        if (opName == "call_llm" || opName == "send_data" || opName.StartsWith("execute_tool"))
        {
            var attributes = new Dictionary<string, object?>();
            foreach (var tag in activity.Tags)
            {
                attributes[tag.Key] = tag.Value;
            }

            // Add span/trace IDs into the attributes dict (matches JS format)
            attributes["trace_id"] = activity.TraceId.ToString();
            attributes["span_id"] = activity.SpanId.ToString();

            var eventId = activity.GetTagItem("gcp.vertex.agent.event_id") as string;
            if (eventId != null)
            {
                _traceByEvent[eventId] = attributes;
            }
        }

        // Track session → trace IDs for call_llm spans (matches JS InMemoryExporter)
        if (opName == "call_llm")
        {
            var sessionId = activity.GetTagItem("gcp.vertex.agent.session_id") as string
                         ?? activity.GetTagItem("gen_ai.conversation.id") as string;
            if (sessionId != null)
            {
                var bag = _sessionTraceIds.GetOrAdd(sessionId, _ => new ConcurrentBag<string>());
                bag.Add(activity.TraceId.ToString());
            }
        }

        // Store all spans for session trace lookup
        _allSpans.Add(new SpanRecord
        {
            Name = opName,
            SpanId = activity.SpanId.ToString(),
            TraceId = activity.TraceId.ToString(),
            StartTime = activity.StartTimeUtc.Ticks * 100,
            EndTime = (activity.StartTimeUtc + activity.Duration).Ticks * 100,
            ParentSpanId = activity.ParentSpanId == default ? null : activity.ParentSpanId.ToString(),
            Attributes = activity.Tags.ToDictionary(t => t.Key, t => (object?)t.Value),
        });
    }

    /// <summary>Get flat attributes dict for a specific event ID (matches JS traceDict[eventId]).</summary>
    public Dictionary<string, object?>? GetTraceByEventId(string eventId)
    {
        return _traceByEvent.TryGetValue(eventId, out var trace) ? trace : null;
    }

    /// <summary>Get all spans for a session, ordered by start time (matches JS InMemoryExporter.getFinishedSpans).</summary>
    public List<Dictionary<string, object?>> GetSpansBySessionId(string sessionId)
    {
        if (!_sessionTraceIds.TryGetValue(sessionId, out var traceIdBag))
            return [];

        var traceIds = new HashSet<string>(traceIdBag);
        return _allSpans
            .Where(s => traceIds.Contains(s.TraceId))
            .OrderBy(s => s.StartTime)
            .Select(s => new Dictionary<string, object?>
            {
                ["name"] = s.Name,
                ["span_id"] = s.SpanId,
                ["trace_id"] = s.TraceId,
                ["start_time"] = s.StartTime,
                ["end_time"] = s.EndTime,
                ["attributes"] = s.Attributes,
                ["parent_span_id"] = s.ParentSpanId,
            })
            .ToList();
    }

    public void Dispose()
    {
        _listener.Dispose();
    }

    private sealed class SpanRecord
    {
        public required string Name { get; init; }
        public required string SpanId { get; init; }
        public required string TraceId { get; init; }
        public required long StartTime { get; init; }
        public required long EndTime { get; init; }
        public required string? ParentSpanId { get; init; }
        public required Dictionary<string, object?> Attributes { get; init; }
    }
}
