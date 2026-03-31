// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Agents;

namespace GoogleAdk.Core.Tools;

/// <summary>
/// Tool for exiting execution of a LoopAgent.
/// When called by an LLM agent inside a LoopAgent, this tool sets the escalate
/// and skipSummarization flags, causing the LoopAgent to stop iterating.
/// </summary>
public class ExitLoopTool : BaseTool
{
    public static readonly ExitLoopTool Instance = new();

    public ExitLoopTool()
        : base("exit_loop", "Exits the loop.\n\nCall this function only when you are instructed to do so.") { }

    public override FunctionDeclaration? GetDeclaration()
        => new FunctionDeclaration { Name = Name, Description = Description };

    public override Task<object?> RunAsync(Dictionary<string, object?> args, AgentContext context)
    {
        context.EventActions.Escalate = true;
        context.EventActions.SkipSummarization = true;
        return Task.FromResult<object?>(string.Empty);
    }
}
