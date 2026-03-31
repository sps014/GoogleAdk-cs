// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Agents;

namespace GoogleAdk.Core.Plugins;

/// <summary>
/// Manages plugin registration and execution. Runs plugin callbacks in order,
/// implementing early-exit if any callback returns a non-null value.
/// </summary>
public class PluginManager
{
    private readonly List<BasePlugin> _plugins = new();

    public PluginManager(IEnumerable<BasePlugin>? plugins = null)
    {
        if (plugins != null)
        {
            foreach (var plugin in plugins)
                RegisterPlugin(plugin);
        }
    }

    public void RegisterPlugin(BasePlugin plugin)
    {
        if (_plugins.Any(p => p.Name == plugin.Name))
            throw new InvalidOperationException($"Plugin with name '{plugin.Name}' already registered.");

        _plugins.Add(plugin);
    }

    public BasePlugin? GetPlugin(string name) => _plugins.FirstOrDefault(p => p.Name == name);

    public async Task<Content?> RunOnUserMessageCallbackAsync(InvocationContext invocationContext, Content userMessage)
    {
        foreach (var plugin in _plugins)
        {
            var result = await plugin.OnUserMessageCallbackAsync(invocationContext, userMessage);
            if (result != null) return result;
        }
        return null;
    }

    public async Task<Content?> RunBeforeRunCallbackAsync(InvocationContext invocationContext)
    {
        foreach (var plugin in _plugins)
        {
            var result = await plugin.BeforeRunCallbackAsync(invocationContext);
            if (result != null) return result;
        }
        return null;
    }

    public async Task<Event?> RunOnEventCallbackAsync(InvocationContext invocationContext, Event evt)
    {
        foreach (var plugin in _plugins)
        {
            var result = await plugin.OnEventCallbackAsync(invocationContext, evt);
            if (result != null) return result;
        }
        return null;
    }

    public async Task RunAfterRunCallbackAsync(InvocationContext invocationContext)
    {
        foreach (var plugin in _plugins)
        {
            await plugin.AfterRunCallbackAsync(invocationContext);
        }
    }

    public async Task<Content?> RunBeforeAgentCallbackAsync(BaseAgent agent, AgentContext callbackContext)
    {
        foreach (var plugin in _plugins)
        {
            var result = await plugin.BeforeAgentCallbackAsync(agent, callbackContext);
            if (result != null) return result;
        }
        return null;
    }

    public async Task<Content?> RunAfterAgentCallbackAsync(BaseAgent agent, AgentContext callbackContext)
    {
        foreach (var plugin in _plugins)
        {
            var result = await plugin.AfterAgentCallbackAsync(agent, callbackContext);
            if (result != null) return result;
        }
        return null;
    }

    public async Task<LlmResponse?> RunBeforeModelCallbackAsync(AgentContext callbackContext, LlmRequest llmRequest)
    {
        foreach (var plugin in _plugins)
        {
            var result = await plugin.BeforeModelCallbackAsync(callbackContext, llmRequest);
            if (result != null) return result;
        }
        return null;
    }

    public async Task<LlmResponse?> RunAfterModelCallbackAsync(AgentContext callbackContext, LlmResponse llmResponse)
    {
        foreach (var plugin in _plugins)
        {
            var result = await plugin.AfterModelCallbackAsync(callbackContext, llmResponse);
            if (result != null) return result;
        }
        return null;
    }

    public async Task<LlmResponse?> RunOnModelErrorCallbackAsync(AgentContext callbackContext, LlmRequest llmRequest, Exception error)
    {
        foreach (var plugin in _plugins)
        {
            var result = await plugin.OnModelErrorCallbackAsync(callbackContext, llmRequest, error);
            if (result != null) return result;
        }
        return null;
    }

    public async Task<Dictionary<string, object?>?> RunBeforeToolCallbackAsync(
        BaseTool tool, Dictionary<string, object?> toolArgs, AgentContext toolContext)
    {
        foreach (var plugin in _plugins)
        {
            var result = await plugin.BeforeToolCallbackAsync(tool, toolArgs, toolContext);
            if (result != null) return result;
        }
        return null;
    }

    public async Task<Dictionary<string, object?>?> RunAfterToolCallbackAsync(
        BaseTool tool, Dictionary<string, object?> toolArgs, AgentContext toolContext, Dictionary<string, object?> result)
    {
        foreach (var plugin in _plugins)
        {
            var pluginResult = await plugin.AfterToolCallbackAsync(tool, toolArgs, toolContext, result);
            if (pluginResult != null) return pluginResult;
        }
        return null;
    }
}
