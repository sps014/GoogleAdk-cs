// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Agents;

namespace GoogleAdk.Core.Plugins;

/// <summary>
/// Base class for creating plugins.
/// Plugins provide a structured way to intercept and modify agent, tool, and
/// LLM behaviors at critical points. While agent callbacks apply to a particular agent,
/// plugins apply globally to all agents added in the runner.
/// </summary>
public abstract class BasePlugin
{
    public string Name { get; }

    protected BasePlugin(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Called when a user message is received before an invocation starts.
    /// Return a Content to replace the user message; null to proceed normally.
    /// </summary>
    public virtual Task<Content?> OnUserMessageCallbackAsync(InvocationContext invocationContext, Content userMessage)
        => Task.FromResult<Content?>(null);

    /// <summary>
    /// Called before the runner starts. Return Content to halt execution early.
    /// </summary>
    public virtual Task<Content?> BeforeRunCallbackAsync(InvocationContext invocationContext)
        => Task.FromResult<Content?>(null);

    /// <summary>
    /// Called after an event is yielded from the runner. Return an Event to replace it.
    /// </summary>
    public virtual Task<Event?> OnEventCallbackAsync(InvocationContext invocationContext, Event evt)
        => Task.FromResult<Event?>(null);

    /// <summary>
    /// Called after a runner run has completed.
    /// </summary>
    public virtual Task AfterRunCallbackAsync(InvocationContext invocationContext)
        => Task.CompletedTask;

    /// <summary>
    /// Called before an agent's primary logic is invoked.
    /// Return Content to short-circuit the agent's execution.
    /// </summary>
    public virtual Task<Content?> BeforeAgentCallbackAsync(BaseAgent agent, AgentContext callbackContext)
        => Task.FromResult<Content?>(null);

    /// <summary>
    /// Called after an agent's primary logic has completed.
    /// Return Content to replace the agent's result.
    /// </summary>
    public virtual Task<Content?> AfterAgentCallbackAsync(BaseAgent agent, AgentContext callbackContext)
        => Task.FromResult<Content?>(null);

    /// <summary>
    /// Called before a request is sent to the model.
    /// Return an LlmResponse to skip the actual model call.
    /// </summary>
    public virtual Task<LlmResponse?> BeforeModelCallbackAsync(AgentContext callbackContext, LlmRequest llmRequest)
        => Task.FromResult<LlmResponse?>(null);

    /// <summary>
    /// Called after a response is received from the model.
    /// Return an LlmResponse to replace the model's response.
    /// </summary>
    public virtual Task<LlmResponse?> AfterModelCallbackAsync(AgentContext callbackContext, LlmResponse llmResponse)
        => Task.FromResult<LlmResponse?>(null);

    /// <summary>
    /// Called when a model call encounters an error.
    /// Return an LlmResponse to recover gracefully.
    /// </summary>
    public virtual Task<LlmResponse?> OnModelErrorCallbackAsync(AgentContext callbackContext, LlmRequest llmRequest, Exception error)
        => Task.FromResult<LlmResponse?>(null);

    /// <summary>
    /// Called before a tool is called.
    /// Return a dictionary to skip the actual tool call and use this as the result.
    /// </summary>
    public virtual Task<Dictionary<string, object?>?> BeforeToolCallbackAsync(
        BaseTool tool, Dictionary<string, object?> toolArgs, AgentContext toolContext)
        => Task.FromResult<Dictionary<string, object?>?>(null);

    /// <summary>
    /// Called after a tool has been called.
    /// Return a dictionary to replace the tool's result.
    /// </summary>
    public virtual Task<Dictionary<string, object?>?> AfterToolCallbackAsync(
        BaseTool tool, Dictionary<string, object?> toolArgs, AgentContext toolContext, Dictionary<string, object?> result)
        => Task.FromResult<Dictionary<string, object?>?>(null);
}
