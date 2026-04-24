# Models and the LLM Registry

The ADK heavily abstracts the underlying Large Language Model (LLM) implementation. This decoupled architecture allows you to dynamically switch between models, swap providers, or mock models entirely for testing, without modifying your agent logic.

## Supported Providers

- **Google Gemini**: First-class native support via the official `Microsoft.Extensions.AI` (MEAI) integration.
- **MEAI-compatible providers**: Use `GoogleAdk.Models.Meai` to wrap any `IChatClient` (OpenAI, Anthropic, Ollama, Azure OpenAI, etc.).

## The Model Registry

Instead of directly instantiating model clients, the ADK utilizes the `LlmRegistry`. You can simply reference a model by its string name (e.g., `"gemini-2.5-flash"`). The registry matches the string pattern and dynamically instantiates the correct client.

### Using the Model Registry (Recommended)

This is the standard approach for configuring an agent. By passing a string to `Model`, the ADK handles resolution automatically based on standard regex rules.

```csharp
using GoogleAdk.Core.Agents;

var agent = new LlmAgent(new LlmAgentConfig
{
    Name = "registry_agent",
    Model = "gemini-2.5-flash",
    Instruction = "Provide helpful guidance."
});
```

### Direct Model Instantiation

If you need to configure specific underlying MEAI settings, instantiate the model directly using the factory and pass it to the `Model` property.

```csharp
using GoogleAdk.Models.Gemini;

// Instantiate the Gemini model directly
var geminiModel = GeminiModelFactory.Create("gemini-2.5-flash");

var agent = new LlmAgent(new LlmAgentConfig
{
    Name = "direct_agent",
    Model = geminiModel,
    Instruction = "Provide helpful guidance."
});
```

### Using `MeaiLlm` (any MEAI `IChatClient`)

The true power of the ADK's model abstraction is its native support for Microsoft Extensions AI (MEAI). You can wrap **any** MEAI `IChatClient` with `MeaiLlm` and pass it as the model. This allows you to use models from OpenAI, Anthropic, Ollama, Azure, and more.

#### Ollama (Local Models)

To run local models like Llama 3 or Gemma using Ollama, use the `GoogleAdk.Models.Ollama` package, which provides a native `OllamaChatClient` that supports advanced features like model-native thinking.

```csharp
using GoogleAdk.Models.Meai;
using GoogleAdk.Models.Ollama;
using Microsoft.Extensions.AI;

// Connect to your local Ollama instance
// Pull the model first with: ollama pull gemma4:e4b
string modelName = "gemma4:e4b";
IChatClient ollamaClient = new OllamaChatClient(new Uri("http://localhost:11434"), modelName);

var agent = new LlmAgent(new LlmAgentConfig
{
    Name = "ollama_agent",
    Model = new MeaiLlm(modelName, ollamaClient),
    Instruction = "You are a local assistant running on Ollama."
});
```

#### OpenAI

To use OpenAI models like GPT-4o, use the `Microsoft.Extensions.AI.OpenAI` package.

```csharp
using GoogleAdk.Models.Meai;
using Microsoft.Extensions.AI;
using OpenAI;

// Initialize the OpenAI client with your API key
var openAiClient = new OpenAIClient("your-openai-api-key").AsIChatClient("gpt-4o");

var agent = new LlmAgent(new LlmAgentConfig
{
    Name = "openai_agent",
    Model = new MeaiLlm("gpt-4o", openAiClient),
    Instruction = "You are a helpful assistant powered by OpenAI."
});
```

#### Anthropic (Claude)

To use Anthropic models like Claude 3.5 Sonnet, use a compatible MEAI wrapper or the official SDK if it implements `IChatClient`.

```csharp
using GoogleAdk.Models.Meai;
using Microsoft.Extensions.AI;
using Anthropic;
using Anthropic.Core;

var anthropicClient = new AnthropicClient(new ClientOptions { ApiKey = "your-anthropic-api-key" });
var chatClient = anthropicClient.AsIChatClient("claude-3-5-sonnet-latest");

var agent = new LlmAgent(new LlmAgentConfig
{
    Name = "claude_agent",
    Model = new MeaiLlm("claude-3-5-sonnet-latest", chatClient),
    Instruction = "You are Claude, a helpful assistant."
});
```

## Fallbacks and Hierarchy

In multi-agent orchestration, you do not need to configure a model for every single sub-agent. If a child agent (like a `SequentialAgent`'s sub-agent) lacks a `ModelN`, it will automatically traverse up the execution tree to find the `CanonicalModel` defined by its parent or root agent.

```csharp
var childAgent = new LlmAgent(new LlmAgentConfig
{
    Name = "child",
    // Model is intentionally left blank. It will inherit from the parent.
    Instruction = "I am a child agent."
});

var rootAgent = new LlmAgent(new LlmAgentConfig
{
    Name = "root",
    Model = "gemini-2.5-flash", // Defined here at the root
    Tools = [new AgentTool(childAgent)]
});
```

## Gemini-Specific Limitations

While the ADK supports any model via MEAI, certain built-in tools and features are deeply integrated with Google Cloud and the Gemini API infrastructure. These features **will not work** if you use a non-Gemini provider (like OpenAI or Ollama).

### Features requiring Gemini:
- **Context Caching**: The `ContextCacheRequestProcessor` relies on the Gemini Cache API.
- **Code Execution**: The `CodeExecutionRequestProcessor` relies on Gemini's native code execution environment.
- **Live/Bidirectional Streaming**: `RunLiveAsync` requires the Gemini Bidi protocol.

### Tools requiring Gemini or Google Cloud Auth:
The following tools require Google Cloud authentication (ADC) or rely on Gemini-specific search grounding capabilities:
- `GoogleSearchTool`
- `VertexAiSearchTool`
- `VertexAiRagRetrievalTool`
- `DiscoveryEngineSearchTool`
- `GoogleApiTool`
- `ApiHubTool`
- `BigQueryQueryTool` & `BigQueryMetadataTool`
- `SpannerQueryTool` & `SpannerSearchTool`
- `BigtableQueryTool`
- `PubSubMessageTool`

If you are using Ollama, OpenAI, or Anthropic, you should use standard function tools (`[FunctionTool]`), MCP toolsets, or custom `BaseTool` implementations instead.