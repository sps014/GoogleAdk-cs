# Agent Lifecycle & Callbacks

The Agent Development Kit (ADK) provides a rich set of lifecycle callbacks that allow you to execute custom logic at various stages of an agent's execution. These callbacks are essential for tasks like state management, auditing, dynamic system instructions, and emitting custom events (like A2UI components).

Unlike older architectures that required complex lists of delegates, the C# ADK uses a simple **single-cast delegate** model for configuration, ensuring predictability and consistency—especially in asynchronous workflows.

## The Execution Pipeline

When a `Runner` invokes an agent, the execution follows a well-defined pipeline. If the agent is an `LlmAgent` (which represents the vast majority of agents), the pipeline extends to cover interactions with the underlying language model and any tools.

1. **Agent Started**: The agent is initialized and the session context is loaded.
2. **`BeforeAgentCallback`**: Executed before the agent begins its core logic.
3. **Model Loop** (for `LlmAgent`):
   - **`BeforeModelCallback`**: Executed just before the LLM is called. You can inspect or modify the `LlmRequest` here.
   - **LLM Execution**: The LLM processes the request.
     - *If the LLM fails:* **`OnModelErrorCallback`** is triggered.
   - **`AfterModelCallback`**: Executed immediately after the LLM responds.
   - **Tool Loop** (if the LLM requests tool execution):
     - **`BeforeToolCallback`**: Executed before a specific tool is run.
     - **Tool Execution**: The tool's `RunAsync` method is called.
       - *If the tool fails:* **`OnToolErrorCallback`** is triggered.
     - **`AfterToolCallback`**: Executed after the tool successfully completes.
4. **Agent Completed**: The agent finishes processing.
5. **`AfterAgentCallback`**: Executed before control is returned to the runner.

## Configuring Callbacks

Callbacks are configured directly on the agent's configuration object (e.g., `BaseAgentConfig` or `LlmAgentConfig`). Each property accepts a single asynchronous delegate.

### Agent-Level Callbacks

Available on all agents deriving from `BaseAgent`:

*   **`BeforeAgentCallback`**: `Func<AgentContext, Task<Content?>>`
    *   Fires before the agent starts processing.
    *   If you return a `Content` object, the agent will *short-circuit* and immediately return that content to the user, skipping the rest of its execution. Return `null` to continue normally.
*   **`AfterAgentCallback`**: `Func<AgentContext, Task<Content?>>`
    *   Fires after the agent has completed its processing.
    *   You can use this to emit a final event (e.g., rendering an A2UI component or a summary). Return `null` if you don't want to emit an additional event.

### Model & Tool Callbacks

Available exclusively on `LlmAgent`:

*   **`BeforeModelCallback`**: `Func<AgentContext, LlmRequest, Task<LlmResponse?>>`
    *   Allows you to modify the `LlmRequest` (e.g., adding global system instructions, modifying tools).
    *   If you return an `LlmResponse`, the LLM call is bypassed and your response is used instead.
*   **`AfterModelCallback`**: `Func<AgentContext, LlmResponse, Task<LlmResponse?>>`
    *   Allows you to inspect or modify the `LlmResponse` before the agent processes it (e.g., logging token usage or sanitizing output).
*   **`OnModelErrorCallback`**: `Func<AgentContext, LlmRequest, Exception, Task<LlmResponse?>>`
    *   Fires if the LLM provider throws an exception. You can return a fallback `LlmResponse` to recover gracefully.
*   **`BeforeToolCallback`**: `Func<IBaseTool, Dictionary<string, object?>, AgentContext, Task<Dictionary<string, object?>?>>`
    *   Fires before a tool executes.
    *   If you return a dictionary, the actual tool execution is bypassed, and your dictionary is treated as the tool's result.
*   **`AfterToolCallback`**: `Func<IBaseTool, Dictionary<string, object?>, AgentContext, Dictionary<string, object?>, Task<Dictionary<string, object?>?>>`
    *   Fires after a tool executes. The original result is passed in. You can modify and return a new dictionary to change the tool's output.
*   **`OnToolErrorCallback`**: `Func<IBaseTool, Dictionary<string, object?>, AgentContext, Exception, Task<Dictionary<string, object?>?>>`
    *   Fires if a tool throws an exception. You can return a valid result dictionary to suppress the error and allow the agent to continue.

## Example: Using Callbacks

Here is an example demonstrating several lifecycle callbacks:

```csharp
var agent = new LlmAgent(new LlmAgentConfig
{
    Name = "callback_demo_agent",
    Model = "gemini-2.5-flash",
    Instruction = "You are a helpful assistant.",
    
    // Inject a dynamic instruction before the model runs
    BeforeModelCallback = (ctx, request) => 
    {
        request.Config ??= new GenerateContentConfig();
        request.Config.SystemInstruction += "\nAlways be extremely polite.";
        return Task.FromResult<LlmResponse?>(null);
    },

    // Catch and recover from tool errors
    OnToolErrorCallback = (tool, args, ctx, ex) =>
    {
        Console.WriteLine($"Tool {tool.Name} failed: {ex.Message}");
        // Return a mock result to prevent the agent from crashing
        var recoveryResult = new Dictionary<string, object?> { ["error"] = "Service temporarily unavailable" };
        return Task.FromResult<Dictionary<string, object?>?>(recoveryResult);
    },

    // Emit a custom UI component when the agent finishes
    AfterAgentCallback = async (ctx) =>
    {
        var rootNode = new A2uiText("This conversation has ended.");
        var part = A2uiBuilder.BeginRendering(rootNode);
        
        return new Content 
        { 
            Role = "model", 
            Parts = new List<Part> { part } 
        };
    }
});
```

## Callbacks vs. Plugins

Callbacks are excellent for configuring specific behavior on a **single agent instance**. However, if you need logic that applies to *all* agents in your application (like global telemetry, auditing, or security constraints), you should use the **[Plugin System](plugins.md)** instead. Plugins tap into these exact same lifecycle events but operate globally at the `Runner` level.
