# Getting Started

The Agent Development Kit (ADK) for .NET gives you the foundational components to build, test, and deploy intelligent agents.

## Prerequisites

- **.NET SDK 10.0+**
- **An API Key or Google Cloud Project credentials** (e.g., Gemini API Key, Vertex AI configuration)

### Environment Configuration

The ADK uses `AdkEnv.Load()` to read environment variables from a `.env` file or the system environment. To run the examples, ensure you have the following set:

**For Google AI Studio:**
```env
GOOGLE_API_KEY=your_api_key_here
```

**For Vertex AI:**
```env
GOOGLE_GENAI_USE_VERTEXAI=True
GOOGLE_CLOUD_PROJECT=your_project_id
GOOGLE_CLOUD_LOCATION=us-central1
```

## First Agent in 30 Seconds

Here is a complete, runnable console application that creates a basic conversational agent.

1. Create a new C# Console application:
   ```bash
   dotnet new console -n QuickstartAgent
   cd QuickstartAgent
   ```
2. Add the ADK package:
   ```bash
   dotnet add package GoogleAdk --prerelease
   ```
3. Paste the following into `Program.cs`:

```csharp
using System;
using GoogleAdk.Core;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Runner;
using GoogleAdk.Models.Gemini;

// 1. Load environment configurations
AdkEnv.Load();

// 2. Configure the agent
var agent = new LlmAgent(new LlmAgentConfig
{
    Name = "quickstart_agent",
    Model = "gemini-2.5-flash",
    Instruction = "You are a helpful assistant. Keep answers concise."
});

// 3. Set up the runner to execute the agent.
// InMemoryRunner handles session state and context temporarily for this process.
var runner = new InMemoryRunner("quickstart_app", agent);

// 4. Formulate the user's input request
var input = new Content 
{ 
    Role = "user", 
    Parts = [new Part { Text = "Explain what an LLM agent is in one sentence." }] 
};

Console.WriteLine("User: Explain what an LLM agent is in one sentence.\n");

// 5. Run the agent and stream the event responses back
await foreach (var evt in runner.RunAsync("user-1", "session-1", input))
{
    // The ADK emits events continuously. We listen for textual output parts.
    if (evt.Content?.Parts?.FirstOrDefault()?.Text is string text)
    {
        Console.WriteLine($"Agent: {text}");
    }
}
```

## Building from Source

To compile the ADK locally and explore the robust set of samples:

```bash
# Clone the repository
git clone <repository_url>
cd Adk

# Restore and build the entire solution
dotnet restore "GoogleAdk/GoogleAdk.slnx"
dotnet build "GoogleAdk/GoogleAdk.slnx"
```

You can then run the specific samples via `dotnet run`. For example:

```bash
dotnet run --project GoogleAdk/samples/GoogleAdk.Samples.Orchestration
```