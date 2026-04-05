using Google.Cloud.BigQuery.V2;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Agents;

namespace GoogleAdk.Core.Tools;

public sealed class BigQueryQueryTool : BaseTool
{
    public BigQueryQueryTool()
        : base("bigquery_query", "Executes a query in BigQuery.")
    {
    }

    public override async Task<object?> RunAsync(Dictionary<string, object?> args, AgentContext context)
    {
        var projectId = args.TryGetValue("projectId", out var projectIdObj) ? FunctionToolArgs.Get<string>(projectIdObj) : null;
        if (string.IsNullOrEmpty(projectId))
            return new Dictionary<string, object?> { ["error"] = "projectId is required." };

        var query = args.TryGetValue("query", out var queryObj) ? FunctionToolArgs.Get<string>(queryObj) : null;
        if (string.IsNullOrEmpty(query))
            return new Dictionary<string, object?> { ["error"] = "query is required." };

        try
        {
            var client = await BigQueryClient.CreateAsync(projectId);
            var results = await client.ExecuteQueryAsync(query, parameters: null);
            
            var rows = new List<Dictionary<string, object?>>();
            foreach (var row in results)
            {
                var dict = new Dictionary<string, object?>();
                foreach (var field in results.Schema.Fields)
                {
                    dict[field.Name] = row[field.Name];
                }
                rows.Add(dict);
            }

            return new Dictionary<string, object?>
            {
                ["status"] = "SUCCESS",
                ["rows"] = rows
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
                    ["query"] = new Dictionary<string, object?>
                    {
                        ["type"] = "string",
                        ["description"] = "The SQL query to execute."
                    }
                },
                ["required"] = new[] { "projectId", "query" }
            }
        };
    }
}
