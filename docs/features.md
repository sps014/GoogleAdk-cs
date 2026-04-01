# Features

The ADK provides a rich set of features for building complex agentic workflows.

## Agents

The ADK includes several built-in agent types to handle different orchestration patterns:

- **`LlmAgent`**: The standard agent powered by an LLM. It handles tool calling, context management, and system instructions.
- **`SequentialAgent`**: Runs a list of sub-agents in order, passing the output of one as the input to the next.
- **`ParallelAgent`**: Runs multiple sub-agents concurrently and aggregates their results.
- **`LoopAgent`**: Runs a sub-agent repeatedly until a specific condition is met or a max iteration count is reached.

### LlmAgent API Essentials

When configuring an `LlmAgent`, you use `LlmAgentConfig`:

- `Model` or `ModelName`: The LLM to use (direct instance or resolved via registry).
- `Instruction` / `InstructionProvider`: The system prompt that guides the agent's behavior.
- `Tools`: A list of `IBaseTool` instances the agent can call.
- `CodeExecutor`: An optional engine (like `BuiltInCodeExecutor`) allowing the agent to write and run code.
- `RequestProcessors` / `ResponseProcessors`: Middleware pipeline for modifying requests before they hit the LLM, or responses after they return.

## Processors

Processors allow you to hook into the LLM request/response lifecycle. The ADK includes several built-in processors:

- **`InstructionsLlmRequestProcessor`**: Injects system instructions and few-shot examples.
- **`CodeExecutionRequestProcessor`**: Configures the LLM to use code execution capabilities.
- **`RequestConfirmationLlmRequestProcessor`**: Handles resuming tool execution after requiring user confirmation.

### Example: Customizing the Processor Pipeline

You can override the default processors if you need a specialized pipeline:

```csharp
GeminiModelFactory.RegisterDefaults();

var agent = new LlmAgent(new LlmAgentConfig
{
    Name = "custom_pipeline_agent",
    ModelName = "gemini-2.5-flash",
    RequestProcessors = new List<BaseLlmRequestProcessor>
    {
        BasicLlmRequestProcessor.Instance,
        InstructionsLlmRequestProcessor.Instance,
        ContentRequestProcessor.Instance
        // Add your own custom processors here
    }
});
```

## Context Compaction

Long-running conversations can exceed the LLM's context window. Context compactors automatically reduce the history size.

- **Token-based**: Removes older messages until the token count is under a threshold.
- **Truncation-based**: Keeps only the most recent N messages.
- **LLM summarizer**: Uses an LLM to summarize older messages into a single concise memory.

### Example: Adding a Compactor

```csharp
var agent = new LlmAgent(new LlmAgentConfig
{
    Name = "compact_agent",
    Model = GeminiModelFactory.Create("gemini-2.5-flash"),
    // Keep only the most recent 2000 characters/tokens of history
    ContextCompactors = [new TruncatingContextCompactor(2000)]
});
```

## Placeholders

- Live streaming agents: _coming soon_
