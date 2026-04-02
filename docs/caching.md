# Prompt Caching (Context Caching)

Large language models charge for the number of tokens processed. In use cases like code analysis or iterative questioning over huge documents, sending the identical, massive context payload repeatedly incurs significant latency and token costs.

The ADK leverages **Context Caching** (currently supported natively by Gemini models) to cache massive contexts securely server-side.

## Enabling Prompt Caching

To enable prompt caching for an agent, simply provide a `ContextCacheConfig` to your `LlmAgentConfig`. The `ContextCacheRequestProcessor` will dynamically evaluate the conversational history. If the context meets the caching criteria (min tokens), it builds a cache request; if the context was recently cached, the model leverages the stored payload without reprocessing the tokens.

The cache implementation relies on cryptographic SHA-256 fingerprinting. The ADK calculates a fingerprint of your agent's system instruction, available tools, and the message history. If a matching fingerprint is found in your session's event history, the ADK automatically intercepts the request and injects the `cache_name` into the LLM payload, skipping the redundant upload.

```csharp
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Context;
using GoogleAdk.Models.Gemini;

// Simulating a large document to pass as context
var largeDocument = "The history of the world is vast. " + new string('A', 50000);

var agent = new LlmAgent(new LlmAgentConfig
{
    Name = "cache_agent",
    ModelName = "gemini-1.5-pro", // Caching is most valuable on large reasoning models
    
    // Enables the ContextCacheRequestProcessor
    ContextCacheConfig = new ContextCacheConfig
    {
        CacheIntervals = 10, // Number of turns to reuse the cache before forcing a refresh
        TtlSeconds = 3600,   // Cache time-to-live (1 hour)
        MinTokens = 10       // Minimum threshold of tokens required to trigger cache creation. 
                             // Gemini API restricts this to >32k tokens in production.
    }
});

var runner = new InMemoryRunner("cache-app", agent);
var session = await runner.SessionService.CreateSessionAsync(new CreateSessionRequest { AppName = "cache-app", UserId = "user" });

// The first turn will take longer as it creates the Cache remotely
var turn1 = new Content { Role = "user", Parts = [new Part { Text = $"{largeDocument}\nWhat is this document about?" }] };
await foreach (var evt in runner.RunAsync("user", session.Id, turn1))
{
    if (evt.IsFinalResponse() && evt.CacheMetadata != null)
    {
        Console.WriteLine($"Cache created! Name: {evt.CacheMetadata.CacheName}");
    }
}

// The second turn calculates a matching fingerprint and uses the existing cache!
var turn2 = new Content { Role = "user", Parts = [new Part { Text = "Tell me more about the history section." }] };
await foreach (var evt in runner.RunAsync("user", session.Id, turn2))
{
    if (evt.IsFinalResponse() && evt.CacheMetadata != null)
    {
        Console.WriteLine($"Cache Reused! Invocations: {evt.CacheMetadata.InvocationsUsed}");
    }
}
```

**Benefits of Context Caching:**
- **Cost Reduction**: Reused context blocks are typically billed at a fraction of the cost.
- **Latency Optimization**: Substantially reduces time-to-first-byte (TTFB) since the model circumvents re-tokenizing massive prefix prompts or documents.
- **Improved Performance on Repetitive Queries**: Highly effective for applications building "Chat with PDF" workflows or complex data extraction pipelines.