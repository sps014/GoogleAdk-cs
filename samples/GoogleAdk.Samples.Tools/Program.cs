// ============================================================================
// Tools Sample — LLM + Auth, Bash, Grounding
// ============================================================================

using GoogleAdk.Core;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Abstractions.Sessions;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Runner;
using GoogleAdk.Core.Tools;
using GoogleAdk.Samples.Tools;

AdkEnv.Load();

Console.WriteLine("=== Tools Sample (LLM) ===\n");

var authConfig = SampleToolFunctions.WeatherAuthConfig;
var weatherTool = SampleToolFunctions.WeatherLookupTool;

var bashTool = new ExecuteBashTool(["echo", "dir"]);
var groundingTools = new BaseTool[]
{
    new DiscoveryEngineSearchTool(),
    new UrlContextTool()
};

var agent = new LlmAgent(new LlmAgentConfig
{
    Name = "tools",
    Model = "gemini-2.5-flash",
    Instruction = "Use weather_lookup for weather and bash for shell tasks.",
    Tools = [weatherTool, bashTool, .. groundingTools],
    BeforeModelCallback = (_, request) =>
    {
        var toolCount = request.Config?.Tools?.Count ?? 0;
        Console.WriteLine($"Tool declarations added to request: {toolCount}");
        return Task.FromResult<GoogleAdk.Core.Abstractions.Events.LlmResponse?>(null);
    }
});

// ── Console mode ───────────────────────────────────────────────────────────

await ConsoleRunner.RunAsync(agent);

Console.WriteLine("\n=== Tools Sample Complete ===");
