// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

namespace GoogleAdk.Core.CodeExecutors;

/// <summary>
/// Represents a file available to or produced by code execution.
/// </summary>
public class CodeFile
{
    /// <summary>The name of the file with extension (e.g., "file.csv").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The base64-encoded bytes of the file content.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>The MIME type of the file (e.g., "image/png").</summary>
    public string MimeType { get; set; } = string.Empty;
}

/// <summary>
/// Input for code execution.
/// </summary>
public class CodeExecutionInput
{
    /// <summary>The code to execute.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>The input files available to the code.</summary>
    public List<CodeFile> InputFiles { get; set; } = new();

    /// <summary>The execution ID for stateful code execution.</summary>
    public string? ExecutionId { get; set; }
}

/// <summary>
/// Result of code execution.
/// </summary>
public class CodeExecutionOutput
{
    /// <summary>The standard output of the code execution.</summary>
    public string Stdout { get; set; } = string.Empty;

    /// <summary>The standard error of the code execution.</summary>
    public string Stderr { get; set; } = string.Empty;

    /// <summary>The output files from the code execution.</summary>
    public List<CodeFile> OutputFiles { get; set; } = new();
}
