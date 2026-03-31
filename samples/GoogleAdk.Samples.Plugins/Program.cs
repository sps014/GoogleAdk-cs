// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

// ============================================================================
// Plugins Sample — Security & Logging Plugins
// ============================================================================
//
// Demonstrates:
//   1. LoggingPlugin — logs all callback events to console
//   2. SecurityPlugin — policy-based tool call gating (deny/confirm/allow)
//   3. Custom IBasePolicyEngine — configurable deny list
//   4. PluginManager — registering multiple plugins
// ============================================================================

using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Abstractions.Sessions;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Plugins;
using GoogleAdk.Core.Tools;

Console.WriteLine("=== Plugins Sample ===\n");

// ── 1. LoggingPlugin ───────────────────────────────────────────────────────

Console.WriteLine("--- LoggingPlugin ---\n");

var logs = new List<string>();
var loggingPlugin = new LoggingPlugin(logAction: msg => logs.Add(msg));

// Simulate callbacks
var invocationContext = new InvocationContext
{
    Session = Session.Create("s1", "plugin-demo", "user-1"),
    Agent = null!,
};

var userMessage = new Content
{
    Role = "user",
    Parts = new List<Part> { new() { Text = "What's the weather?" } }
};

await loggingPlugin.OnUserMessageCallbackAsync(invocationContext, userMessage);
await loggingPlugin.BeforeRunCallbackAsync(invocationContext);

var sampleEvent = Event.Create(e =>
{
    e.Author = "weather_agent";
    e.Content = new Content
    {
        Role = "model",
        Parts = new List<Part> { new() { Text = "It's sunny and 72°F." } }
    };
});

await loggingPlugin.OnEventCallbackAsync(invocationContext, sampleEvent);
await loggingPlugin.AfterRunCallbackAsync(invocationContext);

Console.WriteLine($"Captured {logs.Count} log entries:");
foreach (var log in logs.Take(8))
    Console.WriteLine($"  {log}");
if (logs.Count > 8)
    Console.WriteLine($"  ... and {logs.Count - 8} more");

// ── 2. SecurityPlugin with Custom Policy Engine ────────────────────────────

Console.WriteLine("\n--- SecurityPlugin ---\n");

// A custom policy engine that denies "dangerous_tool" and confirms "sensitive_tool"
var customPolicy = new DenyListPolicyEngine(
    denyList: new[] { "dangerous_tool" },
    confirmList: new[] { "sensitive_tool" });

var securityPlugin = new SecurityPlugin(customPolicy);

// Simulate tool call checks
var safeContext = new AgentContext(invocationContext);

var safeTool = new FunctionTool("safe_tool", "A safe tool",
    (args, ctx) => Task.FromResult<object?>("ok"));

var beforeResult = await securityPlugin.BeforeToolCallbackAsync(
    safeTool, new Dictionary<string, object?>(), safeContext);
Console.WriteLine($"safe_tool: {(beforeResult == null ? "ALLOWED" : "BLOCKED")}");

var dangerousTool = new FunctionTool("dangerous_tool", "A dangerous tool",
    (args, ctx) => Task.FromResult<object?>("ok"));

var dangerousContext = new AgentContext(invocationContext);
dangerousContext.FunctionCallId = "call-1";
var dangerousResult = await securityPlugin.BeforeToolCallbackAsync(
    dangerousTool, new Dictionary<string, object?>(), dangerousContext);
Console.WriteLine($"dangerous_tool: {(dangerousResult?.ContainsKey("error") == true ? "DENIED" : "ALLOWED")}");
if (dangerousResult?.ContainsKey("error") == true)
    Console.WriteLine($"  Reason: {dangerousResult["error"]}");

var sensitiveTool = new FunctionTool("sensitive_tool", "A sensitive tool",
    (args, ctx) => Task.FromResult<object?>("ok"));

var sensitiveContext = new AgentContext(invocationContext);
sensitiveContext.FunctionCallId = "call-2";
var sensitiveResult = await securityPlugin.BeforeToolCallbackAsync(
    sensitiveTool, new Dictionary<string, object?>(), sensitiveContext);
Console.WriteLine($"sensitive_tool: {(sensitiveResult?.ContainsKey("partial") == true ? "NEEDS CONFIRMATION" : "ALLOWED")}");

// ── 3. PluginManager ──────────────────────────────────────────────────────

Console.WriteLine("\n--- PluginManager ---\n");

var pluginManager = new PluginManager(new BasePlugin[] { loggingPlugin, securityPlugin });

Console.WriteLine("Registered plugins:");
Console.WriteLine($"  - {pluginManager.GetPlugin("logging_plugin")?.Name}");
Console.WriteLine($"  - {pluginManager.GetPlugin("security_plugin")?.Name}");

Console.WriteLine("\n=== Plugins Sample Complete ===");

// ── Custom Policy Engine ───────────────────────────────────────────────────

public class DenyListPolicyEngine : IBasePolicyEngine
{
    private readonly HashSet<string> _denyList;
    private readonly HashSet<string> _confirmList;

    public DenyListPolicyEngine(IEnumerable<string> denyList, IEnumerable<string> confirmList)
    {
        _denyList = new HashSet<string>(denyList);
        _confirmList = new HashSet<string>(confirmList);
    }

    public Task<PolicyCheckResult> EvaluateAsync(ToolCallPolicyContext context)
    {
        var toolName = context.Tool.Name;

        if (_denyList.Contains(toolName))
            return Task.FromResult(new PolicyCheckResult
            {
                Outcome = PolicyOutcome.Deny,
                Reason = $"Tool '{toolName}' is on the deny list."
            });

        if (_confirmList.Contains(toolName))
            return Task.FromResult(new PolicyCheckResult
            {
                Outcome = PolicyOutcome.Confirm,
                Reason = $"Tool '{toolName}' requires confirmation."
            });

        return Task.FromResult(new PolicyCheckResult
        {
            Outcome = PolicyOutcome.Allow,
            Reason = "Tool allowed by default."
        });
    }
}
