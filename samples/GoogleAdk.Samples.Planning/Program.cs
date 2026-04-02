// ============================================================================
// Planning Sample — PlanReActPlanner
// ============================================================================

using GoogleAdk.Core;
using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Planning;
using GoogleAdk.Core.Runner;
using GoogleAdk.Models.Gemini;

Console.WriteLine("=== Planning Sample ===\n");

AdkEnv.Load();

var agent = new LlmAgent(new LlmAgentConfig
{
    Name = "planner",
    Model = "gemini-2.5-flash",
    Planner = new PlanReActPlanner(),
    Instruction = "You are a helpful assistant."
});

var runner = new InMemoryRunner("planning-sample", agent);
var session = await runner.SessionService.CreateSessionAsync(new GoogleAdk.Core.Abstractions.Sessions.CreateSessionRequest
{
    AppName = "planning-sample",
    UserId = "user-1"
});

var userMessage = new Content
{
    Role = "user",
    Parts = [new Part { Text = "Plan a weekend in Paris." }]
};

await foreach (var evt in runner.RunAsync("user-1", session.Id, userMessage))
{
    var text = evt.Content?.Parts?.FirstOrDefault()?.Text;
    if (!string.IsNullOrWhiteSpace(text))
        Console.WriteLine(text);
}



Console.WriteLine("\n=== Planning Sample Complete ===");

