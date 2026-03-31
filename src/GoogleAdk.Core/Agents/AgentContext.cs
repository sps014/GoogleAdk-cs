// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Sessions;

namespace GoogleAdk.Core.Agents;

/// <summary>
/// Context available to callbacks and tool invocations during an agent run.
/// Provides access to state, event actions, artifacts, and memory.
/// </summary>
public class AgentContext
{
    public InvocationContext InvocationContext { get; }
    public EventActions EventActions { get; }
    public State State { get; }
    public string? FunctionCallId { get; set; }

    public AgentContext(InvocationContext invocationContext, EventActions? eventActions = null, string? functionCallId = null)
    {
        InvocationContext = invocationContext;
        EventActions = eventActions ?? new EventActions();
        State = new State(invocationContext.Session.State, EventActions.StateDelta);
        FunctionCallId = functionCallId;
    }

    public string AppName => InvocationContext.AppName;
    public string UserId => InvocationContext.UserId;
    public Session Session => InvocationContext.Session;
}
