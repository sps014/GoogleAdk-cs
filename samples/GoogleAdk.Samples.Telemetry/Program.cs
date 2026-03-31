// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

// ============================================================================
// Telemetry / Tracing Sample — OpenTelemetry Integration with ADK
// ============================================================================
//
// Demonstrates:
//   1. AdkTracing — System.Diagnostics.Activity-based tracing
//   2. TelemetrySetup — ActivityListener for span collection
//   3. Tracing agent invocations and tool calls
//   4. Integration pattern for OpenTelemetry SDK
// ============================================================================

using System.Diagnostics;
using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Telemetry;

Console.WriteLine("=== Telemetry / Tracing Sample ===\n");

// ── 1. Set up an ActivityListener ──────────────────────────────────────────

var capturedSpans = new List<Activity>();
using var listener = TelemetrySetup.CreateAdkActivityListener(
    onActivityStopped: activity =>
    {
        capturedSpans.Add(activity);
        Console.WriteLine($"  [SPAN] {activity.OperationName} " +
                          $"(Duration: {activity.Duration.TotalMilliseconds:F1}ms)");

        foreach (var tag in activity.Tags)
        {
            Console.WriteLine($"         {tag.Key} = {tag.Value}");
        }
    });

Console.WriteLine("ActivityListener registered for ADK tracing.\n");

// ── 2. Create spans using AdkTracing.ActivitySource ────────────────────────

Console.WriteLine("--- Creating sample spans ---\n");

// Simulate an agent invocation span
using (var agentSpan = AdkTracing.ActivitySource.StartActivity("invoke_agent"))
{
    if (agentSpan != null)
    {
        agentSpan.SetTag("gen_ai.agent.name", "weather_agent");
        agentSpan.SetTag("gen_ai.agent.description", "Provides weather forecasts");
        agentSpan.SetTag("gen_ai.conversation.id", "session-123");
        agentSpan.SetTag("gen_ai.operation.name", "invoke_agent");

        // Simulate a tool call span nested under the agent
        using (var toolSpan = AdkTracing.ActivitySource.StartActivity("execute_tool"))
        {
            if (toolSpan != null)
            {
                toolSpan.SetTag("gen_ai.tool.name", "get_weather");
                toolSpan.SetTag("gen_ai.tool.description", "Gets current weather");
                toolSpan.SetTag("gen_ai.tool.type", "FunctionTool");
                toolSpan.SetTag("gen_ai.tool.call.id", "call-456");
                toolSpan.SetTag("gen_ai.operation.name", "execute_tool");

                // Simulate some processing time
                await Task.Delay(10);
            }
        }

        // Simulate an LLM call span
        using (var llmSpan = AdkTracing.ActivitySource.StartActivity("call_llm"))
        {
            if (llmSpan != null)
            {
                llmSpan.SetTag("gen_ai.operation.name", "call_llm");
                llmSpan.SetTag("gen_ai.request.model", "gemini-2.5-flash");
                await Task.Delay(10);
            }
        }
    }
}

Console.WriteLine($"\nTotal spans captured: {capturedSpans.Count}");

// ── 3. Using AdkTracing static helpers ─────────────────────────────────────

Console.WriteLine("\n--- AdkTracing static helpers ---");
Console.WriteLine($"ActivitySource Name: {AdkTracing.ActivitySource.Name}");
Console.WriteLine($"ActivitySource Version: {AdkTracing.ActivitySource.Version}");

// ── 4. OpenTelemetry SDK Integration Pattern ───────────────────────────────

Console.WriteLine("\n--- OpenTelemetry SDK Integration ---");
Console.WriteLine("For production, configure via the OpenTelemetry .NET SDK:");
Console.WriteLine(@"
  builder.Services.AddOpenTelemetry()
      .WithTracing(tracing => tracing
          .AddSource(AdkTracing.ActivitySource.Name)
          .AddOtlpExporter());
");

Console.WriteLine("=== Telemetry Sample Complete ===");
