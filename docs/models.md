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

When you already have a `Microsoft.Extensions.AI` client, wrap it with `MeaiLlm` and pass it as the model:

```csharp
using GoogleAdk.Models.Meai;
using Microsoft.Extensions.AI;

IChatClient chatClient = /* build your MEAI client */;
var meaiModel = new MeaiLlm("gpt-4o", chatClient);

var agent = new LlmAgent(new LlmAgentConfig
{
    Name = "meai_agent",
    Model = meaiModel,
    Instruction = "Respond using the MEAI client."
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