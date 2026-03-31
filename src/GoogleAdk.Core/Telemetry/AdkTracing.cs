// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text.Json;
using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Agents;

namespace GoogleAdk.Core.Telemetry;

/// <summary>
/// Provides tracing utilities for the Agent Development Kit using
/// OpenTelemetry-compatible System.Diagnostics.Activity spans.
///
/// Follows OpenTelemetry Semantic Conventions v1.37 for GenAI.
/// </summary>
public static class AdkTracing
{
    /// <summary>
    /// The ActivitySource for all ADK tracing.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new("gcp.vertex.agent", GetVersion());

    private const string GenAiAgentDescription = "gen_ai.agent.description";
    private const string GenAiAgentName = "gen_ai.agent.name";
    private const string GenAiConversationId = "gen_ai.conversation.id";
    private const string GenAiOperationName = "gen_ai.operation.name";
    private const string GenAiToolCallId = "gen_ai.tool.call.id";
    private const string GenAiToolDescription = "gen_ai.tool.description";
    private const string GenAiToolName = "gen_ai.tool.name";
    private const string GenAiToolType = "gen_ai.tool.type";

    /// <summary>
    /// Sets span attributes for an agent invocation.
    /// </summary>
    public static void TraceAgentInvocation(BaseAgent agent, InvocationContext invocationContext)
    {
        var activity = Activity.Current;
        if (activity == null) return;

        activity.SetTag(GenAiOperationName, "invoke_agent");
        activity.SetTag(GenAiAgentDescription, agent.Description);
        activity.SetTag(GenAiAgentName, agent.Name);
        activity.SetTag(GenAiConversationId, invocationContext.Session.Id);
    }

    /// <summary>
    /// Traces a tool call with arguments and response.
    /// </summary>
    public static void TraceToolCall(BaseTool tool, Dictionary<string, object?> args, Event functionResponseEvent)
    {
        var activity = Activity.Current;
        if (activity == null) return;

        activity.SetTag(GenAiOperationName, "execute_tool");
        activity.SetTag(GenAiToolDescription, tool.Description ?? "");
        activity.SetTag(GenAiToolName, tool.Name);
        activity.SetTag(GenAiToolType, tool.GetType().Name);
        activity.SetTag("gcp.vertex.agent.llm_request", "{}");
        activity.SetTag("gcp.vertex.agent.llm_response", "{}");
        activity.SetTag("gcp.vertex.agent.tool_call_args",
            ShouldAddRequestResponseToSpans() ? SafeJsonSerialize(args) : "{}");

        string toolCallId = "<not specified>";
        object? toolResponse = "<not specified>";

        if (functionResponseEvent.Content?.Parts != null && functionResponseEvent.Content.Parts.Count > 0)
        {
            var functionResponse = functionResponseEvent.Content.Parts[0].FunctionResponse;
            if (functionResponse?.Id != null)
                toolCallId = functionResponse.Id;
            if (functionResponse?.Response != null)
                toolResponse = functionResponse.Response;
        }

        activity.SetTag(GenAiToolCallId, toolCallId);
        activity.SetTag("gcp.vertex.agent.event_id", functionResponseEvent.Id);
        activity.SetTag("gcp.vertex.agent.tool_response",
            ShouldAddRequestResponseToSpans() ? SafeJsonSerialize(toolResponse) : "{}");
    }

    /// <summary>
    /// Traces merged tool call events (for web UI /debug/trace support).
    /// </summary>
    public static void TraceMergedToolCalls(string responseEventId, Event functionResponseEvent)
    {
        var activity = Activity.Current;
        if (activity == null) return;

        activity.SetTag(GenAiOperationName, "execute_tool");
        activity.SetTag(GenAiToolName, "(merged tools)");
        activity.SetTag(GenAiToolDescription, "(merged tools)");
        activity.SetTag(GenAiToolCallId, responseEventId);
        activity.SetTag("gcp.vertex.agent.tool_call_args", "N/A");
        activity.SetTag("gcp.vertex.agent.event_id", responseEventId);
        activity.SetTag("gcp.vertex.agent.llm_request", "{}");
        activity.SetTag("gcp.vertex.agent.llm_response", "{}");
        activity.SetTag("gcp.vertex.agent.tool_response",
            ShouldAddRequestResponseToSpans() ? SafeJsonSerialize(functionResponseEvent) : "{}");
    }

    /// <summary>
    /// Traces an LLM call with request and response.
    /// </summary>
    public static void TraceCallLlm(InvocationContext invocationContext, string eventId, LlmRequest llmRequest, LlmResponse llmResponse)
    {
        var activity = Activity.Current;
        if (activity == null) return;

        activity.SetTag(GenAiOperationName, "call_llm");
        activity.SetTag(GenAiAgentName, invocationContext.Agent.Name);
        activity.SetTag("gen_ai.system", "gcp.vertex.agent");
        activity.SetTag("gcp.vertex.agent.session_id", invocationContext.Session.Id);
        activity.SetTag("gcp.vertex.agent.llm_request",
            ShouldAddRequestResponseToSpans() ? SafeJsonSerialize(BuildLlmRequestForTrace(llmRequest)) : "{}");
        activity.SetTag("gcp.vertex.agent.llm_response",
            ShouldAddRequestResponseToSpans() ? SafeJsonSerialize(llmResponse) : "{}");
        activity.SetTag("gcp.vertex.agent.invocation_id", invocationContext.InvocationId);
        activity.SetTag("gcp.vertex.agent.event_id", eventId);
    }

    /// <summary>
    /// Starts a new activity (span) for the given operation.
    /// </summary>
    public static Activity? StartSpan(string operationName, ActivityKind kind = ActivityKind.Internal)
    {
        return ActivitySource.StartActivity(operationName, kind);
    }

    private static bool ShouldAddRequestResponseToSpans()
    {
        var envVar = Environment.GetEnvironmentVariable("ADK_TRACE_INCLUDE_DATA");
        return !string.Equals(envVar, "false", StringComparison.OrdinalIgnoreCase);
    }

    private static string SafeJsonSerialize(object? obj)
    {
        try
        {
            return JsonSerializer.Serialize(obj);
        }
        catch
        {
            return "<not serializable>";
        }
    }

    private static Dictionary<string, object?> BuildLlmRequestForTrace(LlmRequest llmRequest)
    {
        var result = new Dictionary<string, object?>
        {
            ["model"] = llmRequest.Model,
            ["contents"] = llmRequest.Contents.Select(c => new
            {
                role = c.Role,
                parts = c.Parts?.Where(p => p.InlineData == null).ToList()
            }).ToList()
        };

        if (llmRequest.Config != null)
        {
            result["config"] = new
            {
                systemInstruction = llmRequest.Config.SystemInstruction,
                tools = llmRequest.Config.Tools,
                temperature = llmRequest.Config.Temperature,
                topP = llmRequest.Config.TopP,
                topK = llmRequest.Config.TopK,
                maxOutputTokens = llmRequest.Config.MaxOutputTokens,
            };
        }

        return result;
    }

    private static string GetVersion()
    {
        return typeof(AdkTracing).Assembly.GetName().Version?.ToString() ?? "0.0.0";
    }
}
