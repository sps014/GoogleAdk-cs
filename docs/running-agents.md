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

## Persisting sessions with EF Core

For production, use `EfCoreSessionService` to persist sessions, events, and state across restarts. It stores session state, event history, and app/user-scoped state in an EF Core database.

```csharp
using GoogleAdk.Sessions.EfCore;
using Microsoft.EntityFrameworkCore;

var dbOptions = new DbContextOptionsBuilder<AdkSessionDbContext>()
    .UseSqlite("Data Source=adk_sessions.db")
    .Options;

var dbFactory = new PooledDbContextFactory<AdkSessionDbContext>(dbOptions);
var sessionService = new EfCoreSessionService(dbFactory);

var runner = new Runner(new RunnerConfig
{
    AppName = "production_app",
    Agent = myAgent,
    SessionService = sessionService,
    ArtifactService = new FileArtifactService("artifacts"),
    MemoryService = new InMemoryMemoryService()
});
```

## Using ADK Web (Visual Dashboard)

The ADK ships with a powerful visual dashboard, **ADK Web**, which automatically mounts an ASP.NET Core server and a Blazor front-end over your agent. It visualizes the execution graph, multi-agent orchestration, tool calls, and streaming responses in real-time.

It is highly recommended to use `AdkServer` when debugging complex workflows like `LoopAgent` or `ParallelAgent`.

### Launching ADK Web
    
    You can launch the dashboard using a single line of code via `AdkServer.RunAsync`.
    
    ```csharp
    using GoogleAdk.ApiServer;
    
    // Ensure your agent is fully configured
    var myComplexAgent = new SequentialAgent(new SequentialAgentConfig { /* ... */ });
    
    // Launch the interactive dashboard application with default options
    // The browser will automatically open to http://localhost:8080/dev-ui
    await AdkServer.RunAsync(myComplexAgent);
    ```
    
    ### Configuring ADK Server Options
    
    You can easily configure server options such as port, host, UI toggles, tracing, and A2A endpoints using the action overload:
    
    ```csharp
    using GoogleAdk.ApiServer;
    
    await AdkServer.RunAsync(myComplexAgent, options => 
    {
        options.Port = 9000;
        options.ShowSwaggerUI = false;
        options.EnableA2a = true;
        
        // Advanced: supply custom CORS policy
        options.ConfigureCors = policy => 
            policy.WithOrigins("http://my-frontend.com").AllowAnyMethod().AllowAnyHeader();
    });
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

## Advanced Run Configurations

The `RunConfig` provides fine-grained control over execution behavior, multimodality, live sessions, and context limits.

```csharp
var runnerConfig = new RunnerConfig
{
    AppName = "production_app",
    Agent = myAgent,
    RunConfig = new RunConfig
    {
        // Limits total LLM calls per run to prevent runaway recursion
        MaxLlmCalls = 10,
        
        // Halts execution on any tool call, allowing client-side execution
        PauseOnToolCalls = true,
        
        // Configures audio response and speech configurations
        ResponseModalities = ["AUDIO"],
        SpeechConfig = new SpeechConfig(),
        
        // Allows saving bidirectional audio/video payloads into session storage
        SaveLiveBlob = true,
        
        // Automatically fetch recent session events upon start
        GetSessionConfig = new GetSessionConfig { NumRecentEvents = 50 }
    }
};
```

## ADK API server and streaming

`GoogleAdk.ApiServer` exposes HTTP APIs for running agents and fetching artifacts. The key runtime endpoints include:

- `POST /run` for synchronous execution
- `POST /run_sse` for SSE streaming
- `POST /run_live` for bidirectional WebSocket streaming

When streaming via SSE, the server splits events that include both content and `ArtifactDelta` into two separate SSE events. This prevents the web UI from double-rendering artifacts and mirrors the Python ADK server behavior.

`RunConfig.SaveInputBlobsAsArtifacts` defaults to `true` in the API server, so inline file uploads are automatically saved and surfaced as artifacts when running via `/run` or `/run_sse`.