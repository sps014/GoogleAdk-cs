// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Tools;
using ModelContextProtocol.Client;

namespace GoogleAdk.Tools.Mcp;

/// <summary>
/// A toolset that dynamically discovers and provides tools from an MCP server.
/// Connects to the server, retrieves available tools, and wraps each in an <see cref="McpTool"/>.
/// </summary>
/// <example>
/// <code>
/// // Stdio-based MCP server
/// var toolset = new McpToolset(new StdioConnectionParams
/// {
///     Command = "npx",
///     Arguments = ["-y", "@modelcontextprotocol/server-everything"]
/// });
///
/// // HTTP-based MCP server
/// var toolset = new McpToolset(new HttpConnectionParams
/// {
///     Url = "http://localhost:8788/mcp"
/// });
///
/// // Use with an LlmAgent
/// var agent = new LlmAgent(new LlmAgentConfig
/// {
///     Name = "mcp_agent",
///     Model = model,
///     Instruction = "You are a helpful assistant.",
///     Toolsets = new List&lt;BaseToolset&gt; { toolset }
/// });
/// </code>
/// </example>
public sealed class McpToolset : BaseToolset
{
    private readonly McpConnectionParams _connectionParams;
    private IMcpClient? _client;

    public McpToolset(McpConnectionParams connectionParams, string? prefix = null)
        : base(prefix: prefix)
    {
        _connectionParams = connectionParams;
    }

    public McpToolset(McpConnectionParams connectionParams, IReadOnlyList<string> toolFilterNames, string? prefix = null)
        : base(toolFilterNames, prefix)
    {
        _connectionParams = connectionParams;
    }

    public McpToolset(McpConnectionParams connectionParams, ToolPredicate toolFilter, string? prefix = null)
        : base(toolFilter, prefix)
    {
        _connectionParams = connectionParams;
    }

    /// <summary>
    /// Connects to the MCP server and returns all available tools (filtered by any configured filter).
    /// </summary>
    public override async Task<IReadOnlyList<BaseTool>> GetToolsAsync(AgentContext? context = null)
    {
        var client = await GetOrCreateClientAsync();
        var mcpTools = await client.ListToolsAsync();

        var tools = new List<BaseTool>();
        foreach (var mcpTool in mcpTools)
        {
            var wrapped = new McpTool(mcpTool, client, Prefix);

            // Apply filters
            if (ToolFilterNames != null && !ToolFilterNames.Contains(mcpTool.Name))
                continue;
            if (ToolFilterPredicate != null && context != null && !ToolFilterPredicate(wrapped, context))
                continue;

            tools.Add(wrapped);
        }

        return tools;
    }

    public override async ValueTask DisposeAsync()
    {
        if (_client != null)
        {
            await _client.DisposeAsync();
            _client = null;
        }
    }

    private async Task<IMcpClient> GetOrCreateClientAsync()
    {
        if (_client != null)
            return _client;

        IClientTransport transport = _connectionParams switch
        {
            StdioConnectionParams stdio => new StdioClientTransport(new StdioClientTransportOptions
            {
                Name = "AdkMcpClient",
                Command = stdio.Command,
                Arguments = stdio.Arguments?.ToList(),
                EnvironmentVariables = stdio.EnvironmentVariables?.ToDictionary(kv => kv.Key, kv => (string?)kv.Value),
                WorkingDirectory = stdio.WorkingDirectory,
            }),
            HttpConnectionParams http => new SseClientTransport(new SseClientTransportOptions
            {
                Endpoint = new Uri(http.Url),
                Name = "AdkMcpClient",
            }),
            _ => throw new NotSupportedException($"Unsupported connection params type: {_connectionParams.GetType().Name}")
        };

        _client = await McpClientFactory.CreateAsync(transport);
        return _client;
    }
}
