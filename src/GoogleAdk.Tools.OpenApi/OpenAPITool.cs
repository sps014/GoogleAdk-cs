using GoogleAdk.Core;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Agents;
using Microsoft.OpenApi.Models;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GoogleAdk.Tools.OpenApi;

/// <summary>
/// A tool representing a single operation from an OpenAPI specification.
/// </summary>
public class OpenAPITool : BaseTool
{
    private readonly OpenApiDocument _document;
    private readonly string _path;
    private readonly OperationType _method;
    private readonly OpenApiOperation _operation;
    private readonly string _baseUrl;
    private readonly HttpClient _httpClient;

    public OpenAPITool(
        OpenApiDocument document,
        string path,
        OperationType method,
        OpenApiOperation operation,
        string baseUrl,
        HttpClient httpClient)
        : base(
              name: operation.OperationId ?? SanitizeName($"{method}_{path}"),
              description: operation.Summary ?? operation.Description ?? $"Executes {method} {path}")
    {
        _document = document;
        _path = path;
        _method = method;
        _operation = operation;
        _baseUrl = baseUrl.TrimEnd('/');
        _httpClient = httpClient;
    }

    public override FunctionDeclaration? GetDeclaration()
    {
        var parameters = new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object?>()
        };
        var required = new List<string>();

        var properties = (Dictionary<string, object?>)parameters["properties"]!;

        // Handle path and query parameters
        foreach (var param in _operation.Parameters)
        {
            var paramSchema = new Dictionary<string, object?>
            {
                ["type"] = GetJsonSchemaType(param.Schema)
            };
            
            if (!string.IsNullOrEmpty(param.Description))
            {
                paramSchema["description"] = param.Description;
            }

            properties[param.Name] = paramSchema;

            if (param.Required)
            {
                required.Add(param.Name);
            }
        }

        // Add body parameters if any (basic support for application/json)
        if (_operation.RequestBody?.Content != null && _operation.RequestBody.Content.TryGetValue("application/json", out var mediaType))
        {
            var schema = mediaType.Schema;
            if (schema != null)
            {
                if (schema.Type == "object" && schema.Properties != null)
                {
                    foreach (var prop in schema.Properties)
                    {
                        var propSchema = new Dictionary<string, object?>
                        {
                            ["type"] = GetJsonSchemaType(prop.Value)
                        };
                        if (!string.IsNullOrEmpty(prop.Value.Description))
                        {
                            propSchema["description"] = prop.Value.Description;
                        }
                        properties[prop.Key] = propSchema;
                        if (schema.Required.Contains(prop.Key))
                        {
                            required.Add(prop.Key);
                        }
                    }
                }
                else
                {
                    properties["requestBody"] = new Dictionary<string, object?>
                    {
                        ["type"] = GetJsonSchemaType(schema),
                        ["description"] = "Request body content"
                    };
                    if (_operation.RequestBody.Required)
                    {
                        required.Add("requestBody");
                    }
                }
            }
        }

        if (required.Count > 0)
        {
            parameters["required"] = required;
        }

        return new FunctionDeclaration
        {
            Name = Name,
            Description = Description,
            Parameters = parameters
        };
    }

    public override async Task<object?> RunAsync(Dictionary<string, object?> args, AgentContext context)
    {
        var requestUrl = _baseUrl + _path;
        var queryParams = new List<string>();

        var requestMessage = new HttpRequestMessage(new HttpMethod(_method.ToString().ToUpperInvariant()), requestUrl);

        // Map arguments to path, query, and body
        foreach (var param in _operation.Parameters)
        {
            if (args.TryGetValue(param.Name, out var val) && val != null)
            {
                var stringVal = val.ToString();
                if (param.In == ParameterLocation.Path)
                {
                    requestUrl = requestUrl.Replace($"{{{param.Name}}}", Uri.EscapeDataString(stringVal!));
                }
                else if (param.In == ParameterLocation.Query)
                {
                    queryParams.Add($"{Uri.EscapeDataString(param.Name)}={Uri.EscapeDataString(stringVal!)}");
                }
                else if (param.In == ParameterLocation.Header)
                {
                    requestMessage.Headers.Add(param.Name, stringVal);
                }
            }
            else if (param.Required)
            {
                throw new ArgumentException($"Missing required parameter: {param.Name}");
            }
        }

        if (queryParams.Count > 0)
        {
            requestUrl += "?" + string.Join("&", queryParams);
        }

        requestMessage.RequestUri = new Uri(requestUrl);

        // Handle body if present
        if (_operation.RequestBody != null && _operation.RequestBody.Content.ContainsKey("application/json"))
        {
            var bodyArgs = new Dictionary<string, object?>();
            var schema = _operation.RequestBody.Content["application/json"].Schema;
            if (schema != null && schema.Type == "object")
            {
                foreach (var prop in schema.Properties)
                {
                    if (args.TryGetValue(prop.Key, out var val))
                    {
                        bodyArgs[prop.Key] = val;
                    }
                }
            }
            else if (args.TryGetValue("requestBody", out var val))
            {
                // single value body
                requestMessage.Content = new StringContent(JsonSerializer.Serialize(val), System.Text.Encoding.UTF8, "application/json");
            }
            
            if (bodyArgs.Count > 0)
            {
                requestMessage.Content = new StringContent(JsonSerializer.Serialize(bodyArgs), System.Text.Encoding.UTF8, "application/json");
            }
        }

        var response = await _httpClient.SendAsync(requestMessage);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return new { error = $"HTTP {response.StatusCode}", message = content };
        }

        try
        {
            return JsonSerializer.Deserialize<JsonElement>(content);
        }
        catch
        {
            return content; // return as string if not JSON
        }
    }

    private static string SanitizeName(string raw)
    {
        return System.Text.RegularExpressions.Regex.Replace(raw, "[^a-zA-Z0-9_-]", "_").ToLowerInvariant();
    }

    private static string GetJsonSchemaType(OpenApiSchema schema)
    {
        var type = schema.Type?.ToLowerInvariant();
        return type switch
        {
            "integer" => "integer",
            "number" => "number",
            "boolean" => "boolean",
            "string" => "string",
            "array" => "array",
            "object" => "object",
            _ => "string" // default
        };
    }
}
