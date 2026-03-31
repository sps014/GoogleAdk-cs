// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Tools;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Tools;

namespace GoogleAdk.Core.Plugins;

/// <summary>
/// The outcome of a policy check.
/// </summary>
public enum PolicyOutcome
{
    /// <summary>The tool call is rejected by the policy engine.</summary>
    Deny,
    /// <summary>The tool call needs external confirmation before proceeding.</summary>
    Confirm,
    /// <summary>The tool call is allowed by the policy engine.</summary>
    Allow
}

/// <summary>
/// The result of a policy check.
/// </summary>
public class PolicyCheckResult
{
    public PolicyOutcome Outcome { get; set; }
    public string? Reason { get; set; }
}

/// <summary>
/// Context for a tool call policy evaluation.
/// </summary>
public class ToolCallPolicyContext
{
    public BaseTool Tool { get; set; } = null!;
    public Dictionary<string, object?> ToolArgs { get; set; } = new();
}

/// <summary>
/// Interface for policy engines that evaluate tool calls.
/// </summary>
public interface IBasePolicyEngine
{
    Task<PolicyCheckResult> EvaluateAsync(ToolCallPolicyContext context);
}

/// <summary>
/// Default permissive policy engine that allows all tool calls.
/// </summary>
public class InMemoryPolicyEngine : IBasePolicyEngine
{
    public Task<PolicyCheckResult> EvaluateAsync(ToolCallPolicyContext context)
    {
        return Task.FromResult(new PolicyCheckResult
        {
            Outcome = PolicyOutcome.Allow,
            Reason = "For prototyping purpose, all tool calls are allowed."
        });
    }
}

/// <summary>
/// Security plugin that evaluates tool calls against a policy engine.
/// Supports deny, confirm (with tool confirmation flow), and allow outcomes.
/// </summary>
public class SecurityPlugin : BasePlugin
{
    private const string ToolCallSecurityCheckStates = "orcas_tool_call_security_check_states";
    private const string IntermediateRequireConfirmationError = "This tool call needs external confirmation before completion.";

    private readonly IBasePolicyEngine _policyEngine;

    public SecurityPlugin(IBasePolicyEngine? policyEngine = null)
        : base("security_plugin")
    {
        _policyEngine = policyEngine ?? new InMemoryPolicyEngine();
    }

    public override async Task<Dictionary<string, object?>?> BeforeToolCallbackAsync(
        BaseTool tool, Dictionary<string, object?> toolArgs, AgentContext toolContext)
    {
        var toolCallCheckState = GetToolCallCheckState(toolContext);

        // Only check the policy once when the tool call is first handled
        if (toolCallCheckState == null)
            return await CheckToolCallPolicyAsync(tool, toolArgs, toolContext);

        if (toolCallCheckState is not string stateStr || stateStr != PolicyOutcome.Confirm.ToString())
            return null;

        // Waiting for confirmation
        var confirmations = toolContext.EventActions.RequestedToolConfirmations;
        if (!confirmations.TryGetValue(toolContext.FunctionCallId ?? "", out var confirmation))
            return new Dictionary<string, object?> { ["partial"] = IntermediateRequireConfirmationError };

        SetToolCallCheckState(toolContext, confirmation.Accepted == true ? "confirmed" : "rejected");

        if (confirmation.Accepted != true)
            return new Dictionary<string, object?> { ["error"] = "Tool call rejected from confirmation flow." };

        return null;
    }

    private object? GetToolCallCheckState(AgentContext toolContext)
    {
        var functionCallId = toolContext.FunctionCallId;
        if (string.IsNullOrEmpty(functionCallId)) return null;

        var states = toolContext.State.Get<Dictionary<string, object?>>(ToolCallSecurityCheckStates);
        if (states == null) return null;

        states.TryGetValue(functionCallId, out var state);
        return state;
    }

    private void SetToolCallCheckState(AgentContext toolContext, object state)
    {
        var functionCallId = toolContext.FunctionCallId;
        if (string.IsNullOrEmpty(functionCallId)) return;

        var states = toolContext.State.Get<Dictionary<string, object?>>(ToolCallSecurityCheckStates)
                     ?? new Dictionary<string, object?>();
        states[functionCallId] = state;
        toolContext.State.Set(ToolCallSecurityCheckStates, states);
    }

    private async Task<Dictionary<string, object?>?> CheckToolCallPolicyAsync(
        BaseTool tool, Dictionary<string, object?> toolArgs, AgentContext toolContext)
    {
        var result = await _policyEngine.EvaluateAsync(new ToolCallPolicyContext
        {
            Tool = tool,
            ToolArgs = toolArgs
        });

        SetToolCallCheckState(toolContext, result.Outcome.ToString());

        return result.Outcome switch
        {
            PolicyOutcome.Deny => new Dictionary<string, object?>
            {
                ["error"] = $"This tool call is rejected by policy engine. Reason: {result.Reason}"
            },
            PolicyOutcome.Confirm => RequestConfirmation(tool, result, toolContext),
            _ => null
        };
    }

    private static Dictionary<string, object?> RequestConfirmation(
        BaseTool tool, PolicyCheckResult result, AgentContext toolContext)
    {
        var functionCallId = toolContext.FunctionCallId ?? "";
        toolContext.EventActions.RequestedToolConfirmations[functionCallId] = new ToolConfirmation
        {
            FunctionCallId = functionCallId,
        };

        return new Dictionary<string, object?>
        {
            ["partial"] = IntermediateRequireConfirmationError
        };
    }
}
