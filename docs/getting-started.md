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
using GoogleAdk.Models.Gemini;
using GoogleAdk.Core.Runner;

// 1. Load environment configurations
AdkEnv.Load();

// 2. Configure the agent
var agent = new LlmAgent(new LlmAgentConfig
{
    Name = "quickstart_agent",
    Model = "gemini-2.5-flash",
    Instruction = "You are a helpful assistant. Keep answers concise."
});


await ConsoleRunner.RunAsync(agent); //run in console

//there are other ways to run it, like web UI or you can have a custom runner to have full control.
//await AdkServer.RunAsync(agent); 


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