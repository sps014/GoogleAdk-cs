# Tools

Tools provide your agents with the ability to interact with the outside world, fetch data, and perform actions. The ADK includes built-in tools, supports dynamic tool generation via source generators, and allows custom tool implementation.

## Built-in Tools

The ADK comes with several built-in tools ready to be used:

- **`GoogleSearchTool`**: Performs live web searches. Returns grounding metadata with search queries.
- **`VertexAiSearchTool`**: Connects to Google Cloud Vertex AI Search (Discovery Engine) data stores.
- **`AuthTool`**: Triggers a credential request flow for authenticating users.
- **`AgentTool`**: Wraps an entire sub-agent as a callable tool, enabling hierarchical agent orchestration.
- **`ComputerUseTool`**: Wraps computer control functions for use with LLMs, automating tasks like clicking, typing, and scrolling.
- **`BigQueryQueryTool` & `BigQueryMetadataTool`**: Execute queries and fetch schema metadata from Google Cloud BigQuery.
- **`SpannerQueryTool`**: Execute SQL queries against Google Cloud Spanner databases.
- **`SpannerSearchTool`**: Perform vector similarity search in a Cloud Spanner database using a text query.
- **`BigtableQueryTool`**: Read rows and ranges from Google Cloud Bigtable.
- **`PubSubMessageTool`**: Publish messages to Google Cloud Pub/Sub topics.
- **`GoogleApiTool`**: Dynamically call Google Cloud APIs using the Discovery API.
- **`ApiHubTool`**: Search and discover enterprise APIs in Google Cloud API Hub.
- **`DiscoveryEngineSearchTool`**: Perform searches against Google Cloud Discovery Engine (Vertex AI Search) datastores.
- **`VertexAiRagRetrievalTool`**: Execute RAG retrieval queries using Vertex AI Search corpora and data stores.

### Using Built-in Cloud Tools

You can provide your agent with access to Google Cloud systems by instantiating these tools and adding them to the agent's configuration. The LLM will automatically understand the tool's required parameters (e.g., `projectId`, `instanceId`, `query`) and invoke them when necessary.

#### BigQuery

The BigQuery tools allow the LLM to explore and query datasets.
- **`BigQueryMetadataTool`**: Retrieves metadata for datasets, tables, schemas, and descriptions. Requires `projectId` (passed by the LLM). Optional: `datasetId`, `tableId`.
- **`BigQueryQueryTool`**: Executes SQL queries against BigQuery. Requires `projectId` and `query` (both determined and passed by the LLM).

```csharp
var agent = new LlmAgent(new LlmAgentConfig
{
    Name = "bq_agent",
    Model = "gemini-2.5-flash",
    Instruction = "You are a data analyst. Explore the dataset and answer questions.",
    Tools = [new BigQueryQueryTool(), new BigQueryMetadataTool()]
});
```

#### Cloud Spanner

The **`SpannerQueryTool`** allows the LLM to execute SQL queries on a Spanner database. It requires `projectId`, `instanceId`, `databaseId`, and `query`.

The **`SpannerSearchTool`** performs vector similarity search using a text query. It requires `projectId`, `instanceId`, `databaseId`, `tableName`, `query`, `embeddingColumnName`, and `modelName`.

```csharp
var agent = new LlmAgent(new LlmAgentConfig
{
    Name = "spanner_agent",
    Model = "gemini-2.5-flash",
    Instruction = "You are a database admin. Query Spanner to check user counts. Use instance 'prod-instance' and database 'main-db'.",
    Tools = [new SpannerQueryTool(), new SpannerSearchTool()]
});
```

#### Cloud Bigtable

The **`BigtableQueryTool`** reads rows and row ranges from Bigtable. It requires `projectId`, `instanceId`, `tableId`. Optional filters: `rowKey`, `rowPrefix`, and `limit`.

```csharp
var agent = new LlmAgent(new LlmAgentConfig
{
    Name = "bigtable_agent",
    Model = "gemini-2.5-flash",
    Instruction = "You are a data explorer. Find user profiles in Bigtable.",
    Tools = [new BigtableQueryTool()]
});
```

#### Cloud Pub/Sub

The **`PubSubMessageTool`** publishes messages to Pub/Sub topics. It requires `projectId`, `topicId`, and `message`.

```csharp
var agent = new LlmAgent(new LlmAgentConfig
{
    Name = "pubsub_agent",
    Model = "gemini-2.5-flash",
    Instruction = "You are an event emitter. If the user asks to trigger a build, publish a message to the 'builds' topic.",
    Tools = [new PubSubMessageTool()]
});
```

#### Google API Discovery & API Hub

- **`GoogleApiTool`**: Dynamically calls Google Cloud APIs using the Discovery API. Requires `apiName` and `apiVersion`.
- **`ApiHubTool`**: Searches and discovers enterprise APIs registered in Google Cloud API Hub. Requires `projectId` and `location`.

```csharp
var agent = new LlmAgent(new LlmAgentConfig
{
    Name = "discovery_agent",
    Model = "gemini-2.5-flash",
    Tools = [new GoogleApiTool(), new ApiHubTool()]
});
```

#### Computer Use
The **`ComputerUseTool`** allows the LLM to perform automated browser or computer interaction tasks. It normalizes virtual coordinates to the actual screen size using a driver instance.

```csharp
var driver = new PlaywrightDriver();
var agent = new LlmAgent(new LlmAgentConfig
{
    Name = "computer_use_agent",
    Model = "gemini-2.5-flash",
    Instruction = "You are a web automation bot. Open google.com and search for ADK.",
    Tools = [new ComputerUseTool(driver)]
});
```

#### Discovery Engine Search

The **`DiscoveryEngineSearchTool`** allows the LLM to search across a Discovery Engine (Vertex AI Search) datastore or engine. It must be initialized with either a `datastore` or an `engine` resource name.

```csharp
var searchTool = new DiscoveryEngineSearchTool(
    datastore: "projects/my-project/locations/global/collections/default_collection/dataStores/my-docs"
);

var agent = new LlmAgent(new LlmAgentConfig
{
    Name = "search_agent",
    Model = "gemini-2.5-flash",
    Instruction = "You are a helpful assistant. Use the search tool to find answers in the company docs.",
    Tools = [searchTool]
});
```

#### Vertex AI RAG Retrieval

The **`VertexAiRagRetrievalTool`** integrates directly with Vertex AI Search (Discovery Engine) data stores or corpora for Retrieval-Augmented Generation (RAG). Note that this maps to a built-in retrieval configuration rather than a function call.

```csharp
var ragTool = new VertexAiRagRetrievalTool(
    ragResources: [
        new VertexAiSearchDataStoreSpec { DataStore = "projects/my-project/locations/global/collections/default_collection/dataStores/my-docs" }
    ],
    ragCorpora: ["projects/my-project/locations/us-central1/ragCorpora/1234567890"],
    similarityTopK: 5,
    vectorDistanceThreshold: 0.3
);

var agent = new LlmAgent(new LlmAgentConfig
{
    Name = "rag_agent",
    Model = "gemini-2.5-flash",
    Tools = [ragTool]
});
```

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
            Parameters = new Schema
            {
                Type = "object",
                Properties = new Dictionary<string, Schema>
                {
                    ["amount"] = new Schema { Type = "number" }
                },
                Required = new List<string> { "amount" }
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