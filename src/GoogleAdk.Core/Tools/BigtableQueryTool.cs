using Google.Cloud.Bigtable.V2;
using Google.Cloud.Bigtable.Common.V2;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Agents;

namespace GoogleAdk.Core.Tools;

public sealed class BigtableQueryTool : BaseTool
{
    public BigtableQueryTool()
        : base("bigtable_query", "Reads rows from a Cloud Bigtable instance.")
    {
    }

    public override async Task<object?> RunAsync(Dictionary<string, object?> args, AgentContext context)
    {
        var projectId = args.TryGetValue("projectId", out var projectIdObj) ? FunctionToolArgs.Get<string>(projectIdObj) : null;
        if (string.IsNullOrEmpty(projectId))
            return new Dictionary<string, object?> { ["error"] = "projectId is required." };

        var instanceId = args.TryGetValue("instanceId", out var instanceIdObj) ? FunctionToolArgs.Get<string>(instanceIdObj) : null;
        if (string.IsNullOrEmpty(instanceId))
            return new Dictionary<string, object?> { ["error"] = "instanceId is required." };

        var tableId = args.TryGetValue("tableId", out var tableIdObj) ? FunctionToolArgs.Get<string>(tableIdObj) : null;
        if (string.IsNullOrEmpty(tableId))
            return new Dictionary<string, object?> { ["error"] = "tableId is required." };

        var rowKey = args.TryGetValue("rowKey", out var rkObj) ? FunctionToolArgs.Get<string>(rkObj) : null;
        var rowPrefix = args.TryGetValue("rowPrefix", out var rpObj) ? FunctionToolArgs.Get<string>(rpObj) : null;
        var limitObj = args.GetValueOrDefault("limit");
        
        int limit = 10;
        if (limitObj is int l) limit = l;
        else if (limitObj is string s && int.TryParse(s, out int parsed)) limit = parsed;

        try
        {
            var client = await BigtableClient.CreateAsync();
            var tableName = new TableName(projectId, instanceId, tableId);
            
            var results = new List<Dictionary<string, object?>>();

            if (!string.IsNullOrEmpty(rowKey))
            {
                var row = await client.ReadRowAsync(tableName, rowKey);
                if (row != null)
                {
                    results.Add(ProcessRow(row));
                }
            }
            else if (!string.IsNullOrEmpty(rowPrefix))
            {
                var prefixBytes = System.Text.Encoding.UTF8.GetBytes(rowPrefix);
                var prefixEndBytes = System.Text.Encoding.UTF8.GetBytes(rowPrefix + (char)0xFFFF);
                
                var rowSet = RowSet.FromRowRanges(RowRange.ClosedOpen(
                    Google.Protobuf.ByteString.CopyFrom(prefixBytes),
                    Google.Protobuf.ByteString.CopyFrom(prefixEndBytes)));

                var stream = client.ReadRows(tableName, rowSet, RowFilters.PassAllFilter(), limit);
                await foreach (var row in stream)
                {
                    results.Add(ProcessRow(row));
                }
            }
            else
            {
                var stream = client.ReadRows(tableName, RowSet.FromRowRanges(RowRange.ClosedOpen(Google.Protobuf.ByteString.Empty, Google.Protobuf.ByteString.Empty)), RowFilters.PassAllFilter(), limit);
                await foreach (var row in stream)
                {
                    results.Add(ProcessRow(row));
                }
            }

            return new Dictionary<string, object?>
            {
                ["status"] = "SUCCESS",
                ["rows"] = results
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

    private Dictionary<string, object?> ProcessRow(Row row)
    {
        var dict = new Dictionary<string, object?>();
        dict["rowKey"] = row.Key.ToStringUtf8();

        foreach (var family in row.Families)
        {
            var cols = new Dictionary<string, object?>();
            foreach (var col in family.Columns)
            {
                var cells = col.Cells.Select(c => new Dictionary<string, object?>
                {
                    ["value"] = c.Value.ToStringUtf8(),
                    ["timestamp"] = c.TimestampMicros
                }).ToList();
                cols[col.Qualifier.ToStringUtf8()] = cells;
            }
            dict[family.Name] = cols;
        }
        return dict;
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
                    ["instanceId"] = new Dictionary<string, object?>
                    {
                        ["type"] = "string",
                        ["description"] = "The Bigtable instance ID."
                    },
                    ["tableId"] = new Dictionary<string, object?>
                    {
                        ["type"] = "string",
                        ["description"] = "The Bigtable table ID."
                    },
                    ["rowKey"] = new Dictionary<string, object?>
                    {
                        ["type"] = "string",
                        ["description"] = "Optional. Specific row key to fetch."
                    },
                    ["rowPrefix"] = new Dictionary<string, object?>
                    {
                        ["type"] = "string",
                        ["description"] = "Optional. Prefix to fetch multiple rows."
                    },
                    ["limit"] = new Dictionary<string, object?>
                    {
                        ["type"] = "integer",
                        ["description"] = "Optional. Maximum number of rows to return (default 10)."
                    }
                },
                ["required"] = new[] { "projectId", "instanceId", "tableId" }
            }
        };
    }
}
