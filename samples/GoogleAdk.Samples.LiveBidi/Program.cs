// ============================================================================
// Live/Bidi Sample — Streaming responses with RunLiveAsync
// ============================================================================

using GoogleAdk.Core;
using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Abstractions.Sessions;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Runner;

Console.WriteLine("=== Live/Bidi Sample ===\n");

AdkEnv.Load();

var model = "gemini-2.5-flash";
var agent = new LlmAgent(new LlmAgentConfig
{
    Name = "live",
    Model = model,
    Instruction = "Keep responses short and stream partial output."
});
var runner = new InMemoryRunner("live-sample", agent);
var session = await runner.SessionService.CreateSessionAsync(new CreateSessionRequest
{
    AppName = "live-sample",
    UserId = "user-1"
});

var queue = new LiveRequestQueue();
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

var runTask = Task.Run(async () =>
{
    try
    {
        await foreach (var evt in runner.RunLiveAsync("user-1", session.Id, queue, cancellationToken: cts.Token))
        {
            var text = evt.Content?.Parts?.FirstOrDefault()?.Text ?? string.Empty;
            if (evt.Partial == true && !string.IsNullOrWhiteSpace(text))
            {
                Console.Write(text);
            }
            else if (evt.IsFinalResponse() && !string.IsNullOrWhiteSpace(text))
            {
                Console.WriteLine();
            }
        }
    }
    catch (OperationCanceledException)
    {
        // Timeout or user cancellation; treat as normal shutdown.
    }
});

await queue.SendContentAsync(new Content
{
    Role = "user",
    Parts = [new Part { Text = "Give me a 3-bullet plan for learning ADK." }]
});

await queue.SendContentAsync(new Content
{
    Role = "user",
    Parts = [new Part { Text = "Now rewrite as a single sentence." }]
});

queue.Close();

await runTask;

Console.WriteLine("\n=== Live/Bidi Sample Complete ===");

