// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Tools;

namespace GoogleAdk.Core.Plugins;

/// <summary>
/// A plugin that logs important information at each callback point.
/// Useful for terminal-based debugging by showing all critical events in the console.
/// </summary>
public class LoggingPlugin : BasePlugin
{
    private readonly Action<string>? _logAction;

    public LoggingPlugin(string name = "logging_plugin", Action<string>? logAction = null)
        : base(name)
    {
        _logAction = logAction;
    }

    public override Task<Content?> OnUserMessageCallbackAsync(InvocationContext invocationContext, Content userMessage)
    {
        Log("USER MESSAGE RECEIVED");
        Log($"   Invocation ID: {invocationContext.InvocationId}");
        Log($"   Session ID: {invocationContext.Session.Id}");
        Log($"   User ID: {invocationContext.UserId}");
        Log($"   App Name: {invocationContext.AppName}");
        Log($"   Root Agent: {invocationContext.Agent.Name}");
        Log($"   User Content: {FormatContent(userMessage)}");
        if (invocationContext.Branch != null)
            Log($"   Branch: {invocationContext.Branch}");
        return Task.FromResult<Content?>(null);
    }

    public override Task<Content?> BeforeRunCallbackAsync(InvocationContext invocationContext)
    {
        Log("INVOCATION STARTING");
        Log($"   Invocation ID: {invocationContext.InvocationId}");
        Log($"   Starting Agent: {invocationContext.Agent.Name}");
        return Task.FromResult<Content?>(null);
    }

    public override Task<Event?> OnEventCallbackAsync(InvocationContext invocationContext, Event evt)
    {
        Log("EVENT YIELDED");
        Log($"   Event ID: {evt.Id}");
        Log($"   Author: {evt.Author}");
        Log($"   Content: {FormatContent(evt.Content)}");
        Log($"   Final Response: {evt.IsFinalResponse()}");

        var functionCalls = evt.GetFunctionCalls();
        if (functionCalls.Count > 0)
            Log($"   Function Calls: {string.Join(", ", functionCalls.Select(fc => fc.Name))}");

        var functionResponses = evt.GetFunctionResponses();
        if (functionResponses.Count > 0)
            Log($"   Function Responses: {string.Join(", ", functionResponses.Select(fr => fr.Name))}");

        if (evt.LongRunningToolIds is { Count: > 0 })
            Log($"   Long Running Tools: {string.Join(", ", evt.LongRunningToolIds)}");

        return Task.FromResult<Event?>(null);
    }

    public override Task AfterRunCallbackAsync(InvocationContext invocationContext)
    {
        Log("INVOCATION COMPLETED");
        Log($"   Invocation ID: {invocationContext.InvocationId}");
        Log($"   Final Agent: {invocationContext.Agent.Name}");
        return Task.CompletedTask;
    }

    public override Task<Content?> BeforeAgentCallbackAsync(BaseAgent agent, AgentContext callbackContext)
    {
        Log("AGENT STARTING");
        Log($"   Agent Name: {agent.Name}");
        Log($"   Invocation ID: {callbackContext.InvocationContext.InvocationId}");
        return Task.FromResult<Content?>(null);
    }

    public override Task<Content?> AfterAgentCallbackAsync(BaseAgent agent, AgentContext callbackContext)
    {
        Log("AGENT COMPLETED");
        Log($"   Agent Name: {agent.Name}");
        Log($"   Invocation ID: {callbackContext.InvocationContext.InvocationId}");
        return Task.FromResult<Content?>(null);
    }

    public override Task<LlmResponse?> BeforeModelCallbackAsync(AgentContext callbackContext, LlmRequest llmRequest)
    {
        Log("LLM REQUEST");
        Log($"   Model: {llmRequest.Model ?? "default"}");

        if (llmRequest.Config?.SystemInstruction != null)
        {
            var instruction = llmRequest.Config.SystemInstruction;
            if (instruction.Length > 200)
                instruction = instruction[..200] + "...";
            Log($"   System Instruction: '{instruction}'");
        }

        if (llmRequest.ToolsDict.Count > 0)
            Log($"   Available Tools: {string.Join(", ", llmRequest.ToolsDict.Keys)}");

        return Task.FromResult<LlmResponse?>(null);
    }

    public override Task<LlmResponse?> AfterModelCallbackAsync(AgentContext callbackContext, LlmResponse llmResponse)
    {
        Log("LLM RESPONSE");
        if (!string.IsNullOrEmpty(llmResponse.ErrorCode))
        {
            Log($"   ERROR - Code: {llmResponse.ErrorCode}");
            Log($"   Error Message: {llmResponse.ErrorMessage}");
        }
        else
        {
            Log($"   Content: {FormatContent(llmResponse.Content)}");
            if (llmResponse.Partial == true)
                Log($"   Partial: {llmResponse.Partial}");
            if (llmResponse.TurnComplete != null)
                Log($"   Turn Complete: {llmResponse.TurnComplete}");
        }

        if (llmResponse.UsageMetadata != null)
        {
            Log($"   Token Usage - Input: {llmResponse.UsageMetadata.PromptTokenCount}, Output: {llmResponse.UsageMetadata.CandidatesTokenCount}");
        }

        return Task.FromResult<LlmResponse?>(null);
    }

    public override Task<LlmResponse?> OnModelErrorCallbackAsync(AgentContext callbackContext, LlmRequest llmRequest, Exception error)
    {
        Log("LLM ERROR");
        Log($"   Error: {error.Message}");
        return Task.FromResult<LlmResponse?>(null);
    }

    public override Task<Dictionary<string, object?>?> BeforeToolCallbackAsync(
        BaseTool tool, Dictionary<string, object?> toolArgs, AgentContext toolContext)
    {
        Log("TOOL STARTING");
        Log($"   Tool Name: {tool.Name}");
        Log($"   Function Call ID: {toolContext.FunctionCallId}");
        Log($"   Arguments: {FormatArgs(toolArgs)}");
        return Task.FromResult<Dictionary<string, object?>?>(null);
    }

    public override Task<Dictionary<string, object?>?> AfterToolCallbackAsync(
        BaseTool tool, Dictionary<string, object?> toolArgs, AgentContext toolContext, Dictionary<string, object?> result)
    {
        Log("TOOL COMPLETED");
        Log($"   Tool Name: {tool.Name}");
        Log($"   Function Call ID: {toolContext.FunctionCallId}");
        Log($"   Result: {FormatArgs(result)}");
        return Task.FromResult<Dictionary<string, object?>?>(null);
    }

    private void Log(string message)
    {
        var formatted = $"[{Name}] {message}";
        if (_logAction != null)
            _logAction(formatted);
        else
            Console.WriteLine(formatted);
    }

    private static string FormatContent(Content? content, int maxLength = 200)
    {
        if (content?.Parts == null || content.Parts.Count == 0)
            return "None";

        var parts = new List<string>();
        foreach (var part in content.Parts)
        {
            if (part.Text != null)
                parts.Add(part.Text.Length > maxLength ? part.Text[..maxLength] + "..." : part.Text);
            else if (part.FunctionCall != null)
                parts.Add($"[FunctionCall: {part.FunctionCall.Name}]");
            else if (part.FunctionResponse != null)
                parts.Add($"[FunctionResponse: {part.FunctionResponse.Name}]");
            else if (part.InlineData != null)
                parts.Add($"[InlineData: {part.InlineData.MimeType}]");
        }

        return string.Join(", ", parts);
    }

    private static string FormatArgs(Dictionary<string, object?> args, int maxLength = 200)
    {
        var str = System.Text.Json.JsonSerializer.Serialize(args);
        return str.Length > maxLength ? str[..maxLength] + "..." : str;
    }
}
