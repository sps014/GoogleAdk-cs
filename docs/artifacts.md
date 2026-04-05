# Artifacts

Artifacts are explicit outputs produced by agents during a session. While conversation text is transient, artifacts are tangible assets like generated CSV files, compiled source code, data reports, or created images.

This page consolidates artifact guidance: lifecycle APIs, storage backends, runner setup, agent helpers, tool usage, and the HTTP API surface.

## Lifecycle and core APIs

Artifacts are scoped to `app + user + session` and support versioning. The core service interface includes:

- Save (`SaveArtifactAsync`)
- Load (`LoadArtifactAsync`, latest or specific version)
- List keys (`ListArtifactKeysAsync`)
- List versions (`ListVersionsAsync`, `ListArtifactVersionsAsync`)
- Delete (`DeleteArtifactAsync`)

These APIs are implemented by `InMemoryArtifactService`, `FileArtifactService`, and `GcsArtifactService`.

## Artifact services

The ADK includes several built-in services to handle artifact storage depending on your environment:

- **`InMemoryArtifactService`**: Stores artifacts in memory. Excellent for unit testing or ephemeral CLI applications.
- **`FileArtifactService`**: Stores artifacts on the local filesystem. Suitable for desktop applications or local development.
- **`GcsArtifactService`**: Stores artifacts remotely in Google Cloud Storage. Supports versioning and is recommended for production cloud environments.

## Configuring artifacts in the runner

Agents rely on the `Runner` to provide access to the Artifact Service. Configure it when bootstrapping your application:

```csharp
using GoogleAdk.Core.Runner;
using GoogleAdk.Core.Artifacts;

// Configure the runner to save artifacts to the local "output_files" folder
var runner = new Runner(new RunnerConfig
{
    AppName = "my_app",
    Agent = myAgent,
    ArtifactService = new FileArtifactService("output_files")
});
```

## Agent helpers (`AgentContext`)

Inside tools or agent callbacks, use `AgentContext` helpers to persist artifacts and automatically emit artifact deltas:

```csharp
// Saves the artifact and records the new version in EventActions.ArtifactDelta
await context.SaveArtifactAsync("summary.txt", new Part { Text = "Final report." });

// Loads an artifact (latest by default)
var part = await context.LoadArtifactAsync("summary.txt");
```

`SaveArtifactAsync` updates the `ArtifactDelta` collection so streaming clients can render artifacts as they appear.

## User uploads and `SaveFilesAsArtifactsPlugin`

When using the web UI or a client that sends `InlineData` as part of a **user message**, the `SaveFilesAsArtifactsPlugin` intercepts those parts, stores them as artifacts, and replaces the inline data with a `FileData` reference (`artifact://...`). This is primarily for **user uploads**, not for model output.

If you want model-produced files to become artifacts, use `AgentContext.SaveArtifactAsync` from a tool or agent callback.

## Saving and loading artifacts directly

If a tool or external process needs to read or write artifacts without `AgentContext`, use the service APIs directly:

```csharp
using GoogleAdk.Core.Artifacts;
using GoogleAdk.Core.Abstractions.Models;

var service = new FileArtifactService("my_artifacts_folder");

await service.SaveArtifactAsync(new SaveArtifactRequest
{
    AppName = "my_app",
    UserId = "user_123",
    SessionId = "session_456",
    Filename = "summary_report.txt",
    Artifact = new Part { Text = "This is the final summary output." }
});

var loadedPart = await service.LoadArtifactAsync(new LoadArtifactRequest
{
    AppName = "my_app",
    UserId = "user_123",
    SessionId = "session_456",
    Filename = "summary_report.txt"
});

Console.WriteLine(loadedPart?.Text);
```

## Loading artifacts from tools

The built-in `load_artifacts` tool can be exposed to models when you want the model to read artifacts (for example, a user upload saved by the UI):

```csharp
var agent = new LlmAgent(new LlmAgentConfig
{
    Name = "artifact_reader",
    Model = "gemini-2.5-flash",
    Tools = [new LoadArtifactsTool()]
});
```

The tool returns text directly when available and provides a short descriptor for binary content.

## HTTP API: artifact routes

The API server exposes REST endpoints for artifact retrieval and listing. The key routes are:

- `GET /apps/{appName}/users/{userId}/sessions/{sessionId}/artifacts`
- `GET /apps/{appName}/users/{userId}/sessions/{sessionId}/artifacts/{artifactName}/versions`
- `GET /apps/{appName}/users/{userId}/sessions/{sessionId}/artifacts/{artifactName}/versions/{versionId}`
- `GET /apps/{appName}/users/{userId}/sessions/{sessionId}/artifacts/{artifactName}/versions/{versionId}/metadata`
- `GET /apps/{appName}/users/{userId}/sessions/{sessionId}/artifacts/{artifactName}/versions/metadata`
- `DELETE /apps/{appName}/users/{userId}/sessions/{sessionId}/artifacts/{artifactName}`

If a stored artifact only has text, the API wraps it into `InlineData` so the web UI can treat it like a file download.

## Streaming semantics and `ArtifactDelta`

When running via SSE (`/run_sse`), events that contain both content and `ArtifactDelta` are split into two SSE events to prevent double-rendering in the web UI. Clients should handle a pure artifact event (no content) and render newly available artifacts based on the `ArtifactDelta` map.

## Sample: `ArtifactsWeb`

For a complete end-to-end example (uploads, storage, and display), see `GoogleAdk.Samples.ArtifactsWeb`.