# Models

The ADK abstracts the underlying Large Language Model (LLM) implementation, allowing you to easily switch between different models and providers.

## Supported Providers

- **Gemini**: Native support via the official MEAI (Microsoft.Extensions.AI) client integration.

## Model Registry

Instead of hardcoding model instances, you can use the `LlmRegistry`. This allows you to request a model by name (e.g., `"gemini-2.5-flash"`) and the registry will automatically instantiate the correct client using registered regex patterns.

### Example: Direct Model Instantiation

If you want to create a model directly, use the factory:

```csharp
var agent = new LlmAgent(new LlmAgentConfig
{
    Name = "direct_agent",
    // Creates the model directly
    ModelName = "gemini-2.5-flash"
});
```

### Example: Using the Model Registry

For more flexible configurations, register the defaults and use `ModelName`:

```csharp
// 1. Register default Gemini patterns (e.g., "gemini-.*")
GeminiModelFactory.RegisterDefaults();

var agent = new LlmAgent(new LlmAgentConfig
{
    Name = "registry_agent",
    // The ADK will automatically resolve this string to a Gemini model instance
    ModelName = "gemini-2.5-flash"
});
```

## Placeholders

- ApigeeLlm: _coming soon_
- BaseLlmConnection live streaming usage: _coming soon_
