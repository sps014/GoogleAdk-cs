// ============================================================================
// Code Executors Sample — LLM + Built-in Code Execution
// ============================================================================
//
// Demonstrates:
//   1. LlmAgent configured with BuiltInCodeExecutor
//   2. Executable code + execution results returned in events
// ============================================================================

using GoogleAdk.Core;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Abstractions.Sessions;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.CodeExecutors;
using GoogleAdk.Core.Runner;

AdkEnv.Load();

Console.WriteLine("=== Code Executors Sample (LLM) ===\n");

var agent = new LlmAgent(new LlmAgentConfig
{
    Name = "code",
    Model = "gemini-2.5-flash",
    Instruction = "Use Python code execution for calculations.",
    CodeExecutor = new BuiltInCodeExecutor()
});

var runner = new InMemoryRunner("code-exec-sample", agent);
var session = await runner.SessionService.CreateSessionAsync(new CreateSessionRequest
{
    AppName = "code-exec-sample",
    UserId = "user-1"
});

var userMessage = new Content
{
    Role = "user",
    Parts =
    [
        new Part { Text = "Calculate the mean and standard deviation of [3, 5, 8, 10, 12]. Show the result." }
    ]
};

Console.WriteLine("User: Calculate the mean and standard deviation of [3, 5, 8, 10, 12].\n");

await foreach (var evt in runner.RunAsync("user-1", session.Id, userMessage))
{
    if (evt.Content?.Parts == null) continue;

    foreach (var part in evt.Content.Parts)
    {
        if (part.ExecutableCode?.Code != null)
        {
            Console.WriteLine($"Executable code ({part.ExecutableCode.Language}):");
            Console.WriteLine(part.ExecutableCode.Code);
            Console.WriteLine();
        }

        if (part.CodeExecutionResult != null)
        {
            Console.WriteLine("Code execution result:");
            Console.WriteLine(part.CodeExecutionResult.Output);
        }

        if (!string.IsNullOrWhiteSpace(part.Text))
        {
            Console.WriteLine($"Agent: {part.Text}");
        }
    }
}

Console.WriteLine("\n=== Code Executors Sample Complete ===");
