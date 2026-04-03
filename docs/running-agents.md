# Running Agents

Once an agent is configured, it must be executed within a runtime environment that manages session state, artifacts, memory, and telemetry. The ADK uses the `Runner` concept for execution.

## The `Runner` class

The core `Runner` requires fully configured implementations for session storage, artifact storage, and memory services. This is designed for production applications (e.g., ASP.NET Core APIs or microservices) where persistent databases (like Entity Framework) or Google Cloud Storage are required.

```csharp
using GoogleAdk.Core.Runner;
using GoogleAdk.Core.Sessions;
using GoogleAdk.Core.Artifacts;

// 1. Initialize services (replace InMemory with persistent versions for Prod)
var runnerConfig = new RunnerConfig
{
    AppName = "production_app",
    Agent = myAgent,
    SessionService = new InMemorySessionService(), // e.g. EfCoreSessionService
    ArtifactService = new FileArtifactService("artifacts"), 
    MemoryService = new InMemoryMemoryService()
};

var runner = new Runner(runnerConfig);

// 2. Explicitly create or fetch a Session
var session = await runner.SessionService.CreateSessionAsync(new CreateSessionRequest
{
    AppName = "production_app",
    UserId = "user-123"
});

// 3. Run the invocation
await foreach (var evt in runner.RunAsync("user-123", session.Id, userMessage))
{
    // handle event output
}
```

## The `InMemoryRunner`

For rapid development, CLI tools, and testing, the ADK provides `InMemoryRunner`. It automatically bootstraps ephemeral memory, artifact, and session services, allowing you to bypass tedious configuration boilerplate.

```csharp
// Bootstraps a runner with all required InMemory services
var runner = new InMemoryRunner("cli-app", myAgent);

// Session history persists ONLY for the lifecycle of this application process
var session = await runner.SessionService.CreateSessionAsync(new CreateSessionRequest
{
    AppName = "cli-app",
    UserId = "dev-user"
});
```

## Using ADK Web (Visual Dashboard)

The ADK ships with a powerful visual dashboard, **ADK Web**, which automatically mounts an ASP.NET Core server and a Blazor front-end over your agent. It visualizes the execution graph, multi-agent orchestration, tool calls, and streaming responses in real-time.

It is highly recommended to use `AdkServer` when debugging complex workflows like `LoopAgent` or `ParallelAgent`.

### Launching ADK Web

You can launch the dashboard using a single line of code via `AdkServer.RunAsync`.

```csharp
using GoogleAdk.DevServer;

// Ensure your agent is fully configured
var myComplexAgent = new SequentialAgent(new BaseAgentConfig { /* ... */ });

// Launch the interactive dashboard application
// The browser will automatically open to http://localhost:<port>
await AdkServer.RunAsync(myComplexAgent);
```

### Running with Console fallback

A common pattern for samples is to allow the application to run via the console, but switch to the visual dashboard if the `--web` flag is passed.

```csharp
if (args.Contains("--web"))
{
    Console.WriteLine("Starting ADK Web Dashboard...");
    await AdkServer.RunAsync(agent);
    return;
}

// Fallback to standard Console execution
await RunConsoleAppAsync(agent);
```