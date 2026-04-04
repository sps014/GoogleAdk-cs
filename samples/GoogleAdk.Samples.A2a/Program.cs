using GoogleAdk.Core;
using GoogleAdk.Core.A2a;
using GoogleAdk.Core.Agents;
using GoogleAdk.ApiServer;
using GoogleAdk.Models.Gemini;

// Load environment variables (e.g. GOOGLE_CLOUD_PROJECT, GOOGLE_CLOUD_LOCATION)
AdkEnv.Load();

var mode = args.FirstOrDefault(a => a.StartsWith("--mode="))?.Split('=', 2).Last() ?? "server";
var transport = args.FirstOrDefault(a => a.StartsWith("--transport="))?.Split('=', 2).Last() ?? "jsonrpc";
var baseUrl = args.FirstOrDefault(a => a.StartsWith("--url="))?.Split('=', 2).Last();

if (mode.Equals("server", StringComparison.OrdinalIgnoreCase))
{
    await RunServerAsync();
    return;
}

if (mode.Equals("client", StringComparison.OrdinalIgnoreCase))
{
    await RunClientAsync(transport, baseUrl);
    return;
}

Console.WriteLine("Usage:");
Console.WriteLine("  --mode=server");
Console.WriteLine("  --mode=client [--transport=jsonrpc|rest] [--url=http://localhost:8080/a2a/a2a-sample/jsonrpc]");

static async Task RunServerAsync()
{
    var model = GeminiModelFactory.Create("gemini-2.5-flash");
    var agent = new LlmAgent(new LlmAgentConfig
    {
        Name = "a2a-sample",
        Model = model,
        Instruction = "Answer clearly and concisely."
    });

    Console.WriteLine("Starting ADK Web with A2A endpoints...");
    await AdkServer.RunAsync(agent, enableA2a: true);
}

static async Task RunClientAsync(string transport, string? baseUrl)
{
    var normalizedTransport = transport.Equals("rest", StringComparison.OrdinalIgnoreCase)
        ? "HTTP+JSON"
        : "JSONRPC";

    baseUrl ??= normalizedTransport == "JSONRPC"
        ? "http://localhost:8080/a2a/a2a-sample/jsonrpc"
        : "http://localhost:8080/a2a/a2a-sample/rest";

    var client = new A2aClient(baseUrl, normalizedTransport);

    var parameters = new MessageSendParams
    {
        Message = new Message
        {
            MessageId = Guid.NewGuid().ToString(),
            Role = MessageRole.User,
            Parts = [new A2aPart { Text = "Summarize ADK in one sentence." }]
        },
        Configuration = new MessageSendConfiguration
        {
            Blocking = true,
            AcceptedOutputModes = ["text/plain"]
        }
    };

    Console.WriteLine($"A2A client ({normalizedTransport}) → {baseUrl}");
    Console.WriteLine("\n--- message/send ---");
    var result = await client.SendMessageAsync(parameters);
    PrintEvent(result);

    Console.WriteLine("\n--- message/stream ---");
    await foreach (var evt in client.SendMessageStreamAsync(parameters))
        PrintEvent(evt);
}

static void PrintEvent(IA2aEvent evt)
{
    switch (evt)
    {
        case Message msg:
            var text = msg.Parts.FirstOrDefault()?.Text ?? "";
            Console.WriteLine($"Message[{msg.Role}]: {text}");
            break;
        case A2aTask task:
            Console.WriteLine($"Task: {task.Status.State}");
            break;
        case TaskStatusUpdateEvent status:
            Console.WriteLine($"Status: {status.Status.State} (final={status.Final})");
            if (status.Status.Message?.Parts?.FirstOrDefault()?.Text is { } sText)
                Console.WriteLine($"  → {sText}");
            break;
        case TaskArtifactUpdateEvent artifact:
            Console.WriteLine($"Artifact: {artifact.Artifact.Name ?? artifact.Artifact.ArtifactId}");
            break;
        default:
            Console.WriteLine($"Event: {evt.Kind}");
            break;
    }
}
