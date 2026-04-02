// ============================================================================
// Context Caching Sample
// ============================================================================
//
// Demonstrates:
//   1. Initializing an LlmAgent with ContextCacheConfig
//   2. Passing a large document (simulated) as context
//   3. Observing the cache creation and subsequent reuse
//
// Environment variables:
//   GOOGLE_API_KEY=<your-api-key>
//   (Or GOOGLE_GENAI_USE_VERTEXAI=True with related Vertex env vars)
// ============================================================================

using GoogleAdk.Core;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Abstractions.Sessions;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Context;
using GoogleAdk.Core.Runner;

AdkEnv.Load();

Console.WriteLine("=== Context Caching Sample ===\n");

// Simulate a large document that justifies caching
var largeDocument = "The history of the world is vast. " + string.Join(" ", Enumerable.Repeat("The quick brown fox jumps over the lazy dog.", 5000));

var agent = new LlmAgent(new LlmAgentConfig
{
    Name = "cache_agent",
    Model = "gemini-2.5-pro", // Caching is typically more impactful on pro models
    Instruction = "You are a helpful assistant. Use the provided document to answer questions.",
    ContextCacheConfig = new ContextCacheConfig
    {
        CacheIntervals = 10,
        TtlSeconds = 3600, // 1 hour TTL
        MinTokens = 10     // Set low for the sake of this sample. In reality, caching only kicks in for >32k tokens on Gemini.
    }
});

var runner = new InMemoryRunner("cache-sample", agent);
var session = await runner.SessionService.CreateSessionAsync(new CreateSessionRequest
{
    AppName = "cache-sample",
    UserId = "user-1"
});

// Turn 1: Pass the large document and ask the first question.
// This should trigger the creation of a new cache.
var turn1 = new Content
{
    Role = "user",
    Parts =
    [
        new Part { Text = $"Document: {largeDocument}\n\nQuestion: What does the fox jump over?" }
    ]
};

Console.WriteLine("Turn 1: Initial query (Expect cache creation)");
await RunAndPrintAsync(runner, session.Id, turn1);

// Turn 2: Ask a follow-up question.
// This should reuse the cached document from Turn 1.
var turn2 = new Content
{
    Role = "user",
    Parts =
    [
        new Part { Text = "Question: Is the document about the history of the world?" }
    ]
};

Console.WriteLine("\nTurn 2: Follow-up query (Expect cache hit)");
await RunAndPrintAsync(runner, session.Id, turn2);

Console.WriteLine("\n=== Context Caching Sample Complete ===");


static async Task RunAndPrintAsync(Runner runner, string sessionId, Content userMessage)
{
    await foreach (var evt in runner.RunAsync("user-1", sessionId, userMessage))
    {
        if (evt.Content?.Parts != null)
        {
            foreach (var part in evt.Content.Parts)
            {
                if (!string.IsNullOrWhiteSpace(part.Text) && evt.Partial != true)
                {
                    Console.WriteLine($"Agent: {part.Text}");
                }
            }
        }
        
        if (evt.IsFinalResponse() && evt.CacheMetadata != null)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[Cache Metadata] Fingerprint: {evt.CacheMetadata.Fingerprint}, Used: {evt.CacheMetadata.InvocationsUsed}, CacheName: {evt.CacheMetadata.CacheName ?? "N/A"}");
            Console.ResetColor();
        }
    }
}
