# Features & Architecture

The ADK provides a robust and composable architecture designed for complex, agentic workflows. By standardizing the pipeline for interactions, developers can build logic from the ground up without rewriting boilerplate.

## Agent Types

The ADK supports various built-in agent orchestration patterns that determine how a sequence of actions executes.

### `LlmAgent`

The foundational agent type powered by a Large Language Model. It handles tool execution, context window management, code execution capabilities, and system instructions. 

- Automatically processes input context.
- Analyzes prompts to determine if a tool call is required.
- Manages the execution context.

### Structural Agents (Multi-Agent Patterns)

Instead of relying on a single monolith model, it is often more reliable to split complex problems across multiple specialized agents. 

- **`SequentialAgent`**: Executes a list of sub-agents sequentially. The output of Agent A becomes the context input for Agent B.
- **`ParallelAgent`**: Distributes identical context to a group of sub-agents and runs them concurrently. Once all resolve, their responses are aggregated and returned.
- **`LoopAgent`**: Runs a sub-agent recursively in a loop until a specific condition is met, max iterations are reached, or an "escalate" tool is called.

> See [Orchestration (Multi-Agent)](orchestration.md) for detailed examples.

## Processors Pipeline

`LlmAgent` utilizes a customizable processor pipeline. This pipeline gives developers fine-grained control over how an LLM request is mutated before being dispatched to the model and how the response is handled upon return.

### Built-in Processors

| Processor | Description |
| :--- | :--- |
| **`InstructionsLlmRequestProcessor`** | Injects system instructions, few-shot examples, and metadata into the system prompt. |
| **`CodeExecutionRequestProcessor`** | Triggers the capability for the agent to write and execute code. |
| **`RequestConfirmationLlmRequestProcessor`** | Pauses execution and surfaces a confirmation prompt when an agent attempts to execute a sensitive tool. |
| **`ContextCacheRequestProcessor`** | Automatically implements prompt caching to lower costs on repeated context blocks. |
| **`OutputSchemaRequestProcessor`** | Configures the LLM output to conform strictly to a predefined JSON schema. |

### Customizing the Pipeline

By default, the `LlmAgent` configures these processors in an optimal order. However, if your use-case requires a custom middleware sequence (e.g., injecting proprietary headers or logging specific steps), you can supply your own array of `BaseLlmRequestProcessor`.

```csharp
var customAgent = new LlmAgent(new LlmAgentConfig
{
    Name = "custom_pipeline_agent",
    Model = "gemini-2.5-flash",
    // Overriding the default pipeline with a custom sequence
    RequestProcessors = new List<BaseLlmRequestProcessor>
    {
        BasicLlmRequestProcessor.Instance,
        InstructionsLlmRequestProcessor.Instance,
        // Your custom processor
        new MyAuthenticationRequestProcessor(),
        ContentRequestProcessor.Instance
    }
});
```

## Context Compaction

When engaging in long-running conversational flows, the context length (token count) will inevitably exceed the LLM's context window. To prevent failures and manage costs, the ADK supports **Context Compactors**, which automatically truncate or summarize conversation history.

| Compactor | Strategy | Description |
| :--- | :--- | :--- |
| **Truncation-based** | FIFO | Removes the oldest messages in the history to keep only the `N` most recent items. |
| **Token-based** | Limit | Evaluates token limits dynamically and removes history until the payload fits. |
| **Summarization-based** | LLM | Utilizes the LLM itself to read the history, summarize it, and replace the long dialogue block with the concise summary. |

```csharp
var agent = new LlmAgent(new LlmAgentConfig
{
    Name = "compact_agent",
    Model = "gemini-2.5-flash",
    // Keep only the most recent 2000 characters/tokens of conversational history
    ContextCompactors = [new TruncatingContextCompactor(2000)]
});
```