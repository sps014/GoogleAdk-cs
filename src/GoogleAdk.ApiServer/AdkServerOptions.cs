using GoogleAdk.Core.Abstractions.Artifacts;
using GoogleAdk.Core.Abstractions.Memory;
using Microsoft.AspNetCore.Cors.Infrastructure;

namespace GoogleAdk.ApiServer;

/// <summary>
/// Configuration options for the ADK Server.
/// </summary>
public class AdkServerOptions
{
    /// <summary>
    /// The artifact service to use for managing artifacts.
    /// </summary>
    public IBaseArtifactService? ArtifactService { get; set; }

    /// <summary>
    /// The memory service to use for persisting agent memory.
    /// </summary>
    public IBaseMemoryService? MemoryService { get; set; }

    /// <summary>
    /// The port on which the server will listen. Defaults to 8080.
    /// </summary>
    public int Port { get; set; } = 8080;

    /// <summary>
    /// The host on which the server will listen. Defaults to localhost.
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// Whether to host the ADK Web UI. Defaults to true.
    /// </summary>
    public bool ShowAdkWebUI { get; set; } = true;

    /// <summary>
    /// Whether to expose Swagger UI. Defaults to true.
    /// </summary>
    public bool ShowSwaggerUI { get; set; } = true;

    /// <summary>
    /// Whether to enable Agent-to-Agent (A2A) endpoints. Defaults to false.
    /// </summary>
    public bool EnableA2a { get; set; } = false;

    /// <summary>
    /// The initial state to supply to the server.
    /// </summary>
    public Dictionary<string, object?>? InitialState { get; set; }

    /// <summary>
    /// Whether to enable Google Cloud tracing. Defaults to false.
    /// </summary>
    public bool EnableCloudTracing { get; set; } = false;

    /// <summary>
    /// Optional action to configure the default CORS policy.
    /// If not provided, allows any origin, method, and header.
    /// </summary>
    public Action<CorsPolicyBuilder>? ConfigureCors { get; set; }
}
