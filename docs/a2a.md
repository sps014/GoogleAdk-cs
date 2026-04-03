# Agent-to-Agent (A2A) Protocol

The ADK includes a complete implementation of the Agent-to-Agent (A2A) protocol. This enables decentralized, interoperable workflows where agents hosted in different processes, containers, or even languages (e.g., Python, TypeScript) can discover, invoke, and seamlessly stream data to one another.

## Core Concepts

- **`A2aClient`**: The client used to invoke a remote A2A endpoint.
- **`RemoteA2aAgent`**: A local proxy class representing the remote agent. It implements `BaseAgent`, meaning you can place it inside pipelines (`SequentialAgent`), attach it as an `AgentTool`, or run it directly via a `Runner`.
- **A2A Server**: Exposing your local agents as REST endpoints via ASP.NET Core extension methods.

---

## 1. Creating an A2A Server (ASP.NET Core)

You can expose any locally registered agent over HTTP so that external systems can talk to it.

### Setup and Wiring

In your `Program.cs`, use the `AgentLoader` to register instances of your agents, then expose the endpoints using `MapA2aApi`.

```csharp
using GoogleAdk.Core.Agents;
using GoogleAdk.ApiServer.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// 1. Create and configure your local agent
var myAgent = new LlmAgent(new LlmAgentConfig
{
    Name = "support_agent",
    Model = "gemini-2.5-flash",
    Instruction = "You are a highly capable support agent."
});

// 2. Initialize the AgentLoader and register the agent
// The TempPath is used for temporary workspace generation during execution
var loader = new AgentLoader(Path.GetTempPath());
loader.Register("support_agent", myAgent);

// 3. Inject the loader into the DI container
builder.Services.AddSingleton(loader);

var app = builder.Build();

// 4. Map the standard A2A endpoints
// This mounts the necessary routes (e.g., /a2a/support_agent/rest)
app.MapA2aApi();

await app.RunAsync();
```

Your `support_agent` is now accessible over HTTP JSON-RPC at `http://localhost:<port>/a2a/support_agent/rest`.

---

## 2. Consuming an A2A Endpoint (A2A Client)

To connect to a remote agent and utilize it within your application, you map its endpoint to a `RemoteA2aAgent`.

### Client Example

```csharp
using GoogleAdk.Core.A2a;
using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Runner;
using System;

// 1. Configure the remote agent's identity and connection details
var remoteAgentUrl = "http://localhost:5000/a2a/support_agent/rest";
var preferredTransport = "HTTP+JSON";

var agentCard = new AgentCard 
{ 
    Name = "support_agent", 
    Url = remoteAgentUrl,
    PreferredTransport = preferredTransport
};

// 2. Instantiate the A2A Client
var a2aClient = new A2aClient(remoteAgentUrl, preferredTransport);

// 3. Create the proxy agent
var remoteProxyAgent = new RemoteA2aAgent(new RemoteA2aAgentConfig 
{ 
    Name = "remote_support_proxy", 
    Client = a2aClient, 
    AgentCard = agentCard 
});

// 4. Execute the remote agent exactly as you would a local agent
var runner = new InMemoryRunner("a2a-client-app", remoteProxyAgent);
var session = await runner.SessionService.CreateSessionAsync(new GoogleAdk.Core.Abstractions.Sessions.CreateSessionRequest
{
    AppName = "a2a-client-app",
    UserId = "user-1"
});

var userInput = new Content 
{ 
    Role = "user", 
    Parts = [new Part { Text = "Can you help me reset my password?" }] 
};

// The runner seamlessly invokes the remote REST endpoint and streams the response back
await foreach (var evt in runner.RunAsync("user-1", session.Id, userInput)) 
{
    if (evt.Content?.Parts?.FirstOrDefault()?.Text is string text)
    {
        Console.WriteLine($"[Remote Agent]: {text}");
    }
}
```

## Integrating Remote Agents into Orchestration

Because `RemoteA2aAgent` implements `BaseAgent`, you can integrate it into complex workflows alongside local agents.

```csharp
// Use a remote agent as a tool inside a local LLM agent
var localCoordinator = new LlmAgent(new LlmAgentConfig
{
    Name = "coordinator",
    Model = "gemini-2.5-flash",
    Instruction = "You manage support workflows.",
    Tools = [new AgentTool(remoteProxyAgent)] // Mounts the remote agent as a tool
});
```