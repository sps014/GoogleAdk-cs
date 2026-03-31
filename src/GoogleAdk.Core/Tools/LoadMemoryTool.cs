// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Agents;

namespace GoogleAdk.Core.Tools;

/// <summary>
/// A tool that loads memory for the current user.
/// Uses the InvocationContext.MemoryService to search for relevant memories.
/// </summary>
public class LoadMemoryTool : BaseTool
{
    public static readonly LoadMemoryTool Instance = new();

    public LoadMemoryTool()
        : base("load_memory", "Loads the memory for the current user.\n\nNOTE: Currently this tool only uses text part from the memory.") { }

    public override FunctionDeclaration? GetDeclaration()
        => new FunctionDeclaration
        {
            Name = Name,
            Description = Description,
            Parameters = new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?>
                {
                    ["query"] = new Dictionary<string, object?>
                    {
                        ["type"] = "string",
                        ["description"] = "The query to load the memory for."
                    }
                },
                ["required"] = new List<string> { "query" }
            }
        };

    public override async Task<object?> RunAsync(Dictionary<string, object?> args, AgentContext context)
    {
        var query = args.GetValueOrDefault("query")?.ToString() ?? string.Empty;
        var memoryService = context.InvocationContext.MemoryService
            ?? throw new InvalidOperationException("Memory service is not initialized.");

        var response = await memoryService.SearchMemoryAsync(new Abstractions.Memory.SearchMemoryRequest
        {
            AppName = context.AppName,
            UserId = context.UserId,
            Query = query
        });

        return new
        {
            memories = response.Memories.Select(m => new
            {
                content = string.Join(" ", m.Content.Parts?.Select(p => p.Text ?? "") ?? Array.Empty<string>()),
                author = m.Author,
                timestamp = m.Timestamp
            }).ToArray()
        };
    }

    public override Task ProcessLlmRequestAsync(AgentContext context, Abstractions.Events.LlmRequest llmRequest)
    {
        base.ProcessLlmRequestAsync(context, llmRequest);

        if (context.InvocationContext.MemoryService == null) return Task.CompletedTask;

        llmRequest.AppendInstructions(
            "You have memory. You can use it to answer questions. If any questions need\n" +
            "you to look up the memory, you should call load_memory function with a query.");

        return Task.CompletedTask;
    }
}
