// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Abstractions.Sessions;
using GoogleAdk.Core.Abstractions.Tools;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Plugins;
using GoogleAdk.Core.Tools;

namespace GoogleAdk.Core.Tests;

public class PluginTests
{
    private static InvocationContext CreateTestInvocationContext()
    {
        return new InvocationContext
        {
            Session = Session.Create("s1", "app", "user"),
            Agent = new TestAgent("root"),
        };
    }

    // --- LoggingPlugin ---

    [Fact]
    public async Task LoggingPlugin_CapturesOnUserMessage()
    {
        var logs = new List<string>();
        var plugin = new LoggingPlugin(logAction: msg => logs.Add(msg));
        var ctx = CreateTestInvocationContext();

        await plugin.OnUserMessageCallbackAsync(ctx, new Content
        {
            Role = "user",
            Parts = new List<Part> { new() { Text = "hello" } }
        });

        Assert.True(logs.Count > 0);
        Assert.Contains(logs, l => l.Contains("USER MESSAGE"));
    }

    [Fact]
    public async Task LoggingPlugin_CapturesBeforeAndAfterRun()
    {
        var logs = new List<string>();
        var plugin = new LoggingPlugin(logAction: msg => logs.Add(msg));
        var ctx = CreateTestInvocationContext();

        await plugin.BeforeRunCallbackAsync(ctx);
        await plugin.AfterRunCallbackAsync(ctx);

        Assert.Contains(logs, l => l.Contains("INVOCATION STARTING"));
        Assert.Contains(logs, l => l.Contains("INVOCATION COMPLETED"));
    }

    [Fact]
    public async Task LoggingPlugin_CapturesOnEvent()
    {
        var logs = new List<string>();
        var plugin = new LoggingPlugin(logAction: msg => logs.Add(msg));
        var ctx = CreateTestInvocationContext();

        var evt = Event.Create(e =>
        {
            e.Author = "agent";
            e.Content = new Content { Role = "model", Parts = new List<Part> { new() { Text = "response" } } };
        });

        await plugin.OnEventCallbackAsync(ctx, evt);

        Assert.Contains(logs, l => l.Contains("EVENT YIELDED"));
    }

    [Fact]
    public async Task LoggingPlugin_ReturnsNull_DoesNotModify()
    {
        var plugin = new LoggingPlugin();
        var ctx = CreateTestInvocationContext();

        var result = await plugin.BeforeRunCallbackAsync(ctx);
        Assert.Null(result);

        var eventResult = await plugin.OnEventCallbackAsync(ctx, Event.Create());
        Assert.Null(eventResult);
    }

    // --- SecurityPlugin ---

    [Fact]
    public async Task SecurityPlugin_DefaultPolicy_AllowsAll()
    {
        var plugin = new SecurityPlugin();
        var ctx = CreateTestInvocationContext();
        var agentCtx = new AgentContext(ctx);
        agentCtx.FunctionCallId = "call-1";

        var tool = new FunctionTool("safe_tool", "test", (args, c) => Task.FromResult<object?>("ok"));
        var result = await plugin.BeforeToolCallbackAsync(tool, new Dictionary<string, object?>(), agentCtx);

        Assert.Null(result);
    }

    [Fact]
    public async Task SecurityPlugin_DenyPolicy_BlocksToolCall()
    {
        var policy = new AlwaysDenyPolicy();
        var plugin = new SecurityPlugin(policy);
        var ctx = CreateTestInvocationContext();
        var agentCtx = new AgentContext(ctx);
        agentCtx.FunctionCallId = "call-1";

        var tool = new FunctionTool("dangerous_tool", "test", (args, c) => Task.FromResult<object?>("ok"));
        var result = await plugin.BeforeToolCallbackAsync(tool, new Dictionary<string, object?>(), agentCtx);

        Assert.NotNull(result);
        Assert.True(result!.ContainsKey("error"));
    }

    [Fact]
    public async Task SecurityPlugin_ConfirmPolicy_RequestsConfirmation()
    {
        var policy = new AlwaysConfirmPolicy();
        var plugin = new SecurityPlugin(policy);
        var ctx = CreateTestInvocationContext();
        var agentCtx = new AgentContext(ctx);
        agentCtx.FunctionCallId = "call-1";

        var tool = new FunctionTool("sensitive_tool", "test", (args, c) => Task.FromResult<object?>("ok"));
        var result = await plugin.BeforeToolCallbackAsync(tool, new Dictionary<string, object?>(), agentCtx);

        Assert.NotNull(result);
        Assert.True(result!.ContainsKey("partial"));
    }

    // --- InMemoryPolicyEngine ---

    [Fact]
    public async Task InMemoryPolicyEngine_ReturnsAllow()
    {
        var engine = new InMemoryPolicyEngine();
        var tool = new FunctionTool("any_tool", "test", (args, c) => Task.FromResult<object?>("ok"));

        var result = await engine.EvaluateAsync(new ToolCallPolicyContext
        {
            Tool = tool,
            ToolArgs = new Dictionary<string, object?>()
        });

        Assert.Equal(PolicyOutcome.Allow, result.Outcome);
    }

    // --- PluginManager ---

    [Fact]
    public void PluginManager_RegistersPlugins()
    {
        var plugin = new LoggingPlugin();
        var manager = new PluginManager(new[] { plugin });

        Assert.NotNull(manager.GetPlugin("logging_plugin"));
    }

    [Fact]
    public void PluginManager_ThrowsOnDuplicate()
    {
        var manager = new PluginManager();
        manager.RegisterPlugin(new LoggingPlugin("p1"));

        Assert.Throws<InvalidOperationException>(() => manager.RegisterPlugin(new LoggingPlugin("p1")));
    }

    [Fact]
    public void PluginManager_GetPlugin_ReturnsNull_WhenNotFound()
    {
        var manager = new PluginManager();
        Assert.Null(manager.GetPlugin("nonexistent"));
    }

    // --- Test Policy Engines ---

    private class AlwaysDenyPolicy : IBasePolicyEngine
    {
        public Task<PolicyCheckResult> EvaluateAsync(ToolCallPolicyContext context)
            => Task.FromResult(new PolicyCheckResult { Outcome = PolicyOutcome.Deny, Reason = "Denied by test" });
    }

    private class AlwaysConfirmPolicy : IBasePolicyEngine
    {
        public Task<PolicyCheckResult> EvaluateAsync(ToolCallPolicyContext context)
            => Task.FromResult(new PolicyCheckResult { Outcome = PolicyOutcome.Confirm, Reason = "Confirm required" });
    }
}
