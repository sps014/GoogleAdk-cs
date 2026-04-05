using Google.Cloud.BigQuery.V2;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Agents;

namespace GoogleAdk.Core.Tools;

public sealed class BigQueryMetadataTool : BaseTool
{
    public BigQueryMetadataTool()
        : base("bigquery_metadata", "Retrieves metadata for BigQuery datasets and tables.")
    {
    }

    public override async Task<object?> RunAsync(Dictionary<string, object?> args, AgentContext context)
    {
        var projectId = args.TryGetValue("projectId", out var projectIdObj) ? FunctionToolArgs.Get<string>(projectIdObj) : null;
        if (string.IsNullOrEmpty(projectId))
            return new Dictionary<string, object?> { ["error"] = "projectId is required." };

        var datasetId = args.TryGetValue("datasetId", out var dObj) ? FunctionToolArgs.Get<string>(dObj) : null;
        var tableId = args.TryGetValue("tableId", out var tObj) ? FunctionToolArgs.Get<string>(tObj) : null;

        try
        {
            var client = await BigQueryClient.CreateAsync(projectId);

            if (string.IsNullOrEmpty(datasetId))
            {
                var datasets = new List<string>();
                var listDatasets = client.ListDatasets();
                foreach (var dataset in listDatasets)
                {
                    datasets.Add(dataset.Reference.DatasetId);
                }
                return new Dictionary<string, object?>
                {
                    ["status"] = "SUCCESS",
                    ["datasets"] = datasets
                };
            }

            if (string.IsNullOrEmpty(tableId))
            {
                var dataset = await client.GetDatasetAsync(datasetId);
                var tables = new List<string>();
                var listTables = client.ListTables(datasetId);
                foreach (var table in listTables)
                {
                    tables.Add(table.Reference.TableId);
                }
                return new Dictionary<string, object?>
                {
                    ["status"] = "SUCCESS",
                    ["dataset_info"] = dataset.Resource.Description,
                    ["tables"] = tables
                };
            }

            var tableInfo = await client.GetTableAsync(datasetId, tableId);
            var fields = tableInfo.Schema.Fields.Select(f => new Dictionary<string, object?>
            {
                ["name"] = f.Name,
                ["type"] = f.Type.ToString(),
                ["description"] = f.Description
            }).ToList();

            return new Dictionary<string, object?>
            {
                ["status"] = "SUCCESS",
                ["table_info"] = new Dictionary<string, object?>
                {
                    ["id"] = tableInfo.Reference.TableId,
                    ["description"] = tableInfo.Resource.Description,
                    ["schema"] = fields,
                    ["numRows"] = tableInfo.Resource.NumRows
                }
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
                    ["datasetId"] = new Dictionary<string, object?>
                    {
                        ["type"] = "string",
                        ["description"] = "Optional. The BigQuery dataset ID."
                    },
                    ["tableId"] = new Dictionary<string, object?>
                    {
                        ["type"] = "string",
                        ["description"] = "Optional. The BigQuery table ID."
                    }
                },
                ["required"] = new[] { "projectId" }
            }
        };
    }
}
