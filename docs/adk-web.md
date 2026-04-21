# ADK Web

The ADK ships with a powerful visual dashboard, **ADK Web**, which automatically mounts an ASP.NET Core server and a Blazor front-end over your agent. It visualizes the execution graph, multi-agent orchestration, tool calls, and streaming responses in real-time.

It is highly recommended to use `AdkServer` when debugging complex workflows like `LoopAgent` or `ParallelAgent`.

## Launching ADK Web

You can launch the dashboard using a single line of code via `AdkServer.RunAsync`.

```csharp
using GoogleAdk.ApiServer;
using GoogleAdk.Core.Agents;

// Ensure your agent is fully configured
var myComplexAgent = new SequentialAgent(new SequentialAgentConfig { /* ... */ });

// Launch the interactive dashboard application with default options
// The browser will automatically open to http://localhost:8080/dev-ui
await AdkServer.RunAsync(myComplexAgent);
```

## Configuring ADK Server Options

You can easily configure server options such as port, host, UI toggles, tracing, and A2A endpoints using the action overload:

```csharp
using GoogleAdk.ApiServer;

await AdkServer.RunAsync(myComplexAgent, options => 
{
    options.Port = 9000;
    options.ShowSwaggerUI = true;
    options.EnableA2a = true;
    
    // Advanced: supply custom CORS policy
    options.ConfigureCors = policy => 
        policy.WithOrigins("http://my-frontend.com").AllowAnyMethod().AllowAnyHeader();
});
```

