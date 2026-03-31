// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Agents;

namespace GoogleAdk.Core.CodeExecutors;

/// <summary>
/// Base class for code executors. The code executor allows the agent to execute
/// code blocks from model responses and incorporate the execution results into
/// the final response.
/// </summary>
public abstract class BaseCodeExecutor
{
    /// <summary>
    /// If true, extract and process data files from the model request
    /// and attach them to the code executor.
    /// Supported data file MimeTypes are [text/csv]. Default: false.
    /// </summary>
    public bool OptimizeDataFile { get; set; }

    /// <summary>
    /// Whether the code executor is stateful. Default: false.
    /// </summary>
    public bool Stateful { get; set; }

    /// <summary>
    /// The number of attempts to retry on consecutive code execution errors. Default: 2.
    /// </summary>
    public int ErrorRetryAttempts { get; set; } = 2;

    /// <summary>
    /// The list of enclosing delimiters to identify code blocks.
    /// </summary>
    public List<(string Open, string Close)> CodeBlockDelimiters { get; set; } = new()
    {
        ("```tool_code\n", "\n```"),
        ("```python\n", "\n```"),
    };

    /// <summary>
    /// The delimiters to format the code execution result.
    /// </summary>
    public (string Open, string Close) ExecutionResultDelimiters { get; set; } = ("```tool_output\n", "\n```");

    /// <summary>
    /// Executes code and returns the code execution result.
    /// </summary>
    /// <param name="invocationContext">The invocation context.</param>
    /// <param name="input">The code execution input.</param>
    /// <returns>The result of the code execution.</returns>
    public abstract Task<CodeExecutionOutput> ExecuteCodeAsync(
        InvocationContext invocationContext,
        CodeExecutionInput input);
}
