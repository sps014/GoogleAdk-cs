using GoogleAdk.Core;
using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Runner;
using GoogleAdk.ApiServer;
using GoogleAdk.Models.Gemini;
using GoogleAdk.Core.Abstractions.Tools;

// Load environment variables (e.g. GOOGLE_CLOUD_PROJECT, GOOGLE_CLOUD_LOCATION)
AdkEnv.Load();

// =========================================================================================
// 1. Configure the Model
// =========================================================================================
// We use gemini-2.5-flash for fast, iterative refinement.
var model = GeminiModelFactory.Create("gemini-2.5-flash");

// =========================================================================================
// 2. Define the Agents
// =========================================================================================

// Agent 1: The Drafter
// Responsible for writing the initial content and revising it based on feedback.
var drafter = new LlmAgent(new LlmAgentConfig
{
    Name = "drafter",
    Description = "Writes and iteratively improves content based on feedback.",
    Model = model,
    Instruction = """
        You are a skilled writer. 
        On the first turn, write a short piece (3-4 sentences) based on the user's topic. 
        On subsequent turns, the critic will have provided feedback — use it to improve your draft. 
        
        Show only your latest improved version, do not repeat the history.
        """,
});

// Agent 2: The Critic
// Responsible for scoring the draft and providing feedback, or ending the loop.
var critic = new LlmAgent(new LlmAgentConfig
{
    Name = "critic",
    Description = "Reviews writing quality and provides improvement feedback.",
    Model = model,
    Instruction = """
        You are a strict writing critic. Review the drafter's latest piece and:
        1. Score it 1-10 on clarity, engagement, and conciseness.
        2. If the average score is >= 8, respond ONLY with: "APPROVED — this is excellent."
        3. If the average score is < 8, provide 2-3 specific, actionable improvement suggestions.

        IMPORTANT: When you approve (score >= 8), you MUST call the `escalate` tool to 
        end the review loop.
        """,
    Tools = [EscalateTool],
});

// =========================================================================================
// 3. Define the Loop Agent
// =========================================================================================
// The LoopAgent orchestrates the back-and-forth between the drafter and the critic.
// It will run a maximum of 5 iterations unless the critic calls the escalate tool.
var refinementLoop = new LoopAgent(new LoopAgentConfig
{
    Name = "refinement_loop",
    Description = "Iteratively refines content through drafting and critique.",
    MaxIterations = 5,
    SubAgents = [drafter, critic],
});

// =========================================================================================
// 4. Run the Application (Web or Console)
// =========================================================================================

// If started with --web, launch the ADK Web Dashboard to visualize the agent interactions
if (args.Contains("--web"))
{
    Console.WriteLine("Starting ADK Web Dashboard...");
    await AdkServer.RunAsync(refinementLoop);
    return;
}

// Otherwise, run a Console application loop
await RunConsoleAppAsync(refinementLoop);


// =========================================================================================
// Console Helper Methods
// =========================================================================================
static async Task RunConsoleAppAsync(LoopAgent refinementLoop)
{
    var runner = new InMemoryRunner("loop-agent-sample", refinementLoop);

    // Create a persistent session so conversation history is preserved across turns
    var session = await runner.SessionService.CreateSessionAsync(
        new GoogleAdk.Core.Abstractions.Sessions.CreateSessionRequest
        {
            AppName = "loop-agent-sample",
            UserId = "user-1",
        });

    Console.Clear();
    Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║  ADK C# — Loop Agent Sample (Iterative Refinement)           ║");
    Console.WriteLine("║  Provide a topic. The 'drafter' and 'critic' will iterate    ║");
    Console.WriteLine("║  to produce a polished piece of writing.                     ║");
    Console.WriteLine("║  Type 'quit' to exit.                                        ║");
    Console.WriteLine("╚══════════════════════════════════════════════════════════════╝\n");

    while (true)
    {
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write("\nEnter a topic (or 'quit'): ");
        var topic = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(topic) || topic.Equals("quit", StringComparison.OrdinalIgnoreCase))
            break;

        var userMessage = new Content
        {
            Role = "user",
            Parts = [new() { Text = topic }]
        };

        Console.WriteLine("\n[Starting Iterative Loop...]\n");

        int currentIteration = 0;
        string? lastAuthor = null;

        await foreach (var evt in runner.RunAsync("user-1", session.Id, userMessage))
        {
            // We only care about complete messages with text
            var text = evt.Content?.Parts?.FirstOrDefault()?.Text;
            if (string.IsNullOrEmpty(text) || evt.Partial == true)
                continue;

            // Track iteration changes whenever the drafter starts a new turn
            if (evt.Author == "drafter" && lastAuthor != "drafter")
            {
                currentIteration++;
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"── Iteration {currentIteration} ──────────────────────────────────────────────────");
            }

            // Pick a color and emoji depending on who is talking
            var isDrafter = evt.Author == "drafter";
            Console.ForegroundColor = isDrafter ? ConsoleColor.Cyan : ConsoleColor.Magenta;
            var emoji = isDrafter ? "✏️" : "🔍";

            Console.WriteLine($"  {emoji} [{evt.Author!.ToUpper()}]:");
            Console.WriteLine($"  {text}\n");

            lastAuthor = evt.Author;

            // Check if the loop has been successfully completed
            if (evt.Actions?.Escalate == true)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("  ✅ Critic approved — loop complete!");
            }
        }

        Console.ResetColor();
        Console.WriteLine(new string('─', 64));
    }
}


/// <summary>
/// A tool that sets the escalate flag when the critic is satisfied,
/// causing the LoopAgent to break its iteration cycle.
/// </summary>
/// <param name="context">Agent context for setting escalate.</param>
[FunctionTool(Name = "escalate")]
static object? Escalate(AgentContext context)
{
    context.EventActions.Escalate = true;
    return new { status = "escalated", message = "Review loop completed." };
}