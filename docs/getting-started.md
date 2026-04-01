# Getting Started

## Prerequisites

- .NET SDK 10.0+

## Build from Source

To build the ADK from source, restore and build the solution:

```bash
dotnet restore "GoogleAdk/GoogleAdk.slnx"
dotnet build "GoogleAdk/GoogleAdk.slnx"
```

## First Agent in 30 seconds

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
    Name = "quickstart_agent",
    ModelName = "gemini-2.5-flash",
    Instruction = "You are a helpful assistant. Be concise."
});

// 3. Set up the runner to execute the agent
var runner = new InMemoryRunner("quickstart_app", agent);

// 4. Create the user input
var input = new Content 
{ 
    Role = "user", 
    Parts = [new Part { Text = "Hello, ADK!" }] 
};

// 5. Run the agent and stream the response
await foreach (var evt in runner.RunAsync("user-1", "session-1", input))
{
    if (evt.Content?.Parts?.FirstOrDefault()?.Text is string text)
    {
        Console.WriteLine(text);
    }
}
```

## Run Tests

To run the unit and end-to-end tests:

```bash
dotnet test
```

## Placeholders

- Packaging and distribution: _coming soon_
- Deployment guide: _coming soon_
