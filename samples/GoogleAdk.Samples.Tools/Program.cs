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
    BeforeModelCallbacks =
    [
        (_, request) =>
        {
            var toolCount = request.Config?.Tools?.Count ?? 0;
            Console.WriteLine($"Tool declarations added to request: {toolCount}");
            return Task.FromResult<GoogleAdk.Core.Abstractions.Models.LlmResponse?>(null);
        }
    ]
});

var runner = new InMemoryRunner("tools-sample", agent);
var session = await runner.SessionService.CreateSessionAsync(new CreateSessionRequest
{
    AppName = "tools-sample",
    UserId = "user-1"
});

var userMessage = new Content
{
    Role = "user",
    Parts =
    [
        new Part
        {
            Text = "Check the weather in Seattle, then run `echo tools-ok` using the bash tool."
        }
    ]
};

var authState = new Dictionary<string, object?>
{
    ["temp:" + authConfig.CredentialKey] = authConfig.RawAuthCredential
};

Console.WriteLine("User: Check the weather in Seattle, then run `echo tools-ok`.\n");

await foreach (var evt in runner.RunAsync("user-1", session.Id, userMessage, authState))
{
    foreach (var call in evt.GetFunctionCalls())
        Console.WriteLine($"Tool call: {call.Name}");

    foreach (var response in evt.GetFunctionResponses())
    {
        var responseText = response.Response != null
            ? string.Join(", ", response.Response.Select(kv => $"{kv.Key}={kv.Value}"))
            : "(null)";
        Console.WriteLine($"Tool response ({response.Name}): {responseText}");
    }

    if (evt.IsFinalResponse() && evt.Content?.Parts != null)
    {
        foreach (var part in evt.Content.Parts)
        {
            if (!string.IsNullOrWhiteSpace(part.Text))
                Console.WriteLine($"Agent: {part.Text}");
        }
    }
}

Console.WriteLine("\n=== Tools Sample Complete ===");
