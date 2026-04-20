using Google.Cloud.Spanner.Data;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Agents;
using System.Data.Common;

namespace GoogleAdk.Core.Tools;

/// <summary>
/// A tool that performs vector similarity search in Cloud Spanner.
/// Matches functionality of Python ADK's spanner search_tool.py.
/// </summary>
public sealed class SpannerSearchTool : BaseTool
{
    public SpannerSearchTool()
        : base("spanner_search", "Performs a vector similarity search in a Cloud Spanner database using a text query.")
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
                    ["tableName"] = new Dictionary<string, object?> { ["type"] = "string" },
                    ["query"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "The text to search for" },
                    ["embeddingColumnName"] = new Dictionary<string, object?> { ["type"] = "string" },
                    ["modelName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Spanner ML Model Name or Vertex Endpoint" },
                    ["distanceType"] = new Dictionary<string, object?> { ["type"] = "string", ["enum"] = new[] { "COSINE", "EUCLIDEAN", "DOT_PRODUCT" } },
                    ["topK"] = new Dictionary<string, object?> { ["type"] = "integer", ["description"] = "Number of results to return" }
                },
                ["required"] = new[] { "projectId", "instanceId", "databaseId", "tableName", "query", "embeddingColumnName", "modelName" }
            }
        };
    }

    public override async Task<object?> RunAsync(Dictionary<string, object?> args, AgentContext context)
    {
        var projectId = args.TryGetValue("projectId", out var pidObj) ? FunctionToolArgs.Get<string>(pidObj) : null;
        var instanceId = args.TryGetValue("instanceId", out var iidObj) ? FunctionToolArgs.Get<string>(iidObj) : null;
        var databaseId = args.TryGetValue("databaseId", out var didObj) ? FunctionToolArgs.Get<string>(didObj) : null;
        
        var tableName = args.TryGetValue("tableName", out var tnObj) ? FunctionToolArgs.Get<string>(tnObj) : null;
        var queryText = args.TryGetValue("query", out var qObj) ? FunctionToolArgs.Get<string>(qObj) : null;
        var embeddingColumnName = args.TryGetValue("embeddingColumnName", out var embColObj) ? FunctionToolArgs.Get<string>(embColObj) : null;
        var modelName = args.TryGetValue("modelName", out var modObj) ? FunctionToolArgs.Get<string>(modObj) : null;
        
        var distanceType = args.TryGetValue("distanceType", out var distObj) ? FunctionToolArgs.Get<string>(distObj) : "COSINE";
        var topK = args.TryGetValue("topK", out var kObj) ? FunctionToolArgs.Get<int>(kObj) : 5;

        if (string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(instanceId) || string.IsNullOrEmpty(databaseId) || 
            string.IsNullOrEmpty(tableName) || string.IsNullOrEmpty(queryText) || string.IsNullOrEmpty(embeddingColumnName) || string.IsNullOrEmpty(modelName))
        {
            return new Dictionary<string, object?> { ["error"] = "Required parameters are missing." };
        }

        string connectionString = $"Data Source=projects/{projectId}/instances/{instanceId}/databases/{databaseId}";

        string distanceFunc = distanceType.ToUpperInvariant() switch
        {
            "EUCLIDEAN" => "EUCLIDEAN_DISTANCE",
            "DOT_PRODUCT" => "DOT_PRODUCT",
            _ => "COSINE_DISTANCE"
        };

        try
        {
            using var connection = new SpannerConnection(connectionString);
            await connection.OpenAsync();

            // Resolve the embedding vector using ML.PREDICT first
            var embeddingQuery = $@"
                SELECT embeddings.values 
                FROM ML.PREDICT(
                    MODEL {modelName},
                    (SELECT CAST(@query AS STRING) as content)
                )";

            using var embedCommand = (SpannerCommand)connection.CreateCommand();
            embedCommand.CommandText = embeddingQuery;
            embedCommand.Parameters.Add("query", SpannerDbType.String, queryText);

            double[]? vector = null;
            using (var reader = await embedCommand.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    // Convert FLOAT64 array to double array
                    var rawArray = reader.GetFieldValue<double[]>(0);
                    vector = rawArray;
                }
            }

            if (vector == null)
            {
                return new { error = "Failed to generate embedding for the query." };
            }

            // Perform KNN search
            var searchSql = $@"
                SELECT *, {distanceFunc}({embeddingColumnName}, @vector) as distance
                FROM {tableName}
                ORDER BY distance
                LIMIT @topK
            ";

            using var searchCommand = (SpannerCommand)connection.CreateCommand();
            searchCommand.CommandText = searchSql;
            // Spanner ADO.NET supports double[] for FLOAT64 ARRAY
            searchCommand.Parameters.Add("vector", SpannerDbType.ArrayOf(SpannerDbType.Float64), vector);
            searchCommand.Parameters.Add("topK", SpannerDbType.Int64, (long)topK);

            using var searchReader = await searchCommand.ExecuteReaderAsync();
            var results = new List<Dictionary<string, object>>();
            while (await searchReader.ReadAsync())
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < searchReader.FieldCount; i++)
                {
                    string colName = searchReader.GetName(i);
                    // Skip returning the embedding vector itself to save space
                    if (colName == embeddingColumnName) continue;
                    
                    row[colName] = searchReader.IsDBNull(i) ? null! : searchReader.GetValue(i);
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
