// ============================================================================
// Computer Use Sample - Tool-backed screen actions
// ============================================================================
//
// Prerequisites:
// 1. Create a `.env` file in this directory with your Gemini API key:
//    GOOGLE_API_KEY=your_api_key_here
//    GOOGLE_GENAI_USE_VERTEXAI=False
//
// Running the sample:
// 
// 1. Navigate to the sample directory:
//    cd GoogleAdk/samples/GoogleAdk.Samples.ComputerUse
//
// 2. Variant 1: Console Driver (Simple HTTP scraping, no screenshot)
//    dotnet run
//
// 3. Variant 2: Playwright Driver (Real browser, real screenshots)
//    # First-time Playwright setup (installs browsers):
//    powershell bin/Debug/net10.0/playwright.ps1 install
//    
//    # Run with Playwright driver:
//    dotnet run -- use-playwright
// ============================================================================

using GoogleAdk.Core;
using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Abstractions.Sessions;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Runner;
using GoogleAdk.Core.Tools;
using GoogleAdk.Samples.ComputerUse.Drivers;

AdkEnv.Load();

Console.WriteLine("=== Computer Use Sample (LLM) ===\n");

bool usePlaywright = args.Contains("use-playwright");

IComputerDriver driver;
if (usePlaywright)
{
    Console.WriteLine("Using Playwright Driver (Real Browser)");
    var playwrightDriver = new PlaywrightComputerDriver();
    await playwrightDriver.InitializeAsync();
    driver = playwrightDriver;
}
else
{
    Console.WriteLine("Using Console Driver (HTTP Fetching Only)");
    var consoleDriver = new ConsoleComputerDriver();
    await consoleDriver.InitializeAsync();
    driver = consoleDriver;
}

var toolset = new ComputerUseToolset(driver);

var agent = new LlmAgent(new LlmAgentConfig
{
    Name = "computer_use",
    Model = "gemini-2.5-flash",
    Instruction = "Use the computer_use tool to navigate and interact with the screen."
        + " Always provide coordinates for click and drag actions.",
    Tools = [toolset]
});

var runner = new InMemoryRunner("computer-use-sample", agent);
var session = await runner.SessionService.CreateSessionAsync(new CreateSessionRequest
{
    AppName = "computer-use-sample",
    UserId = "user-1"
});

var userMessage = new Content
{
    Role = "user",
    Parts =
    [
        new Part
        {
            Text = "Open a browser, navigate to https://github.com/trending, wait a moment for it to load, then read the page content and tell me the names of the top 3 trending repositories right now."
        }
    ]
};

Console.WriteLine("User: Open a browser, navigate to https://github.com/trending, wait a moment for it to load, then read the page content and tell me the names of the top 3 trending repositories right now.\n");

await foreach (var evt in runner.RunAsync("user-1", session.Id, userMessage))
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

Console.WriteLine("\n=== Computer Use Sample Complete ===");
await driver.CloseAsync();