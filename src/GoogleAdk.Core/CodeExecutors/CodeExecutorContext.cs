// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Sessions;

namespace GoogleAdk.Core.CodeExecutors;

/// <summary>
/// Persistent context used to configure the code executor across invocations.
/// Stores execution session ID, processed file names, and execution results in session state.
/// </summary>
public class CodeExecutorContext
{
    private const string ContextKey = "_code_execution_context";
    private const string SessionIdKey = "execution_session_id";
    private const string ProcessedFileNamesKey = "processed_input_files";
    private const string InputFileKey = "_code_executor_input_files";
    private const string ErrorCountKey = "_code_executor_error_counts";
    private const string CodeExecutionResultsKey = "_code_execution_results";

    private readonly Dictionary<string, object?> _context;
    private readonly State _sessionState;

    public CodeExecutorContext(State sessionState)
    {
        _sessionState = sessionState;
        _context = sessionState.Get<Dictionary<string, object?>>(ContextKey) ?? new();
    }

    /// <summary>
    /// Gets the state delta to update in the persistent session state.
    /// </summary>
    public Dictionary<string, object?> GetStateDelta()
    {
        return new Dictionary<string, object?>
        {
            [ContextKey] = new Dictionary<string, object?>(_context)
        };
    }

    /// <summary>
    /// Gets the execution ID for the code executor.
    /// </summary>
    public string? GetExecutionId()
    {
        return _context.TryGetValue(SessionIdKey, out var id) ? id?.ToString() : null;
    }

    /// <summary>
    /// Sets the execution ID for the code executor.
    /// </summary>
    public void SetExecutionId(string executionId)
    {
        _context[SessionIdKey] = executionId;
    }

    /// <summary>
    /// Gets the processed input file names.
    /// </summary>
    public List<string> GetProcessedFileNames()
    {
        if (_context.TryGetValue(ProcessedFileNamesKey, out var names) && names is List<string> list)
            return list;
        return new List<string>();
    }

    /// <summary>
    /// Adds a processed file name.
    /// </summary>
    public void AddProcessedFileName(string fileName)
    {
        var names = GetProcessedFileNames();
        names.Add(fileName);
        _context[ProcessedFileNamesKey] = names;
    }

    /// <summary>
    /// Gets the current error count.
    /// </summary>
    public int GetErrorCount()
    {
        if (_context.TryGetValue(ErrorCountKey, out var count) && count is int c)
            return c;
        return 0;
    }

    /// <summary>
    /// Sets the error count.
    /// </summary>
    public void SetErrorCount(int count)
    {
        _context[ErrorCountKey] = count;
    }

    /// <summary>
    /// Gets the input files for the code executor.
    /// </summary>
    public List<CodeFile> GetInputFiles()
    {
        if (_sessionState.Get<List<CodeFile>>(InputFileKey) is { } files)
            return files;
        return new List<CodeFile>();
    }

    /// <summary>
    /// Sets the input files for the code executor.
    /// </summary>
    public void SetInputFiles(List<CodeFile> files)
    {
        _sessionState.Set(InputFileKey, files);
    }
}
