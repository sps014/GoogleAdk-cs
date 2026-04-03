using GoogleAdk.Core;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Runner;
using GoogleAdk.Core.Tools;
using GoogleAdk.ApiServer;
using GoogleAdk.Models.Gemini;

AdkEnv.Load();

Console.WriteLine("==> Demo: Vertex AI Search Tool\n");

// Ensure you have configured Google Cloud credentials (e.g., via gcloud auth application-default login)
// and set your project/location/collection/dataStore below.

var projectId = Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT");
var location = "global";
var datastoreId = Environment.GetEnvironmentVariable("GOOGLE_CLOUD_DATASTORE") ;

var dataStoreId = $"projects/{projectId}/locations/{location}/collections/default_collection/dataStores/{datastoreId}";

// For this demo, we'll configure the agent and tool to show how it's done.
var vertexSearchTool = new VertexAiSearchTool(dataStoreId: dataStoreId);

var agent = new LlmAgent(new LlmAgentConfig
{
    Name = "search_agent",
    Model = "gemini-2.5-pro",
    Instruction = "You are a helpful assistant. Always Use the Vertex AI Search tool to find information by reforming user query cleanly.",
    Tools = [vertexSearchTool]
});

Console.WriteLine($"Agent configured with tool: {vertexSearchTool.Name}");
Console.WriteLine($"DataStore ID: {vertexSearchTool.DataStoreId}\n");


if(args.Contains("--web"))
{
    await AdkServer.RunAsync(agent);
    return;
}

var runner = new InMemoryRunner("vertex-search-app", agent);
await runner.SessionService.CreateSessionAsync(new GoogleAdk.Core.Abstractions.Sessions.CreateSessionRequest
{
    AppName = "vertex-search-app",
    UserId = "user-1",
    SessionId = "session-1"
});

var content = new Content
{
    Role = "user",
    Parts = new List<Part> { new Part { Text = "" } }
};

Console.WriteLine("User: " + content.Parts[0].Text);

try
{
    Console.Write("Agent: ");
    GoogleAdk.Core.Abstractions.Models.GroundingMetadata? finalGrounding = null;
    await foreach (var evt in runner.RunAsync("user-1", "session-1", content))
    {
        if (evt.Content?.Parts?.FirstOrDefault()?.Text is string text)
        {
            Console.Write(text);
        }
        if (evt.GroundingMetadata != null)
        {
            finalGrounding = evt.GroundingMetadata;
        }
    }
    Console.WriteLine();
    
    if (finalGrounding != null)
    {
        Console.WriteLine("\n[Grounding Metadata]");
        if (finalGrounding.WebSearchQueries != null && finalGrounding.WebSearchQueries.Count > 0)
        {
            Console.WriteLine($"- Web Search Queries: {string.Join(", ", finalGrounding.WebSearchQueries)}");
        }
        if (finalGrounding.SearchEntryPoint != null)
        {
            Console.WriteLine($"- Search Entry Point Data Keys: {string.Join(", ", finalGrounding.SearchEntryPoint.Keys)}");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"\n\n[Note] This sample requires a valid Vertex AI DataStore ID and credentials.");
    Console.WriteLine($"Error: {ex.Message}");
}

Console.WriteLine("\nDone!");
