// ============================================================================
// Output Schema Sample — SetModelResponseTool
// ============================================================================

using GoogleAdk.Core;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Abstractions.Sessions;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Runner;

Console.WriteLine("=== Output Schema Sample ===\n");

AdkEnv.Load();

var agent = new LlmAgent(new LlmAgentConfig
{
    Name = "schema",
    Model = "gemini-2.5-flash",
    Instruction = "Return a JSON response that matches the schema using set_model_response.",
    OutputSchema = typeof(SchemaOutput)
});

var runner = new InMemoryRunner("output-schema-sample", agent);
var session = await runner.SessionService.CreateSessionAsync(new CreateSessionRequest
{
    AppName = "output-schema-sample",
    UserId = "user-1",
});

var userMessage = new Content
{
    Role = "user",
    Parts = [new Part { Text = "Give me a sample response" }]
};

Console.WriteLine("User: Give me a sample response\n");

await foreach (var evt in runner.RunAsync("user-1", session.Id, userMessage))
{
    if (evt.IsFinalResponse() && evt.Content?.Parts != null)
    {
        foreach (var part in evt.Content.Parts)
        {
            if (part.Text != null)
                Console.WriteLine($"Agent: {part.Text}");
        }
    }
}

Console.WriteLine("\nDone!");

public class SchemaOutput
{
    public string? Foo { get; set; }
}