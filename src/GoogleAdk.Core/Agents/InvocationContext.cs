// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Artifacts;
using GoogleAdk.Core.Abstractions.Auth;
using GoogleAdk.Core.Abstractions.Memory;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Abstractions.Sessions;
using GoogleAdk.Core.Plugins;

namespace GoogleAdk.Core.Agents;

/// <summary>
/// An invocation context representing a single agent invocation (user message → final response).
/// Can span multiple agent calls via agent transfer.
/// </summary>
public class InvocationContext
{
    public IBaseArtifactService? ArtifactService { get; set; }
    public BaseSessionService? SessionService { get; set; }
    public IBaseMemoryService? MemoryService { get; set; }
    public IBaseCredentialService? CredentialService { get; set; }

    /// <summary>The unique ID of this invocation.</summary>
    public string InvocationId { get; set; } = $"e-{Guid.NewGuid()}";

    /// <summary>
    /// The branch path (e.g. "agent_1.agent_2") for parallel agent isolation.
    /// </summary>
    public string? Branch { get; set; }

    /// <summary>The current agent in this invocation.</summary>
    public BaseAgent Agent { get; set; } = null!;

    /// <summary>The user content that started this invocation.</summary>
    public Content? UserContent { get; set; }

    /// <summary>The current session.</summary>
    public Session Session { get; set; } = null!;

    /// <summary>Set to true in callbacks/tools to terminate this invocation.</summary>
    public bool EndInvocation { get; set; }

    /// <summary>Runtime configuration.</summary>
    public RunConfig? RunConfig { get; set; }

    /// <summary>Plugin manager for this invocation.</summary>
    public PluginManager? PluginManager { get; set; }

    private int _llmCallCount;

    public InvocationContext() { }

    /// <summary>
    /// Copy constructor from parent context.
    /// </summary>
    public InvocationContext(InvocationContext parent)
    {
        ArtifactService = parent.ArtifactService;
        SessionService = parent.SessionService;
        MemoryService = parent.MemoryService;
        CredentialService = parent.CredentialService;
        InvocationId = parent.InvocationId;
        Branch = parent.Branch;
        Agent = parent.Agent;
        UserContent = parent.UserContent;
        Session = parent.Session;
        EndInvocation = parent.EndInvocation;
        RunConfig = parent.RunConfig;
        PluginManager = parent.PluginManager;
    }

    public string AppName => Session.AppName;
    public string UserId => Session.UserId;

    /// <summary>
    /// Increments the LLM call count and enforces the limit from RunConfig.
    /// </summary>
    public void IncrementLlmCallCount()
    {
        _llmCallCount++;
        if (RunConfig?.MaxLlmCalls > 0 && _llmCallCount > RunConfig.MaxLlmCalls)
            throw new InvalidOperationException($"Max number of LLM calls limit of {RunConfig.MaxLlmCalls} exceeded.");
    }
}
