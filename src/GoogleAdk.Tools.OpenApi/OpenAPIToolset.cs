using GoogleAdk.Core;
using GoogleAdk.Core.Tools;
using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Agents;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GoogleAdk.Tools.OpenApi;

/// <summary>
/// A toolset that generates tools from an OpenAPI specification.
/// </summary>
public class OpenAPIToolset : BaseToolset
{
    private readonly List<BaseTool> _tools = new();

    /// <summary>
    /// Initializes a new instance of the OpenAPIToolset by parsing the provided OpenAPI spec.
    /// </summary>
    /// <param name="openApiSpec">The OpenAPI specification content.</param>
    /// <param name="format">The format of the spec ("json" or "yaml"). Currently unused by OpenApiStringReader as it auto-detects.</param>
    /// <param name="httpClient">An optional HttpClient to use for requests. If null, a new one is created.</param>
    public OpenAPIToolset(string openApiSpec, string format = "json", HttpClient? httpClient = null)
    {
        var reader = new OpenApiStringReader();
        var document = reader.Read(openApiSpec, out var diagnostic);

        if (diagnostic.Errors.Count > 0)
        {
            var errors = string.Join(", ", diagnostic.Errors.Select(e => e.Message));
            throw new ArgumentException($"Failed to parse OpenAPI spec: {errors}");
        }

        var baseUrl = document.Servers?.FirstOrDefault()?.Url ?? "http://localhost";
        httpClient ??= new HttpClient();

        foreach (var pathItem in document.Paths)
        {
            foreach (var operation in pathItem.Value.Operations)
            {
                var tool = new OpenAPITool(
                    document,
                    pathItem.Key,
                    operation.Key,
                    operation.Value,
                    baseUrl,
                    httpClient);
                
                _tools.Add(tool);
            }
        }
    }

    /// <summary>
    /// Returns the tools generated from the OpenAPI spec.
    /// </summary>
    public IEnumerable<BaseTool> GetTools() => _tools;

    /// <summary>
    /// Async implementation to satisfy BaseToolset.
    /// </summary>
    public override Task<IReadOnlyList<BaseTool>> GetToolsAsync(AgentContext? context = null)
    {
        IReadOnlyList<BaseTool> filtered = _tools.Where(t => IsToolSelected(t, context!)).ToList();
        return Task.FromResult(filtered);
    }
}
