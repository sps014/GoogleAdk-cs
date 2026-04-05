# Tools

Tools provide your agents with the ability to interact with the outside world, fetch data, and perform actions. The ADK includes built-in tools, supports dynamic tool generation via source generators, and allows custom tool implementation.

## Built-in Tools

The ADK comes with several built-in tools ready to be used:

- **`GoogleSearchTool`**: Performs live web searches. Returns grounding metadata with search queries.
- **`VertexAiSearchTool`**: Connects to Google Cloud Vertex AI Search (Discovery Engine) data stores.
- **`AuthTool`**: Triggers a credential request flow for authenticating users.
- **`AgentTool`**: Wraps an entire sub-agent as a callable tool, enabling hierarchical agent orchestration.
- **`BigQueryQueryTool` & `BigQueryMetadataTool`**: Execute queries and fetch schema metadata from Google Cloud BigQuery.
- **`SpannerQueryTool`**: Execute SQL queries against Google Cloud Spanner databases.
- **`BigtableQueryTool`**: Read rows and ranges from Google Cloud Bigtable.
- **`PubSubMessageTool`**: Publish messages to Google Cloud Pub/Sub topics.
- **`GoogleApiTool`**: Dynamically call Google Cloud APIs using the Discovery API.
- **`ApiHubTool`**: Search and discover enterprise APIs in Google Cloud API Hub.
- **`VertexAiRagRetrievalTool`**: Execute RAG retrieval queries using Vertex AI Search corpora and data stores.

## Source Generated Tools (Recommended)

The easiest and most robust way to create tools in C# is by using the ADK's source generators. By decorating a static method with `[FunctionTool]`, the ADK automatically generates the required JSON schema, parameter parsing, and execution boilerplate.

### 1. Define your tools

Create a `public static partial class` and add your methods. The XML documentation (`<summary>`, `<param>`) is parsed and injected directly into the LLM's system prompt to explain how to use the tool.

```csharp
using GoogleAdk.Core.Abstractions.Tools;

public static partial class WeatherTools
{
    /// <summary>Gets the current weather for a specified city.</summary>
    /// <param name="city">The name of the city (e.g., "Seattle").</param>
    /// <param name="units">The temperature units to use.</param>
    [FunctionTool]
    public static object? GetWeather(string city, string units = "celsius")
    {
        // Your logic here
        return new { city, temperature = 22, condition = "Sunny", units };
    }
}
```

### 2. Use the generated tool

The source generator automatically creates a static property appended with `Tool`.

```csharp
var agent = new LlmAgent(new LlmAgentConfig
{
    Name = "weather_agent",
    Model = "gemini-2.5-flash",
    // Use the auto-generated GetWeatherTool property
    Tools = [WeatherTools.GetWeatherTool]
});
```

## Toolsets

Toolsets (`BaseToolset`) are dynamic collections of tools that can be resolved at runtime. They are ideal for wrapping external APIs or protocols.

### OpenAPIToolset

Automatically generate tools from an OpenAPI/Swagger specification.

```csharp
using GoogleAdk.Tools.OpenApi;

var openApiSpec = """
{
  "openapi": "3.0.0",
  "info": { "title": "Pet Store API", "version": "1.0" },
  "paths": {
    "/pets": { 
      "get": { 
        "operationId": "getPets", 
        "responses": { "200": { "description": "OK" } } 
      } 
    }
  }
}
""";

// Parse the spec and expose its operations as tools
var toolset = new OpenAPIToolset(openApiSpec, "json");

var agent = new LlmAgent(new LlmAgentConfig
{
    Name = "api_agent",
    Model = "gemini-2.5-flash",
    Toolsets = [toolset]
});
```

### AgentTool (Hierarchical Orchestration)

You can wrap an existing agent as a tool. When the LLM decides it needs the sub-agent's expertise, it will call the tool, passing the context to the sub-agent.

```csharp
var summarizeAgent = new LlmAgent(new LlmAgentConfig
{
    Name = "summarizer",
    Model = "gemini-2.5-flash",
    Instruction = "You summarize lengthy texts into 3 bullet points."
});

var coordinatorAgent = new LlmAgent(new LlmAgentConfig
{
    Name = "coordinator",
    Model = "gemini-2.5-flash",
    Tools = [new AgentTool(summarizeAgent)]
});
```

## Custom Tools (Manual Implementation)

If you need absolute control over the schema or execution logic, inherit from `BaseTool`.

```csharp
using GoogleAdk.Core.Abstractions.Tools;
using GoogleAdk.Core.Context;
using System.Threading.Tasks;

public class MyCustomTool : BaseTool
{
    public MyCustomTool() : base("calculate_tax", "Calculates tax for an amount") { }
    
    public override FunctionDeclaration? GetDeclaration() 
    { 
        // Return your manually constructed JSON schema here
        return new FunctionDeclaration
        {
            Name = Name,
            Description = Description,
            Parameters = new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?>
                {
                    ["amount"] = new Dictionary<string, object?> { ["type"] = "number" }
                },
                ["required"] = new[] { "amount" }
            }
        };
    }
    
    public override Task<object?> RunAsync(Dictionary<string, object?> args, AgentContext context) 
    { 
        if (args.TryGetValue("amount", out var amt) && amt is double amount)
        {
            return Task.FromResult<object?>(new { tax = amount * 0.2 });
        }
        return Task.FromResult<object?>(new { error = "Invalid amount" });
    }
}
```