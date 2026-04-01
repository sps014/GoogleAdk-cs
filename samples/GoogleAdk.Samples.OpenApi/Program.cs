using GoogleAdk.Core.Agents;
using GoogleAdk.Models.Gemini;
using GoogleAdk.Tools.OpenApi;

Console.WriteLine("==> Demo: OpenAPIToolset with JSONPlaceholder API\n");

var openApiSpec = """
{
  "openapi": "3.0.0",
  "info": {
    "title": "JSONPlaceholder API",
    "version": "1.0.0"
  },
  "servers": [
    { "url": "https://jsonplaceholder.typicode.com" }
  ],
  "paths": {
    "/posts": {
      "get": {
        "operationId": "listPosts",
        "summary": "Get all posts",
        "responses": { "200": { "description": "Success" } }
      }
    },
    "/posts/{id}": {
      "get": {
        "operationId": "getPost",
        "summary": "Get post by ID",
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": { "type": "integer" }
          }
        ],
        "responses": { "200": { "description": "Success" } }
      }
    }
  }
}
""";

// Create toolset from OpenAPI spec
var toolset = new OpenAPIToolset(openApiSpec, "json");

// Example: Display the generated tools
Console.WriteLine("Discovered tools:");
foreach (var tool in toolset.GetTools())
{
    Console.WriteLine($"- {tool.Name}: {tool.Description}");
}

Console.WriteLine();

// Let's actually execute one of the tools directly!
Console.WriteLine("Executing listPosts tool manually...");
var listPostsTool = toolset.GetTools().First(t => t.Name == "listPosts");
var context = new AgentContext(new InvocationContext { Session = GoogleAdk.Core.Abstractions.Sessions.Session.Create("s1", "app", "u1") });
var result = await listPostsTool.RunAsync(new Dictionary<string, object?>(), context);

// result is a JsonElement, let's just print its array length
if (result is System.Text.Json.JsonElement el && el.ValueKind == System.Text.Json.JsonValueKind.Array)
{
    Console.WriteLine($"Got {el.GetArrayLength()} posts from API!");
}
else
{
    Console.WriteLine(result);
}

// You can also use it with an Agent!
GeminiModelFactory.RegisterDefaults();
var agent = new LlmAgent(new LlmAgentConfig
{
    Name = "openapi_agent",
    ModelName = "gemini-2.5-flash",
    Instruction = "Use tools to find the requested data.",
    Tools = new List<GoogleAdk.Core.Abstractions.Events.IBaseTool> { toolset }
});

Console.WriteLine("\nDone!");
