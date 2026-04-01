# Tools

## Built-in Tools

- GoogleSearchTool
- LoadMemoryTool
- LoadArtifactsTool
- AuthTool (credential requests)

### Tool API basics

- `BaseTool.RunAsync(args, context)` executes the tool
- `BaseTool.ProcessLlmRequestAsync` can modify the request (function schema, etc.)

## Source Generated Tools

The ADK uses C# source generators to automatically create `IBaseTool` instances from your static methods. This eliminates the need to manually write JSON schema boilerplate.

### How to use source generated tools

1. Create a `public static partial class`.
2. Add a static method decorated with `[FunctionTool]`.
3. Add XML documentation comments to describe the tool and its parameters (the LLM uses these descriptions).
4. The source generator automatically creates a static property named `{MethodName}Tool`.

```csharp
using GoogleAdk.Core.Abstractions.Tools;

public static partial class MyTools
{
    /// <summary>Gets the current weather for a city.</summary>
    /// <param name="city">The city name</param>
    [FunctionTool]
    public static object? GetWeather(string city)
    {
        return new { city, temperature = 22, condition = "Sunny" };
    }
}
```

You can then pass the generated tool directly to your agent:

```csharp
var agent = new LlmAgent(new LlmAgentConfig
{
    Name = "weather_agent",
    ModelName = "gemini-2.5-flash",
    Tools = [MyTools.GetWeatherTool] // Auto-generated property
});
```

### Example: Custom Tool (Manual)

If you need more control than the source generator provides, you can implement `BaseTool` manually:

```csharp
public class MyCustomTool : BaseTool
{
    public MyCustomTool() : base("my_tool", "Does something useful") { }
    
    public override FunctionDeclaration? GetDeclaration() 
    { 
        // Return manual schema here
    }
    
    public override async Task<object?> RunAsync(Dictionary<string, object?> args, AgentContext context) 
    { 
        // Implementation here
    }
}
```

## Toolsets

- MCP toolset
- OpenAPIToolset (parses OpenAPI specs into tools)
- AgentTool for agent-as-tool composition

### Example: OpenAPIToolset

```csharp
using GoogleAdk.Tools.OpenApi;

var openApiSpec = """
{
  "openapi": "3.0.0",
  "info": { "title": "API", "version": "1.0" },
  "paths": {
    "/ping": { "get": { "operationId": "ping", "responses": { "200": { "description": "OK" } } } }
  }
}
""";

var toolset = new OpenAPIToolset(openApiSpec, "json");

var agent = new LlmAgent(new LlmAgentConfig
{
    Name = "api_agent",
    ModelName = "gemini-2.5-flash",
    Toolsets = new List<BaseToolset> { toolset }
});
```

### Example: GoogleSearchTool

```csharp
var agent = new LlmAgent(new LlmAgentConfig
{
    Name = "search",
    ModelName = "gemini-2.5-flash",
    Tools = [new GoogleSearchTool()]
});
```

### Example: AuthTool

```csharp
var agent = new LlmAgent(new LlmAgentConfig
{
    Name = "auth",
    ModelName = "gemini-2.5-flash",
    Tools = [new AuthTool()]
});
```

### Example: AgentTool

```csharp
var subAgent = new LlmAgent(new LlmAgentConfig
{
    Name = "sub",
    ModelName = "gemini-2.5-flash",
    Instruction = "Summarize."
});

var root = new LlmAgent(new LlmAgentConfig
{
    Name = "root",
    ModelName = "gemini-2.5-flash",
    Tools = [new AgentTool(subAgent)]
});
```

## Samples

- `GoogleAdk/samples/GoogleAdk.Samples.GoogleSearch/Program.cs`
- `GoogleAdk/samples/GoogleAdk.Samples.SubAgents/Program.cs`

## Placeholders

- Additional built-ins: _coming soon_
