# Agent-to-Agent (A2A) Protocol

The .NET ADK includes a complete implementation of the A2A protocol, allowing agents to communicate with each other across network boundaries, mirroring the JS SDK.

## Core Components

- **`A2aClient`**: Sends events to a remote agent over REST or JSON-RPC.
- **`RemoteA2aAgent`**: A local proxy that represents a remote agent. You can use this inside your `SequentialAgent` or as a tool, and it will forward calls to the remote server.
- **ASP.NET Core Server**: The ADK provides extension methods to easily host your agents as A2A endpoints.

## Server Wiring (ASP.NET Core)

To expose your local agents over the A2A protocol, use the `MapA2aApi` extension method in your ASP.NET Core application.

```csharp
var builder = WebApplication.CreateBuilder(args);

// Register your agents with the AgentLoader
var loader = new AgentLoader(Path.GetTempPath());
loader.Register("my_agent", myAgentInstance);
builder.Services.AddSingleton(loader);

var app = builder.Build();

// Expose the A2A endpoints (e.g., /a2a/my_agent/rest)
app.MapA2aApi();

await app.RunAsync();
```

## Client Example (REST)

To talk to a remote agent, create an `A2aClient` and wrap it in a `RemoteA2aAgent`.

```csharp
using GoogleAdk.Core.A2a;

// 1. Create the client pointing to the remote A2A endpoint
var client = new A2aClient("http://localhost:8080/a2a/my_agent/rest", "HTTP+JSON");

// 2. Define the remote agent's capabilities
var agentCard = new AgentCard 
{ 
    Name = "my_agent", 
    Url = "http://localhost:8080/a2a/my_agent/rest",
    PreferredTransport = "HTTP+JSON"
};

// 3. Create the remote agent proxy
var remoteAgent = new RemoteA2aAgent(new RemoteA2aAgentConfig 
{ 
    Name = "remote_proxy", 
    Client = client, 
    AgentCard = agentCard 
});

// 4. Run it just like a local agent
var invocationContext = new InvocationContext { Session = mySession };
await foreach (var evt in remoteAgent.RunAsync(invocationContext)) 
{
    Console.WriteLine(evt.Content?.Parts?.FirstOrDefault()?.Text);
}
```

## Placeholders

- Task lifecycle details: _coming soon_
