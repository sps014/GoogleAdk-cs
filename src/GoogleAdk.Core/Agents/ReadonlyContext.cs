// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Sessions;

namespace GoogleAdk.Core.Agents;

/// <summary>
/// A read-only context representing the data of a single invocation of an agent.
/// Used in instruction providers and toolsets where mutation is not allowed.
/// </summary>
public class ReadonlyContext
{
    public InvocationContext InvocationContext { get; }

    public ReadonlyContext(InvocationContext invocationContext)
    {
        InvocationContext = invocationContext;
    }

    /// <summary>The user content that started this invocation.</summary>
    public Abstractions.Models.Content? UserContent => InvocationContext.UserContent;

    /// <summary>The current invocation ID.</summary>
    public string InvocationId => InvocationContext.InvocationId;

    /// <summary>The user ID of the current session.</summary>
    public string UserId => InvocationContext.UserId;

    /// <summary>The ID of the current session.</summary>
    public string SessionId => InvocationContext.Session.Id;

    /// <summary>The current agent name.</summary>
    public string AgentName => InvocationContext.Agent.Name;

    /// <summary>The state of the current session (read-only view).</summary>
    public IReadOnlyDictionary<string, object?> State => InvocationContext.Session.State;
}
