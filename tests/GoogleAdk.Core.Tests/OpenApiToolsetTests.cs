using GoogleAdk.Core.Agents;
using GoogleAdk.Tools.OpenApi;

namespace GoogleAdk.Core.Tests;

public class OpenApiToolsetTests
{
    private const string OpenApiSpec = """
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

    [Fact]
    public void OpenApiToolset_ParsesToolsCorrectly()
    {
        // Arrange
        var toolset = new OpenAPIToolset(OpenApiSpec, "json");

        // Act
        var tools = toolset.GetTools().ToList();

        // Assert
        Assert.Equal(2, tools.Count);
        Assert.Contains(tools, t => t.Name == "listPosts");
        Assert.Contains(tools, t => t.Name == "getPost");
    }

    [Fact]
    public void OpenApiTool_GeneratesCorrectDeclaration()
    {
        // Arrange
        var toolset = new OpenAPIToolset(OpenApiSpec, "json");
        var tool = toolset.GetTools().First(t => t.Name == "getPost");

        // Act
        var decl = tool.GetDeclaration();

        // Assert
        Assert.NotNull(decl);
        Assert.Equal("getPost", decl.Name);
        Assert.Equal("Get post by ID", decl.Description);
        Assert.NotNull(decl.Parameters);

        var required = decl.Parameters["required"] as List<string>;
        Assert.NotNull(required);
        Assert.Contains("id", required);

        var properties = decl.Parameters["properties"] as Dictionary<string, object?>;
        Assert.NotNull(properties);
        Assert.True(properties.ContainsKey("id"));
    }
    
    [Fact]
    public async Task OpenApiToolset_GetToolsAsyncReturnsAllTools()
    {
        // Arrange
        var toolset = new OpenAPIToolset(OpenApiSpec, "json");
        var context = new AgentContext(new InvocationContext { Session = GoogleAdk.Core.Abstractions.Sessions.Session.Create("s1", "app", "u1") });

        // Act
        var asyncTools = await toolset.GetToolsAsync(context);

        // Assert
        Assert.Equal(2, asyncTools.Count);
    }
}
