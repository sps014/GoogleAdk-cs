// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

// ============================================================================
// Context Compaction Sample — Managing Long Conversation Histories
// ============================================================================
//
// Demonstrates:
//   1. TruncatingContextCompactor — drops oldest events beyond a threshold
//   2. TokenBasedContextCompactor — summarizes old events via an LLM
//   3. CompactedEvent — the synthesized summary event type
// ============================================================================

using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Abstractions.Sessions;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Context;
using GoogleAdk.Core.Events;

Console.WriteLine("=== Context Compaction Sample ===\n");

// ── Helper: create a test invocation context with events ───────────────────

static InvocationContext CreateContextWithEvents(int eventCount)
{
    var session = Session.Create("s1", "app", "user");
    for (int i = 0; i < eventCount; i++)
    {
        session.Events.Add(Event.Create(e =>
        {
            e.Author = i % 2 == 0 ? "user" : "model";
            e.Content = new Content
            {
                Role = i % 2 == 0 ? "user" : "model",
                Parts = new List<Part> { new() { Text = $"Message {i + 1}: Lorem ipsum dolor sit amet." } }
            };
        }));
    }

    return new InvocationContext
    {
        Session = session,
        Agent = null!,  // Not needed for compaction demo
    };
}

// ── 1. TruncatingContextCompactor ──────────────────────────────────────────

Console.WriteLine("--- TruncatingContextCompactor ---");

var truncator = new TruncatingContextCompactor(threshold: 5, preserveLeadingEvents: 1);

var ctx1 = CreateContextWithEvents(10);
Console.WriteLine($"Before: {ctx1.Session.Events.Count} events");

var shouldCompact = await truncator.ShouldCompactAsync(ctx1);
Console.WriteLine($"Should compact (threshold=5, preserve=1): {shouldCompact}");

if (shouldCompact)
{
    await truncator.CompactAsync(ctx1);
    Console.WriteLine($"After:  {ctx1.Session.Events.Count} events");
    Console.WriteLine($"First event preserved: \"{ctx1.Session.Events[0].Content?.Parts?[0].Text}\"");
}

// Small context — no compaction needed
var ctx2 = CreateContextWithEvents(3);
shouldCompact = await truncator.ShouldCompactAsync(ctx2);
Console.WriteLine($"\nSmall context (3 events): should compact = {shouldCompact}");

// ── 2. CompactedEvent ──────────────────────────────────────────────────────

Console.WriteLine("\n--- CompactedEvent ---");

var compacted = CompactedEvent.CreateCompacted(evt =>
{
    evt.Author = "system";
    evt.Content = new Content
    {
        Role = "model",
        Parts = new List<Part> { new() { Text = "Summary: User asked about weather. Agent provided forecast." } }
    };
    evt.StartTime = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeMilliseconds();
    evt.EndTime = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeMilliseconds();
    evt.CompactedContent = "Summary: User asked about weather. Agent provided forecast.";
});

Console.WriteLine($"CompactedEvent ID: {compacted.Id}");
Console.WriteLine($"Is Compacted: {compacted.IsCompacted}");
Console.WriteLine($"Content: {compacted.CompactedContent}");

var normalEvent = Event.Create();
Console.WriteLine($"\nNormal event is compacted: {CompactedEvent.IsCompactedEvent(normalEvent)}");
Console.WriteLine($"Compacted event is compacted: {CompactedEvent.IsCompactedEvent(compacted)}");

// ── 3. TokenBasedContextCompactor (with mock summarizer) ───────────────────

Console.WriteLine("\n--- TokenBasedContextCompactor ---");
Console.WriteLine("(Requires IBaseSummarizer + LLM — see LlmSummarizer for production use)");
Console.WriteLine("TokenBasedContextCompactor accepts:");
Console.WriteLine("  - tokenThreshold: max tokens before compaction triggers");
Console.WriteLine("  - eventRetentionSize: number of recent events to preserve");
Console.WriteLine("  - summarizer: IBaseSummarizer implementation (e.g., LlmSummarizer)");

Console.WriteLine("\n=== Context Compaction Sample Complete ===");
