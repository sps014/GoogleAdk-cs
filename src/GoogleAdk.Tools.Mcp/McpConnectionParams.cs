// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

namespace GoogleAdk.Tools.Mcp;

/// <summary>
/// Base class for MCP connection parameters.
/// </summary>
public abstract class McpConnectionParams;

/// <summary>
/// Connection parameters for stdio-based MCP servers (local child processes).
/// </summary>
public sealed class StdioConnectionParams : McpConnectionParams
{
    /// <summary>The command to run (e.g., "npx", "python").</summary>
    public required string Command { get; init; }

    /// <summary>Arguments passed to the command.</summary>
    public IReadOnlyList<string>? Arguments { get; init; }

    /// <summary>Environment variables for the child process.</summary>
    public IReadOnlyDictionary<string, string>? EnvironmentVariables { get; init; }

    /// <summary>Working directory for the child process.</summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>Connection timeout in milliseconds.</summary>
    public int? TimeoutMs { get; init; }
}

/// <summary>
/// Connection parameters for HTTP/SSE-based MCP servers.
/// </summary>
public sealed class HttpConnectionParams : McpConnectionParams
{
    /// <summary>The URL of the MCP server endpoint.</summary>
    public required string Url { get; init; }

    /// <summary>Optional HTTP headers to include in requests.</summary>
    public IReadOnlyDictionary<string, string>? Headers { get; init; }

    /// <summary>Connection timeout in milliseconds.</summary>
    public int? TimeoutMs { get; init; }
}
