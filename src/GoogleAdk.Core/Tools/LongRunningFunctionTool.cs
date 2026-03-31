// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Agents;

namespace GoogleAdk.Core.Tools;

/// <summary>
/// A FunctionTool that is marked as long-running.
/// Appends a note to the description telling the model not to re-call it.
/// </summary>
public class LongRunningFunctionTool : FunctionTool
{
    private const string LongRunningInstruction =
        "\n\nNOTE: This is a long-running operation. Do not call this tool again if it has already returned some intermediate or pending status.";

    public LongRunningFunctionTool(
        string name,
        string description,
        Func<Dictionary<string, object?>, AgentContext, Task<object?>> execute,
        Dictionary<string, object?>? parameters = null)
        : base(name, description + LongRunningInstruction, execute, parameters, isLongRunning: true)
    {
    }
}
