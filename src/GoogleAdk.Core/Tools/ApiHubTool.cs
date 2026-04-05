using Google.Cloud.ApiHub.V1;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Agents;

namespace GoogleAdk.Core.Tools;

public sealed class ApiHubTool : BaseTool
{
    public ApiHubTool()
        : base("apihub_search", "Searches APIs in Google Cloud API Hub.")
    {
    }

    public override async Task<object?> RunAsync(Dictionary<string, object?> args, AgentContext context)
    {
        if (!args.TryGetValue("projectId", out var projectIdObj) || projectIdObj is not string projectId)
            return new Dictionary<string, object?> { ["error"] = "projectId is required." };
        if (!args.TryGetValue("location", out var locationObj) || locationObj is not string location)
            return new Dictionary<string, object?> { ["error"] = "location is required." };

        var query = args.GetValueOrDefault("query")?.ToString() ?? "";

        try
        {
            var client = await ApiHubClient.CreateAsync();
            var parent = $"projects/{projectId}/locations/{location}";

            var request = new ListApisRequest
            {
                Parent = parent,
                Filter = query
            };

            var results = new List<Dictionary<string, object?>>();
            await foreach (var api in client.ListApisAsync(request))
            {
                results.Add(new Dictionary<string, object?>
                {
                    ["name"] = api.Name,
                    ["displayName"] = api.DisplayName,
                    ["description"] = api.Description
                });
            }

            return new Dictionary<string, object?>
            {
                ["status"] = "SUCCESS",
                ["apis"] = results
            };
        }
        catch (Exception ex)
        {
            return new Dictionary<string, object?>
            {
                ["status"] = "ERROR",
                ["error_details"] = ex.Message
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
                    ["projectId"] = new Dictionary<string, object?>
                    {
                        ["type"] = "string",
                        ["description"] = "The Google Cloud project ID."
                    },
                    ["location"] = new Dictionary<string, object?>
                    {
                        ["type"] = "string",
                        ["description"] = "The API Hub location."
                    },
                    ["query"] = new Dictionary<string, object?>
                    {
                        ["type"] = "string",
                        ["description"] = "Optional filter query."
                    }
                },
                ["required"] = new[] { "projectId", "location" }
            }
        };
    }
}
