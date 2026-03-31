// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Agents;

namespace GoogleAdk.Core;

/// <summary>
/// Base class for all tools in the Agent Development Kit.
/// </summary>
public abstract class BaseTool : IBaseTool
{
    public string Name { get; }
    public string Description { get; }
    public bool IsLongRunning { get; }

    protected BaseTool(string name, string description, bool isLongRunning = false)
    {
        Name = name;
        Description = description;
        IsLongRunning = isLongRunning;
    }

    /// <summary>
    /// Gets the function declaration for this tool. Override to provide schema.
    /// </summary>
    public virtual FunctionDeclaration? GetDeclaration() => null;

    /// <summary>
    /// Runs the tool with the given arguments and context.
    /// </summary>
    public abstract Task<object?> RunAsync(Dictionary<string, object?> args, AgentContext context);

    /// <summary>
    /// Processes the outgoing LLM request for this tool.
    /// Default: adds the function declaration to the request.
    /// </summary>
    public virtual Task ProcessLlmRequestAsync(AgentContext context, LlmRequest llmRequest)
    {
        var declaration = GetDeclaration();
        if (declaration == null) return Task.CompletedTask;

        if (llmRequest.ToolsDict.ContainsKey(Name))
            throw new InvalidOperationException($"Duplicate tool name: {Name}");

        llmRequest.ToolsDict[Name] = this;

        llmRequest.Config ??= new GenerateContentConfig();
        llmRequest.Config.Tools ??= new List<ToolDeclaration>();

        var existing = llmRequest.Config.Tools.FirstOrDefault(t => t.FunctionDeclarations != null);
        if (existing != null)
        {
            existing.FunctionDeclarations!.Add(declaration);
        }
        else
        {
            llmRequest.Config.Tools.Add(new ToolDeclaration
            {
                FunctionDeclarations = new List<FunctionDeclaration> { declaration }
            });
        }

        return Task.CompletedTask;
    }
}
