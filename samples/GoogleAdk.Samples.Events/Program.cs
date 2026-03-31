// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

// ============================================================================
// Structured Events Sample — CompactedEvent & Typed Event Parsing
// ============================================================================
//
// Demonstrates:
//   1. CompactedEvent — synthesized summary events
//   2. StructuredEvent types — Thought, Content, ToolCall, ToolResult, etc.
//   3. Event type identification
// ============================================================================

using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Events;

Console.WriteLine("=== Structured Events Sample ===\n");

// ── 1. CompactedEvent ──────────────────────────────────────────────────────

Console.WriteLine("--- CompactedEvent ---\n");

var compacted = CompactedEvent.CreateCompacted(evt =>
{
    evt.Author = "system";
    evt.Content = new Content
    {
        Role = "model",
        Parts = new List<Part>
        {
            new() { Text = "Summary: User asked about travel to Paris. Agent recommended flights and hotels." }
        }
    };
    evt.StartTime = DateTimeOffset.UtcNow.AddHours(-2).ToUnixTimeMilliseconds();
    evt.EndTime = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeMilliseconds();
    evt.CompactedContent = "Summary: User asked about travel to Paris. Agent recommended flights and hotels.";
});

Console.WriteLine($"Event ID: {compacted.Id}");
Console.WriteLine($"Is Compacted: {compacted.IsCompacted}");
Console.WriteLine($"Author: {compacted.Author}");
Console.WriteLine($"Summary: {compacted.CompactedContent}");
Console.WriteLine($"Time range: {DateTimeOffset.FromUnixTimeMilliseconds(compacted.StartTime):HH:mm} → {DateTimeOffset.FromUnixTimeMilliseconds(compacted.EndTime):HH:mm}");

// Check compacted vs normal events
var normalEvent = Event.Create(e => e.Author = "agent");
Console.WriteLine($"\nNormal event is compacted: {CompactedEvent.IsCompactedEvent(normalEvent)}");
Console.WriteLine($"Compacted event is compacted: {CompactedEvent.IsCompactedEvent(compacted)}");

// ── 2. StructuredEvent Types ───────────────────────────────────────────────

Console.WriteLine("\n--- StructuredEvent Types ---\n");

// Create various structured events
var thought = new ThoughtEvent("I should search for flight prices first.");
Console.WriteLine($"ThoughtEvent: type={thought.Type}, text=\"{thought.Text}\"");

var content = new ContentEvent("Here are the best flights to Paris:");
Console.WriteLine($"ContentEvent: type={content.Type}, text=\"{content.Text}\"");

var toolCall = new ToolCallEvent(new FunctionCall
{
    Name = "search_flights",
    Args = new Dictionary<string, object?> { ["destination"] = "Paris", ["date"] = "2026-04-15" }
});
Console.WriteLine($"ToolCallEvent: type={toolCall.Type}, function=\"{toolCall.Call.Name}\"");

var toolResult = new ToolResultEvent(new FunctionResponse
{
    Name = "search_flights",
    Response = new Dictionary<string, object?> { ["flights"] = 5, ["cheapest"] = "$450" }
});
Console.WriteLine($"ToolResultEvent: type={toolResult.Type}, function=\"{toolResult.Result.Name}\"");

var callCode = new CallCodeEvent("import requests\nresp = requests.get('https://api.example.com/flights')");
Console.WriteLine($"CallCodeEvent: type={callCode.Type}, code length={callCode.Code.Length}");

var codeResult = new CodeResultEvent(new CodeExecutionResult
{
    Output = "Found 5 flights",
    Outcome = "OUTCOME_OK"
});
Console.WriteLine($"CodeResultEvent: type={codeResult.Type}, output=\"{codeResult.Result.Output}\"");

var error = new ErrorEvent("Rate limit exceeded. Retrying in 5 seconds.");
Console.WriteLine($"ErrorEvent: type={error.Type}, message=\"{error.ErrorMessage}\"");

var activity = new ActivityEvent("loading", new Dictionary<string, object?> { ["progress"] = 75 });
Console.WriteLine($"ActivityEvent: type={activity.Type}, kind=\"{activity.Kind}\", detail=[{string.Join(", ", activity.Detail.Select(kv => $"{kv.Key}={kv.Value}"))}]");

var finished = new FinishedEvent(new { summary = "Task complete" });
Console.WriteLine($"FinishedEvent: type={finished.Type}, output={finished.Output}");

// ── 3. Event Type Enumeration ──────────────────────────────────────────────

Console.WriteLine("\n--- All Event Types ---\n");

foreach (var eventType in Enum.GetValues<EventType>())
    Console.WriteLine($"  {eventType}");

Console.WriteLine($"\nTotal event types: {Enum.GetValues<EventType>().Length}");

Console.WriteLine("\n=== Structured Events Sample Complete ===");
