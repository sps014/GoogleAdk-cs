# Memory Management

Memory management in the ADK provides an automated and programmatic way to store, recall, and manage context across an agent's long-term operations. While `State` is used for more structured variables across sessions, `Memory` represents an unstructured collection of previous conversations or explicitly provided facts, allowing agents to search and summarize context for the user dynamically.

## Setting Up Memory Service

By default, the ADK `Runner` automatically provides an `InMemoryMemoryService`. This is suitable for prototyping and testing, allowing you to search through the current user's session history based on simple keyword matching. For production use cases, you can implement a persistent data store or a more robust retrieval engine using the `IBaseMemoryService` interface.

To inject a custom memory service, provide it through the `RunnerConfig`:

```csharp
var runnerConfig = new RunnerConfig
{
    AppName = "my-app",
    Agent = myRootAgent,
    SessionService = new InMemorySessionService(),
    MemoryService = new MyCustomMemoryService() // Use your custom implementation
};
```

## Using Memory in Tools and Callbacks

The memory capabilities are exposed in any tool or callback via the `AgentContext` object.

### Adding Sessions to Memory

You can automatically extract and store an entire session's events to the configured memory service. This is commonly done in `after_run` callbacks or at the completion of a scenario.

```csharp
// Example: Add an entire session's conversation history to memory.
await ctx.AddSessionToMemoryAsync();
```

### Adding Specific Events to Memory

If you only want to save a subset of events (e.g., the last few turns of an interaction), use `AddEventsToMemoryAsync`:

```csharp
var customMetadata = new Dictionary<string, object>
{
    { "category", "financial_advice" },
    { "importance", "high" }
};

await ctx.AddEventsToMemoryAsync(
    events: recentEventsList,
    customMetadata: customMetadata
);
```

### Writing Explicit Memories

You can explicitly inject new facts or entries into the memory using `AddMemoryAsync`. These memories are not tied directly to session turns.

```csharp
var newMemories = new List<MemoryEntry>
{
    new MemoryEntry 
    { 
        Content = new Content { Parts = new List<Part> { new Part { Text = "The user prefers dark mode." } } },
        Author = "system",
        Timestamp = DateTimeOffset.UtcNow.ToString("o")
    }
};

await ctx.AddMemoryAsync(newMemories);
```

### Searching Memory

Memory searching enables the agent to fetch relevant past knowledge. This is typically invoked dynamically by the agent through a built-in or custom tool, or proactively within a callback.

```csharp
var searchResponse = await ctx.SearchMemoryAsync("What is the user's preferred theme color?");

foreach (var memory in searchResponse.Memories)
{
    Console.WriteLine($"Found Memory: {memory.Content.Parts.First().Text}");
}
```

The underlying `IBaseMemoryService` implementation determines how the query is processed (e.g., lexical search, semantic embeddings, vector retrieval).
