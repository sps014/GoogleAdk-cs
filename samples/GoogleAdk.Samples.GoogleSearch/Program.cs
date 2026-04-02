// ============================================================================
// Google Search Agent Sample
// ============================================================================
//
// Demonstrates Gemini's built-in Google Search grounding, which lets the model
// fetch real-time information from the web without custom tool code.
//
// Environment variables:
//   GOOGLE_GENAI_USE_VERTEXAI=True
//   GOOGLE_CLOUD_PROJECT=<your-project-id>
//   GOOGLE_CLOUD_LOCATION=us-central1
// ============================================================================

using GoogleAdk.Core;
using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Runner;
using GoogleAdk.Core.Tools;
using GoogleAdk.Models.Gemini;

AdkEnv.Load();

var model = GeminiModelFactory.Create("gemini-2.5-flash");

var searchAgent = new LlmAgent(new LlmAgentConfig
{
    Name = "search_agent",
    Description = "An agent with real-time web search capability via Gemini grounding.",
    Model = model,
    Instruction = """
        You are a helpful research assistant with real-time web access via Google Search.
        When asked about current events, recent data, or factual questions, use your
        search capability to find accurate, up-to-date information.
        Always cite your sources and provide context for your answers.
        """,
    Tools = new List<IBaseTool> { GoogleSearchTool.Instance },
});

var runner = new InMemoryRunner("google-search-sample", searchAgent);

// Create a persistent session so conversation history is preserved across turns
var session = await runner.SessionService.CreateSessionAsync(
    new GoogleAdk.Core.Abstractions.Sessions.CreateSessionRequest
    {
        AppName = "google-search-sample",
        UserId = "user-1",
    });

Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
Console.WriteLine("║  ADK C# — Google Search Grounding Sample                ║");
Console.WriteLine("║  Ask anything! The agent has real-time web access.      ║");
Console.WriteLine("║  Type 'quit' to exit.                                   ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
Console.WriteLine();

while (true)
{
    Console.Write("You: ");
    var input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input) || input.Equals("quit", StringComparison.OrdinalIgnoreCase))
        break;

    var userMessage = new Content
    {
        Role = "user",
        Parts = new List<Part> { new() { Text = input } }
    };

    Console.WriteLine();
    await foreach (var evt in runner.RunAsync("user-1", session.Id, userMessage))
    {
        var text = evt.Content?.Parts?.FirstOrDefault()?.Text;
        if (text != null && evt.Partial != true)
        {
            Console.WriteLine($"[{evt.Author}]: {text}");
            Console.WriteLine();
        }

        if (evt.GroundingMetadata != null)
        {
            Console.WriteLine("  [Grounding Metadata]");
            if (evt.GroundingMetadata.WebSearchQueries != null && evt.GroundingMetadata.WebSearchQueries.Count > 0)
            {
                Console.WriteLine($"    Search Queries: {string.Join(", ", evt.GroundingMetadata.WebSearchQueries)}");
            }
            if (evt.GroundingMetadata.SearchEntryPoint != null)
            {
                Console.WriteLine($"    Search Entry Point Data Keys: {string.Join(", ", evt.GroundingMetadata.SearchEntryPoint.Keys)}");
            }
        }
    }
    Console.WriteLine(new string('─', 60));
}
