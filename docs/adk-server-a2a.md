# ADK Server A2A

`GoogleAdk.ApiServer` exposes HTTP APIs for running agents, fetching artifacts, and enabling Agent-to-Agent (A2A) communication over HTTP.

## Enabling A2A

To enable the A2A endpoints and standard REST APIs, launch the ADK Server with the `EnableA2a` flag set to true:

```csharp
using GoogleAdk.ApiServer;

await AdkServer.RunAsync(myAgent, options =>
{
    options.EnableA2a = true;
    options.Port = 8080;
});
```

## Runtime Endpoints

The key runtime endpoints include:

- `POST /run` for synchronous execution
- `POST /run_sse` for Server-Sent Events (SSE) streaming
- `POST /run_live` for bidirectional WebSocket streaming

### Synchronous Execution

A simple request to `POST /run` with the user message payload will return the final sequence of events, including the model's text response and tool outputs.

### Streaming with SSE

When streaming via SSE using `POST /run_sse`, the server returns a stream of events as they happen. 

**Note on Artifacts**: To prevent the web UI from double-rendering artifacts, the server splits events that include both text content and `ArtifactDelta` into two separate SSE events. This mirrors the Python ADK server behavior.

### Live Mode

`POST /run_live` opens a WebSocket connection for bidirectional audio/video streaming, allowing live interaction with models like Gemini that support native audio modalities.

## Artifact Handling in the Server

`RunConfig.SaveInputBlobsAsArtifacts` defaults to `true` in the API server. This means that inline file uploads passed in the HTTP request payload are automatically saved to the configured `ArtifactService` and surfaced as artifacts when running via `/run` or `/run_sse`.

You can manage these artifacts using the standard artifact endpoints (e.g., `GET /artifacts/{id}`).

## Agent Card Endpoint

When A2A is enabled, the server automatically generates and serves an Agent Card for each registered agent. The Agent Card provides a machine-readable description of the agent, including its capabilities, tools, and endpoints, allowing other agents to dynamically discover and interact with it.

You can fetch the Agent Card using a `GET` request to the following relative URL:

- `GET /a2a/{agentName}/.well-known/agent-card.json`
