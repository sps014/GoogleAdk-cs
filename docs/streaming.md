# Streaming & Live/Bidirectional Protocols

The ADK is built around continuous streaming. Instead of waiting for a complete LLM response to finish generating, the ADK immediately emits partial text chunks as they arrive. This provides a highly responsive and fluid user experience.

There are two primary modes of streaming within the ADK.

| Feature | `RunAsync` (Standard) | `RunLiveAsync` (Bidirectional) |
| :--- | :--- | :--- |
| **Connection Lifecycle** | Opens and closes per request. | Persistent, long-lived connection. |
| **Input Method** | Single `Content` payload. | Continuous stream via `LiveRequestQueue`. |
| **Best For** | Chatbots, text generation, standard API endpoints. | Voice assistants, real-time multimodal apps, low-latency loops. |

## 1. Standard Event Streaming (`RunAsync`)

The most common way to consume ADK outputs is via the standard asynchronous event stream.

`runner.RunAsync` returns an `IAsyncEnumerable<Event>`. You iterate over these events continuously.

```csharp
using GoogleAdk.Core.Runner;

var runner = new InMemoryRunner("streaming-app", myAgent);

// Initiate the run
await foreach (var evt in runner.RunAsync("user-1", session.Id, userInputContent))
{
    // ADK events contain a 'Partial' flag. When true, this is a streaming chunk of a larger message.
    // When false, the event represents a completed action or final response block.
    
    var text = evt.Content?.Parts?.FirstOrDefault()?.Text ?? string.Empty;
    
    if (evt.Partial == true && !string.IsNullOrWhiteSpace(text))
    {
        // Write chunks directly to the console or UI without appending a newline
        Console.Write(text);
    }
    else if (evt.IsFinalResponse() && !string.IsNullOrWhiteSpace(text))
    {
        // The generation has finished
        Console.WriteLine();
    }
}
```

## 2. Live/Bidirectional Streaming (`RunLiveAsync`)

For highly interactive, real-time applications (such as voice-driven assistants or live collaborative coding), the ADK supports Live Bidirectional Streaming via `RunLiveAsync`.

Unlike `RunAsync` which resolves a single user request, `RunLiveAsync` establishes a persistent connection to the model. You can continuously stream partial requests (e.g., audio chunks) to the LLM while the LLM simultaneously streams text or audio back to you.

### The Live Request Queue

> **Note:** To push data into the live connection asynchronously, you utilize a `LiveRequestQueue`. This queue acts as a buffer, allowing you to send multiple user inputs (like audio chunks or text messages) over the same open connection without waiting for the model to finish its previous response.

```csharp
using GoogleAdk.Core.Runner;
using System.Threading;
using System.Threading.Tasks;

var queue = new LiveRequestQueue();
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)); // Safety timeout

// 1. Start the Live processing loop in a background task
var runTask = Task.Run(async () =>
{
    try
    {
        // The loop remains open, continuously yielding events
        await foreach (var evt in runner.RunLiveAsync("user-1", session.Id, queue, cancellationToken: cts.Token))
        {
            var text = evt.Content?.Parts?.FirstOrDefault()?.Text ?? string.Empty;
            if (evt.Partial == true && !string.IsNullOrWhiteSpace(text))
            {
                Console.Write(text);
            }
            else if (evt.IsFinalResponse() && !string.IsNullOrWhiteSpace(text))
            {
                Console.WriteLine();
            }
        }
    }
    catch (OperationCanceledException)
    {
        // Expected timeout or shutdown
    }
});

// 2. Stream user inputs dynamically while the loop is running
await queue.SendContentAsync(new Content
{
    Role = "user",
    Parts = [new Part { Text = "Provide a 3-bullet plan for learning the ADK." }]
});

// A second input sent over the exact same live connection
await queue.SendContentAsync(new Content
{
    Role = "user",
    Parts = [new Part { Text = "Now summarize it into a single sentence." }]
});

// 3. Close the queue, which signals the live loop to exit cleanly
queue.Close();
await runTask;
```

This pattern drastically reduces latency since the underlying connection remains open and contextual overhead is avoided.