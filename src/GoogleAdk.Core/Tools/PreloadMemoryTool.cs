// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Agents;

namespace GoogleAdk.Core.Tools;

/// <summary>
/// A tool that preloads memory into the LLM context for each request.
/// Not called by the model directly — it modifies the LLM request with past conversation memories.
/// </summary>
public class PreloadMemoryTool : BaseTool
{
    public static readonly PreloadMemoryTool Instance = new();

    public PreloadMemoryTool()
        : base("preload_memory", "preload_memory") { }

    public override Task<object?> RunAsync(Dictionary<string, object?> args, AgentContext context)
    {
        throw new InvalidOperationException("PreloadMemoryTool should not be called by model.");
    }

    public override async Task ProcessLlmRequestAsync(AgentContext context, LlmRequest llmRequest)
    {
        var memoryService = context.InvocationContext.MemoryService;
        if (memoryService == null) return;

        var userContent = context.InvocationContext.UserContent;
        var userQuery = userContent?.Parts?.FirstOrDefault()?.Text;
        if (string.IsNullOrEmpty(userQuery)) return;

        Abstractions.Memory.SearchMemoryResponse response;
        try
        {
            response = await memoryService.SearchMemoryAsync(new Abstractions.Memory.SearchMemoryRequest
            {
                AppName = context.AppName,
                UserId = context.UserId,
                Query = userQuery
            });
        }
        catch
        {
            return; // Fail silently if memory search fails
        }

        if (response.Memories.Count == 0) return;

        var lines = new List<string>();
        foreach (var memory in response.Memories)
        {
            if (memory.Timestamp != null)
                lines.Add($"Time: {memory.Timestamp}");

            var text = string.Join(" ", memory.Content.Parts?.Select(p => p.Text ?? "") ?? Array.Empty<string>());
            if (!string.IsNullOrEmpty(text))
            {
                lines.Add(!string.IsNullOrEmpty(memory.Author) ? $"{memory.Author}: {text}" : text);
            }
        }

        if (lines.Count == 0) return;

        var instruction =
            "The following content is from your previous conversations with the user.\n" +
            "They may be useful for answering the user's current query.\n" +
            "<PAST_CONVERSATIONS>\n" +
            string.Join("\n", lines) + "\n" +
            "</PAST_CONVERSATIONS>\n";

        llmRequest.AppendInstructions(instruction);
    }
}
