# ADK for .NET

Welcome to the Agent Development Kit (ADK) for .NET (C#). This site mirrors the
structure of the TypeScript docs and tracks feature parity with the JS SDK.

## Quick Start

- Install prerequisites: .NET SDK 10.0+
- Build the solution:

```bash
dotnet build "GoogleAdk/GoogleAdk.slnx"
```

### Minimal agent example

Here is a complete, runnable example of a basic agent that responds to a single user message.

```csharp
using System;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Runner;
using GoogleAdk.Models.Gemini;

// 1. Register model defaults
GeminiModelFactory.RegisterDefaults();

// 2. Configure the agent
var agent = new LlmAgent(new LlmAgentConfig
{
    Name = "hello_agent",
    ModelName = "gemini-2.5-flash",
    Instruction = "Answer briefly."
});

// 3. Set up the runner to execute the agent
var runner = new InMemoryRunner("hello-app", agent);

// 4. Create the user input
var content = new Content
{
    Role = "user",
    Parts = new List<Part> { new Part { Text = "Hello" } }
};

// 5. Run the agent and stream the response
await foreach (var evt in runner.RunAsync("user-1", "session-1", content))
{
    if (evt.Content?.Parts?.FirstOrDefault()?.Text is string text)
    {
        Console.WriteLine(text);
    }
}
```

## Core Concepts (API overview)

- Agents: `BaseAgent`, `LlmAgent`, `SequentialAgent`, `ParallelAgent`, `LoopAgent`
- Tools: `BaseTool`, `BaseToolset`, `AgentTool`, `AuthTool`
- Runner: `Runner`, `InMemoryRunner`
- Sessions/Events: `Session`, `Event`, `LlmRequest`, `LlmResponse`
- Artifacts: `InMemoryArtifactService`, `FileArtifactService`, `GcsArtifactService`
- Telemetry: `AdkTracing`, `TelemetrySetup`

## Feature Parity Status

This SDK aims to match the JS ADK feature set. See `plan.md` for the current
status. Any incomplete areas are noted in each section below.

## Sections

- [Getting Started](getting-started.md)
- [Features](features.md)
- [A2A](a2a.md)
- [Tools](tools.md)
- [Artifacts](artifacts.md)
- [Models](models.md)
- [MCP](mcp.md)
- [Testing](testing.md)
