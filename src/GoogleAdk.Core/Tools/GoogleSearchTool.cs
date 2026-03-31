// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Agents;

namespace GoogleAdk.Core.Tools;

/// <summary>
/// A built-in tool that is automatically invoked by Gemini 2 models to retrieve
/// search results from Google Search.
/// This tool operates internally within the model and does not require local execution.
/// </summary>
public class GoogleSearchTool : BaseTool
{
    public static readonly GoogleSearchTool Instance = new();

    public GoogleSearchTool()
        : base("google_search", "Google Search Tool") { }

    public override Task<object?> RunAsync(Dictionary<string, object?> args, AgentContext context)
    {
        // Built-in tool on server side — triggered via request parameters.
        return Task.FromResult<object?>(null);
    }

    public override Task ProcessLlmRequestAsync(AgentContext context, LlmRequest llmRequest)
    {
        llmRequest.Config ??= new GenerateContentConfig();
        llmRequest.Config.Tools ??= new List<ToolDeclaration>();

        // Add googleSearch tool declaration to config
        llmRequest.Config.Tools.Add(new ToolDeclaration
        {
            GoogleSearch = new Dictionary<string, object?>()
        });

        return Task.CompletedTask;
    }
}
