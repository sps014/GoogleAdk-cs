using System.Text.Json;
using GoogleAdk.Core;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Abstractions.Sessions;
using GoogleAdk.Core.Abstractions.Tools;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Runner;
using GoogleAdk.ApiServer;
using GoogleAdk.Models.Gemini;
using GoogleAdk.Samples.RequireConfirmation;

// Load environment variables (e.g. GOOGLE_CLOUD_PROJECT, GOOGLE_CLOUD_LOCATION)
AdkEnv.Load();

// =========================================================================================
// 1. Configure the Model + Agent
// =========================================================================================
var model = GeminiModelFactory.Create("gemini-2.5-flash");

var agent = new LlmAgent(new LlmAgentConfig
{
    Name = "time_off_agent",
    Description = "Handles reimbursements and time off requests with confirmation.",
    Model = model,
    Instruction = """
        You are a helpful assistant for employee requests.
        - Use `reimburse` for reimbursements.
        - Use `request_time_off` for time off requests.
        - Always use tools to respond.
        """,
    Tools =
    [
        RequireConfirmationTools.ReimburseTool,
        RequireConfirmationTools.RequestTimeOffTool
    ]
});

// =========================================================================================
// 2. Run the Application (Web or Console)
// =========================================================================================
if (args.Contains("--web"))
{
    Console.WriteLine("Starting ADK Web Dashboard...");
    await AdkServer.RunAsync(agent);
    return;
}

await RunConsoleAppAsync(agent);

// =========================================================================================
// Console Helper Methods
// =========================================================================================
static async Task RunConsoleAppAsync(LlmAgent agent)
{
    var runner = new InMemoryRunner("require-confirmation-sample", agent);
    var session = await runner.SessionService.CreateSessionAsync(new CreateSessionRequest
    {
        AppName = "require-confirmation-sample",
        UserId = "user-1",
    });

    Console.Clear();
    Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║  ADK C# — RequireConfirmation Sample                         ║");
    Console.WriteLine("║  Try: \"request 5 days off\" or \"reimburse 120\"               ║");
    Console.WriteLine("║  The console will ask for approval before executing tools.   ║");
    Console.WriteLine("║  Type 'quit' to exit.                                        ║");
    Console.WriteLine("╚══════════════════════════════════════════════════════════════╝\n");

    while (true)
    {
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write("\nEnter a request (or 'quit'): ");
        var input = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(input) || input.Equals("quit", StringComparison.OrdinalIgnoreCase))
            break;

        var userMessage = new Content
        {
            Role = "user",
            Parts = [new Part { Text = input }]
        };

        var confirmation = await RunOnceAsync(runner, session.Id, userMessage);
        if (confirmation == null)
            continue;

        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.Write("\nDo you approve this tool call? (yes/no): ");
        Console.ResetColor();
        var approval = Console.ReadLine();
        var accepted = !string.IsNullOrWhiteSpace(approval) &&
                       approval.Trim().StartsWith("y", StringComparison.OrdinalIgnoreCase);

        Console.ForegroundColor = accepted ? ConsoleColor.Green : ConsoleColor.Red;
        Console.WriteLine(accepted ? "\n[Approved]\n" : "\n[Rejected]\n");

        var confirmationResponse = BuildConfirmationResponse(confirmation, accepted);
        await RunOnceAsync(runner, session.Id, confirmationResponse);
    }
}

static async Task<ConfirmationRequest?> RunOnceAsync(
    Runner runner,
    string sessionId,
    Content userMessage)
{
    ConfirmationRequest? confirmation = null;

    await foreach (var evt in runner.RunAsync("user-1", sessionId, userMessage))
    {
        if (evt.Content?.Parts == null) continue;

        foreach (var part in evt.Content.Parts)
        {
            if (part.FunctionCall?.Name == FunctionCallHandler.RequestConfirmationFunctionCallName)
            {
                var originalId = GetOriginalFunctionCallId(part.FunctionCall);
                if (originalId != null && part.FunctionCall.Id != null)
                {
                    confirmation = new ConfirmationRequest(part.FunctionCall.Id, originalId);
                }
            }

            if (!string.IsNullOrWhiteSpace(part.Text) && evt.IsFinalResponse())
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"Agent: {part.Text}");
            }
        }
    }

    return confirmation;
}

static Content BuildConfirmationResponse(ConfirmationRequest request, bool accepted)
{
    return new Content
    {
        Role = "user",
        Parts =
        [
            new Part
            {
                FunctionResponse = new FunctionResponse
                {
                    Name = FunctionCallHandler.RequestConfirmationFunctionCallName,
                    Id = request.ConfirmationCallId,
                    Response = new Dictionary<string, object?>
                    {
                        ["toolConfirmation"] = new ToolConfirmation
                        {
                            FunctionCallId = request.OriginalFunctionCallId,
                            Accepted = accepted
                        }
                    }
                }
            }
        ]
    };
}

static string? GetOriginalFunctionCallId(FunctionCall call)
{
    if (call.Args == null) return null;
    if (!call.Args.TryGetValue("originalFunctionCall", out var raw)) return null;

    var original = JsonSerializer.Deserialize<FunctionCall>(JsonSerializer.Serialize(raw));
    return original?.Id;
}

file sealed record ConfirmationRequest(string ConfirmationCallId, string OriginalFunctionCallId);
