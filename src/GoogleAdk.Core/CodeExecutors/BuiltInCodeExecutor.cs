// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Agents;

namespace GoogleAdk.Core.CodeExecutors;

/// <summary>
/// A code executor that uses the Model's built-in code execution (e.g. Gemini 2.0+).
/// </summary>
public class BuiltInCodeExecutor : BaseCodeExecutor
{
    public override Task<CodeExecutionOutput> ExecuteCodeAsync(
        InvocationContext invocationContext,
        CodeExecutionInput input)
    {
        // Built-in code execution is handled by the model itself.
        return Task.FromResult(new CodeExecutionOutput
        {
            Stdout = string.Empty,
            Stderr = string.Empty,
            OutputFiles = new List<CodeFile>()
        });
    }

    /// <summary>
    /// Processes the LLM request to enable the model's built-in code execution tool.
    /// </summary>
    public void ProcessLlmRequest(LlmRequest llmRequest)
    {
        if (string.IsNullOrEmpty(llmRequest.Model) || !IsGemini2OrAbove(llmRequest.Model))
            throw new InvalidOperationException(
                $"Gemini code execution tool is not supported for model {llmRequest.Model}");

        llmRequest.Config ??= new GenerateContentConfig();
        llmRequest.Config.Tools ??= new List<ToolDeclaration>();
        llmRequest.Config.Tools.Add(new ToolDeclaration
        {
            // Signal to the model API that code execution is enabled
            GoogleSearch = new Dictionary<string, object?> { ["codeExecution"] = new { } }
        });
    }

    private static bool IsGemini2OrAbove(string model)
    {
        var lower = model.ToLowerInvariant();
        if (!lower.Contains("gemini")) return false;

        // Match patterns like gemini-2.0, gemini-2.5, etc.
        if (lower.Contains("gemini-2") || lower.Contains("gemini-3"))
            return true;

        return false;
    }
}
