// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Abstractions.Sessions;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace GoogleAdk.Core.Agents;

/// <summary>
/// Callback for before/after agent hooks.
/// Returns Content to short-circuit agent execution, or null to continue.
/// </summary>
public delegate Task<Content?> AgentCallback(AgentContext context);

/// <summary>
/// Configuration for creating a BaseAgent.
/// </summary>
public class BaseAgentConfig
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public BaseAgent? ParentAgent { get; set; }
    public List<BaseAgent>? SubAgents { get; set; }
    public List<AgentCallback>? BeforeAgentCallbacks { get; set; }
    public List<AgentCallback>? AfterAgentCallbacks { get; set; }
}

/// <summary>
/// Base class for all agents in the Agent Development Kit.
/// </summary>
public abstract class BaseAgent
{
    /// <summary>
    /// The agent's name. Must be a valid identifier and unique within the agent tree.
    /// Cannot be "user" (reserved for end-user input).
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Description about the agent's capability. Used by the model for delegation routing.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// The parent agent of this agent. An agent can only be added as sub-agent once.
    /// </summary>
    public BaseAgent? ParentAgent { get; internal set; }

    /// <summary>
    /// The sub-agents of this agent.
    /// </summary>
    public IReadOnlyList<BaseAgent> SubAgents { get; }

    /// <summary>
    /// Callbacks invoked before the agent runs.
    /// </summary>
    public IReadOnlyList<AgentCallback> BeforeAgentCallbacks { get; }

    /// <summary>
    /// Callbacks invoked after the agent runs.
    /// </summary>
    public IReadOnlyList<AgentCallback> AfterAgentCallbacks { get; }

    /// <summary>
    /// Gets the root agent by traversing up the parent chain.
    /// </summary>
    public BaseAgent RootAgent
    {
        get
        {
            var root = this;
            while (root.ParentAgent != null)
                root = root.ParentAgent;
            return root;
        }
    }

    protected BaseAgent(BaseAgentConfig config)
    {
        Name = ValidateAgentName(config.Name);
        Description = config.Description;
        ParentAgent = config.ParentAgent;
        SubAgents = config.SubAgents ?? new List<BaseAgent>();
        BeforeAgentCallbacks = config.BeforeAgentCallbacks ?? new List<AgentCallback>();
        AfterAgentCallbacks = config.AfterAgentCallbacks ?? new List<AgentCallback>();
        SetParentForSubAgents();
    }

    /// <summary>
    /// Runs the agent and yields events.
    /// </summary>
    public async IAsyncEnumerable<Event> RunAsync(
        InvocationContext parentContext,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var span = Telemetry.AdkTracing.StartSpan(Name);
        var context = CreateInvocationContext(parentContext);

        var beforeEvent = await HandleBeforeAgentCallbackAsync(context);
        if (beforeEvent != null)
        {
            yield return beforeEvent;
            if (context.EndInvocation) yield break;
        }

        Telemetry.AdkTracing.TraceAgentInvocation(this, context);

        await foreach (var evt in RunAsyncImpl(context, cancellationToken).WithCancellation(cancellationToken))
        {
            yield return evt;
        }

        if (context.EndInvocation) yield break;

        var afterEvent = await HandleAfterAgentCallbackAsync(context);
        if (afterEvent != null)
            yield return afterEvent;
    }

    /// <summary>
    /// Core agent logic. Implemented by subclasses.
    /// </summary>
    protected abstract IAsyncEnumerable<Event> RunAsyncImpl(
        InvocationContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds an agent by name in this agent and its descendants.
    /// </summary>
    public BaseAgent? FindAgent(string name)
    {
        if (Name == name) return this;
        return FindSubAgent(name);
    }

    /// <summary>
    /// Finds an agent by name in this agent's descendants only.
    /// </summary>
    public BaseAgent? FindSubAgent(string name)
    {
        foreach (var sub in SubAgents)
        {
            var found = sub.FindAgent(name);
            if (found != null) return found;
        }
        return null;
    }

    protected virtual InvocationContext CreateInvocationContext(InvocationContext parentContext)
    {
        return new InvocationContext(parentContext) { Agent = this };
    }

    private async Task<Event?> HandleBeforeAgentCallbackAsync(InvocationContext invocationContext)
    {
        if (BeforeAgentCallbacks.Count == 0) return null;

        var callbackContext = new AgentContext(invocationContext);
        foreach (var callback in BeforeAgentCallbacks)
        {
            var content = await callback(callbackContext);
            if (content != null)
            {
                invocationContext.EndInvocation = true;
                return Event.Create(e =>
                {
                    e.InvocationId = invocationContext.InvocationId;
                    e.Author = Name;
                    e.Branch = invocationContext.Branch;
                    e.Content = content;
                    e.Actions = callbackContext.EventActions;
                });
            }
        }

        if (callbackContext.State.HasDelta())
        {
            return Event.Create(e =>
            {
                e.InvocationId = invocationContext.InvocationId;
                e.Author = Name;
                e.Branch = invocationContext.Branch;
                e.Actions = callbackContext.EventActions;
            });
        }
        return null;
    }

    private async Task<Event?> HandleAfterAgentCallbackAsync(InvocationContext invocationContext)
    {
        if (AfterAgentCallbacks.Count == 0) return null;

        var callbackContext = new AgentContext(invocationContext);
        foreach (var callback in AfterAgentCallbacks)
        {
            var content = await callback(callbackContext);
            if (content != null)
            {
                return Event.Create(e =>
                {
                    e.InvocationId = invocationContext.InvocationId;
                    e.Author = Name;
                    e.Branch = invocationContext.Branch;
                    e.Content = content;
                    e.Actions = callbackContext.EventActions;
                });
            }
        }

        if (callbackContext.State.HasDelta())
        {
            return Event.Create(e =>
            {
                e.InvocationId = invocationContext.InvocationId;
                e.Author = Name;
                e.Branch = invocationContext.Branch;
                e.Actions = callbackContext.EventActions;
            });
        }
        return null;
    }

    private void SetParentForSubAgents()
    {
        foreach (var sub in SubAgents)
        {
            if (sub.ParentAgent != null)
                throw new InvalidOperationException(
                    $"Agent \"{sub.Name}\" already has a parent agent \"{sub.ParentAgent.Name}\", trying to add \"{Name}\".");
            sub.ParentAgent = this;
        }
    }

    private static string ValidateAgentName(string name)
    {
        if (!Regex.IsMatch(name, @"^[a-zA-Z_][a-zA-Z0-9_\-]*$"))
            throw new ArgumentException(
                $"Invalid agent name: \"{name}\". Must start with a letter or underscore and contain only letters, digits, underscores, and hyphens.");
        if (name == "user")
            throw new ArgumentException("Agent name cannot be 'user'. Reserved for end-user input.");
        return name;
    }
}
