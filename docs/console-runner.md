# Console Runner

For rapid development and CLI applications, the ADK provides a robust `ConsoleRunner` class built on top of `Spectre.Console`. It offers a beautiful, interactive terminal experience with support for streaming, debug logging, and file attachments.

## Basic Usage

To run an agent in the console with default settings, simply pass your agent to `ConsoleRunner.RunAsync`:

```csharp
using GoogleAdk.Core.Runner;

var agent = new LlmAgent(new LlmAgentConfig { Name = "my-agent", Model = "gemini-2.5-flash" });

await ConsoleRunner.RunAsync(agent);
```

This will automatically:
1. Initialize an `InMemorySessionService`, `InMemoryArtifactService`, and `InMemoryMemoryService`.
2. Start a continuous chat loop.
3. Display model responses, tool calls, and subagent transfers in styled panels.

## Customizing the Console Runner

You can customize the `ConsoleRunner` behavior, such as toggling debug mode or enabling streaming, using the configuration delegate:

```csharp
await ConsoleRunner.RunAsync(agent, config =>
{
    config.DebugMode = true;       // Show tool calls and subagent transfers (default: true)
    config.EnableStreaming = true; // Stream text chunk-by-chunk (default: false)
    config.AppName = "my-cli-app"; // Sets the session AppName
    config.InitialMessage = "hi";  // Send an initial message automatically on startup
    config.CloseOnFinish = true;   // Close the runner after the first turn completes
    
    // You can override the default in-memory services here for persistence
    // config.SessionService = new EfCoreSessionService(...);
});
```

### Passing Initial State

If your agent requires initial context or state (like an injected OAuth credential or a predefined data payload), you can pass an `initialState` dictionary to `ConsoleRunner.RunAsync`. This state is applied to the very first execution turn.

```csharp
var initialState = new Dictionary<string, object?>
{
    ["temp:my-auth-key"] = myAuthCredential
};

await ConsoleRunner.RunAsync(agent, configure: null, initialState: initialState);
```

### Initial Message and Auto-Close

If you want the runner to execute a single task automatically without waiting for user input, you can use `InitialMessage` and `CloseOnFinish`. This is especially useful for scripts or CI/CD environments.

```csharp
await ConsoleRunner.RunAsync(agent, config =>
{
    config.InitialMessage = "Summarize the latest tech news";
    config.CloseOnFinish = true; // Exits the application after the agent finishes its response
});
```

## Features

### Debug Output
When `DebugMode` is enabled, the console runner will automatically intercept and display:
- **Tool Calls**: Shows the tool name and JSON-serialized arguments.
- **Tool Results**: Shows the execution outcome of the tool.
- **Subagent Transfers**: Clearly logs when control shifts from one agent to another (e.g., in a `SequentialAgent` or `LoopAgent`).

### Streaming
If `EnableStreaming` is true, the `ConsoleRunner` buffers incoming text chunks and dynamically updates the terminal with a `streaming...` indicator, providing a responsive experience for the user.

### File Attachments
The `ConsoleRunner` supports attaching local files to your prompts via a special CLI command.

To attach a file, type `/attach` followed by one or more file paths in the console prompt:

```bash
You: /attach my_document.pdf image.png
```

The runner will read the files, convert them to Base64 `InlineData`, and stage them. On your next chat message, these files will be sent to the model along with your text.

### Generated Artifacts
If the model generates artifacts (like code execution results or saved files), the `ConsoleRunner` intercepts these `FileData` and `CodeExecutionResult` parts and displays them cleanly in the console UI.

## Exiting
The runner maintains a continuous loop. You can exit by:
- Typing `/bye` or `quit`
- Pressing `Ctrl+C` (the runner handles `Console.CancelKeyPress` gracefully)
