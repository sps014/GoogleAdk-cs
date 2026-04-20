using Google.Cloud.Spanner.Data;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Agents;
using System.Data.Common;

namespace GoogleAdk.Core.Tools;

/// <summary>
/// A tool that discovers schema and metadata for a Cloud Spanner database.
/// Matches functionality of Python ADK's spanner metadata_tool.py.
/// </summary>
public sealed class SpannerMetadataTool : BaseTool
{
    public SpannerMetadataTool()
        : base("spanner_metadata", "Discovers table schemas, indexes, and lists tables for a Cloud Spanner database.")
    {
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
                    ["projectId"] = new Dictionary<string, object?> { ["type"] = "string" },
                    ["instanceId"] = new Dictionary<string, object?> { ["type"] = "string" },
                    ["databaseId"] = new Dictionary<string, object?> { ["type"] = "string" },
                    ["action"] = new Dictionary<string, object?> 
                    { 
                        ["type"] = "string",
                        ["description"] = "The metadata action to perform: list_tables, get_table_schema, list_indexes",
                        ["enum"] = new[] { "list_tables", "get_table_schema", "list_indexes" }
                    },
                    ["tableName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Required for get_table_schema and list_indexes" }
                },
                ["required"] = new[] { "projectId", "instanceId", "databaseId", "action" }
            }
        };
    }

    public override async Task<object?> RunAsync(Dictionary<string, object?> args, AgentContext context)
    {
        var projectId = args.TryGetValue("projectId", out var pidObj) ? FunctionToolArgs.Get<string>(pidObj) : null;
        var instanceId = args.TryGetValue("instanceId", out var iidObj) ? FunctionToolArgs.Get<string>(iidObj) : null;
        var databaseId = args.TryGetValue("databaseId", out var didObj) ? FunctionToolArgs.Get<string>(didObj) : null;
        var action = args.TryGetValue("action", out var actionObj) ? FunctionToolArgs.Get<string>(actionObj) : null;

        if (string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(instanceId) || string.IsNullOrEmpty(databaseId) || string.IsNullOrEmpty(action))
            return new Dictionary<string, object?> { ["error"] = "projectId, instanceId, databaseId, and action are required." };

        string connectionString = $"Data Source=projects/{projectId}/instances/{instanceId}/databases/{databaseId}";

        try
        {
            using var connection = new SpannerConnection(connectionString);
            await connection.OpenAsync();

            string query;
            SpannerCommand command;

            if (action == "list_tables")
            {
                query = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = ''";
                command = (SpannerCommand)connection.CreateCommand();
                command.CommandText = query;
            }
            else if (action == "get_table_schema")
            {
                var tableName = args.TryGetValue("tableName", out var tnObj) ? FunctionToolArgs.Get<string>(tnObj) : null;
                if (string.IsNullOrEmpty(tableName)) return new { error = "tableName is required for get_table_schema" };

                query = "SELECT COLUMN_NAME, SPANNER_TYPE, IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @tableName";
                command = (SpannerCommand)connection.CreateCommand();
                command.CommandText = query;
                command.Parameters.Add("tableName", SpannerDbType.String, tableName);
            }
            else if (action == "list_indexes")
            {
                var tableName = args.TryGetValue("tableName", out var tnObj) ? FunctionToolArgs.Get<string>(tnObj) : null;
                if (string.IsNullOrEmpty(tableName)) return new { error = "tableName is required for list_indexes" };

                query = "SELECT INDEX_NAME, INDEX_TYPE, IS_UNIQUE FROM INFORMATION_SCHEMA.INDEXES WHERE TABLE_NAME = @tableName";
                command = (SpannerCommand)connection.CreateCommand();
                command.CommandText = query;
                command.Parameters.Add("tableName", SpannerDbType.String, tableName);
            }
            else
            {
                return new { error = $"Unknown action: {action}" };
            }

            using var reader = await command.ExecuteReaderAsync();
            var results = new List<Dictionary<string, object>>();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null! : reader.GetValue(i);
                }
                results.Add(row);
            }

            return results;
        }
        catch (Exception ex)
        {
            return new Dictionary<string, object?> { ["error"] = ex.Message };
        }
    }
}
