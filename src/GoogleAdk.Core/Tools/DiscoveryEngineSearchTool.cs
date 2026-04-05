using Google.Cloud.DiscoveryEngine.V1;
using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Agents;

namespace GoogleAdk.Core.Tools;

public sealed class DiscoveryEngineSearchTool : BaseTool
{
    private readonly SearchServiceClient _searchServiceClient;
    private readonly string _servingConfig;

    public string? Datastore { get; }
    public string? Engine { get; }

    public DiscoveryEngineSearchTool(string? datastore = null, string? engine = null)
        : base("discovery_engine_search", "Discovery Engine Search Tool")
    {
        if ((datastore == null && engine == null) || (datastore != null && engine != null))
        {
            throw new ArgumentException("Either datastore or engine must be specified.");
        }

        Datastore = datastore;
        Engine = engine;
        _servingConfig = $"{datastore ?? engine}/servingConfigs/default_config";
        
        _searchServiceClient = SearchServiceClient.Create();
    }

    public override async Task<object?> RunAsync(Dictionary<string, object?> args, AgentContext context)
    {
        var query = args.TryGetValue("query", out var queryObj) ? FunctionToolArgs.Get<string>(queryObj) : null;
        if (string.IsNullOrEmpty(query))
        {
            return new Dictionary<string, object?> { ["error"] = "Query is required." };
        }

        var request = new SearchRequest
        {
            ServingConfig = _servingConfig,
            Query = query,
        };

        try
        {
            var response = _searchServiceClient.SearchAsync(request);
            var results = new List<Dictionary<string, object?>>();

            await foreach (var result in response)
            {
                var dict = new Dictionary<string, object?>();
                if (result.Document != null)
                {
                    dict["name"] = result.Document.Name;
                    dict["id"] = result.Document.Id;
                }
                else if (result.Chunk != null)
                {
                    dict["name"] = result.Chunk.Name;
                    dict["id"] = result.Chunk.Id;
                    dict["content"] = result.Chunk.Content;
                }
                results.Add(dict);
            }

            return new Dictionary<string, object?>
            {
                ["status"] = "success",
                ["results"] = results
            };
        }
        catch (Exception ex)
        {
            return new Dictionary<string, object?>
            {
                ["status"] = "error",
                ["error_message"] = ex.Message
            };
        }
    }

    public override FunctionDeclaration? GetDeclaration()
    {
        return new FunctionDeclaration
        {
            Name = Name,
            Description = Description,
            Parameters = new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?>
                {
                    ["query"] = new Dictionary<string, object?>
                    {
                        ["type"] = "string",
                        ["description"] = "The search query."
                    }
                },
                ["required"] = new[] { "query" }
            }
        };
    }
}
