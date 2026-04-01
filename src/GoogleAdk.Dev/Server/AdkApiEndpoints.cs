using System.Text.Json;
using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Sessions;
using GoogleAdk.Core.Agents;
using GoogleAdk.Dev.Server;
using Microsoft.AspNetCore.Mvc;

namespace GoogleAdk.Dev.Server;

/// <summary>
/// Registers all ADK API endpoints on the WebApplication, matching the JS ADK API surface.
/// </summary>
public static class AdkApiEndpoints
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public static WebApplication MapAdkApi(this WebApplication app)
    {
        // ── List Apps ──────────────────────────────────────────────────────
        app.MapGet("/list-apps", (AgentLoader loader) =>
        {
            return Results.Json(loader.ListAgents(), s_jsonOptions);
        });

        // ── Session CRUD ───────────────────────────────────────────────────
        app.MapGet("/apps/{appName}/users/{userId}/sessions",
            async (string appName, string userId, RunnerManager mgr) =>
            {
                var result = await mgr.SessionService.ListSessionsAsync(new ListSessionsRequest
                {
                    AppName = appName,
                    UserId = userId,
                });
                return Results.Json(result.Sessions, s_jsonOptions);
            });

        app.MapGet("/apps/{appName}/users/{userId}/sessions/{sessionId}",
            async (string appName, string userId, string sessionId, RunnerManager mgr) =>
            {
                var session = await mgr.SessionService.GetSessionAsync(new GetSessionRequest
                {
                    AppName = appName,
                    UserId = userId,
                    SessionId = sessionId,
                });
                return session is null ? Results.NotFound() : Results.Json(session, s_jsonOptions);
            });

        app.MapPost("/apps/{appName}/users/{userId}/sessions",
            async (string appName, string userId, [FromBody] CreateSessionBody? body, RunnerManager mgr) =>
            {
                var session = await mgr.SessionService.CreateSessionAsync(new CreateSessionRequest
                {
                    AppName = appName,
                    UserId = userId,
                    State = body?.State,
                });
                return Results.Json(session, s_jsonOptions, statusCode: 201);
            });

        app.MapPost("/apps/{appName}/users/{userId}/sessions/{sessionId}",
            async (string appName, string userId, string sessionId,
                   [FromBody] CreateSessionBody? body, RunnerManager mgr) =>
            {
                var session = await mgr.SessionService.CreateSessionAsync(new CreateSessionRequest
                {
                    AppName = appName,
                    UserId = userId,
                    SessionId = sessionId,
                    State = body?.State,
                });
                return Results.Json(session, s_jsonOptions, statusCode: 201);
            });

        app.MapDelete("/apps/{appName}/users/{userId}/sessions/{sessionId}",
            async (string appName, string userId, string sessionId, RunnerManager mgr) =>
            {
                await mgr.SessionService.DeleteSessionAsync(new DeleteSessionRequest
                {
                    AppName = appName,
                    UserId = userId,
                    SessionId = sessionId,
                });
                return Results.Ok();
            });

        // ── Run (synchronous) ──────────────────────────────────────────────
        app.MapPost("/run", async (HttpContext http, [FromBody] RunAgentRequest req, RunnerManager mgr) =>
        {
            try
            {
                var runner = mgr.GetOrCreate(req.AppName);
                var message = req.ResolveMessage();
                var events = new List<Event>();

                var runConfig = req.RunConfig ?? new GoogleAdk.Core.Agents.RunConfig();
                runConfig.SaveInputBlobsAsArtifacts = req.RunConfig?.SaveInputBlobsAsArtifacts ?? true;

                await foreach (var evt in runner.RunAsync(
                    req.UserId, req.SessionId, message,
                    stateDelta: req.StateDelta,
                    runConfig: runConfig,
                    cancellationToken: http.RequestAborted))
                {
                    events.Add(evt);
                }

                return Results.Json(events, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Json(new { error = $"Failed to run agent: {ex.Message}" },
                    statusCode: 500, options: s_jsonOptions);
            }
        });

        // ── Run SSE (streaming) ────────────────────────────────────────────
        app.MapPost("/run_sse", async (HttpContext http, [FromBody] RunAgentRequest req, RunnerManager mgr) =>
        {
            bool headersSent = false;
            try
            {
                var runner = mgr.GetOrCreate(req.AppName);
                var message = req.ResolveMessage();

                http.Response.ContentType = "text/event-stream";
                http.Response.Headers.CacheControl = "no-cache";
                http.Response.Headers.Connection = "keep-alive";
                http.Response.Headers["X-Accel-Buffering"] = "no";
                await http.Response.Body.FlushAsync(http.RequestAborted);
                headersSent = true;

                var runConfig = req.RunConfig ?? new GoogleAdk.Core.Agents.RunConfig();
                // Dev Server UI relies on parsing inline data attachments into artifacts.
                runConfig.SaveInputBlobsAsArtifacts = req.RunConfig?.SaveInputBlobsAsArtifacts ?? true;
                runConfig.StreamingMode = req.Streaming
                        ? GoogleAdk.Core.Agents.StreamingMode.Sse
                        : GoogleAdk.Core.Agents.StreamingMode.None;

                await foreach (var evt in runner.RunAsync(
                    req.UserId, req.SessionId, message,
                    stateDelta: req.StateDelta,
                    runConfig: runConfig,
                    cancellationToken: http.RequestAborted))
                {
                    // Send events as-is without splitting. The ADK Web UI processes
                    // artifactDelta only on events that have content (via storeMessage),
                    // so content-less artifact-only events would be silently ignored.
                    var json = JsonSerializer.Serialize(evt, s_jsonOptions);
                    await http.Response.WriteAsync($"data: {json}\n\n", http.RequestAborted);
                    await http.Response.Body.FlushAsync(http.RequestAborted);
                }
            }
            catch (OperationCanceledException) { /* client disconnected */ }
            catch (Exception ex)
            {
                if (headersSent)
                {
                    // Headers already sent — write error as SSE event, then end
                    var errorJson = JsonSerializer.Serialize(new { error = ex.Message }, s_jsonOptions);
                    await http.Response.WriteAsync($"data: {errorJson}\n\n", http.RequestAborted);
                }
                else
                {
                    http.Response.StatusCode = 500;
                    http.Response.ContentType = "application/json";
                    var errorJson = JsonSerializer.Serialize(
                        new { error = $"Failed to run agent: {ex.Message}" }, s_jsonOptions);
                    await http.Response.WriteAsync(errorJson, http.RequestAborted);
                }
            }
        });

        // ── Agent Graph ────────────────────────────────────────────────────
        app.MapGet("/apps/{appName}/agent-graph", (string appName, AgentLoader loader) =>
        {
            var agent = loader.GetAgent(appName);
            var graph = AgentGraphBuilder.BuildGraph(agent);
            return Results.Json(graph, s_jsonOptions);
        });

        // ── Debug Trace (per event) ────────────────────────────────────────
        app.MapGet("/debug/trace/{eventId}", (string eventId, InMemoryTraceCollector traces) =>
        {
            var trace = traces.GetTraceByEventId(eventId);
            if (trace == null) return Results.NotFound("Trace not found");
            return Results.Json(trace, s_jsonOptions);
        });

        // ── Debug Trace (per session) ──────────────────────────────────────
        app.MapGet("/debug/trace/session/{sessionId}", (string sessionId, InMemoryTraceCollector traces) =>
        {
            var spans = traces.GetSpansBySessionId(sessionId);
            return Results.Json(spans, s_jsonOptions);
        });

        // ── Event Graph (per event with highlights) ────────────────────────
        app.MapGet("/apps/{appName}/users/{userId}/sessions/{sessionId}/events/{eventId}/graph",
            async (string appName, string userId, string sessionId, string eventId,
                   RunnerManager mgr, AgentLoader loader) =>
            {
                var session = await mgr.SessionService.GetSessionAsync(new GetSessionRequest
                {
                    AppName = appName,
                    UserId = userId,
                    SessionId = sessionId,
                });

                if (session is null)
                    return Results.NotFound(new { error = $"Session not found: {sessionId}" });

                var evt = session.Events?.FirstOrDefault(e => e.Id == eventId);
                if (evt is null)
                    return Results.NotFound(new { error = $"Event not found: {eventId}" });

                var agent = loader.GetAgent(appName);

                // Build highlight pairs from function calls/responses in the event
                var highlights = new List<(string, string)>();

                if (evt.Content?.Parts != null)
                {
                    foreach (var part in evt.Content.Parts)
                    {
                        if (part.FunctionCall != null && evt.Author != null)
                            highlights.Add((evt.Author, part.FunctionCall.Name ?? ""));
                        if (part.FunctionResponse != null && evt.Author != null)
                            highlights.Add((part.FunctionResponse.Name ?? "", evt.Author));
                    }
                }

                if (highlights.Count == 0 && evt.Author != null)
                    highlights.Add((evt.Author, ""));

                var graph = AgentGraphBuilder.BuildGraph(agent, highlights);
                return Results.Json(new { dotSrc = graph }, s_jsonOptions);
            });

        // ── Eval Sets (stubs – matching JS ADK 501 behavior) ──────────────
        app.MapGet("/apps/{appName}/eval_sets",
            (string appName) => Results.Json(Array.Empty<object>(), s_jsonOptions));

        app.MapPost("/apps/{appName}/eval_sets/{evalSetId}",
            (string appName, string evalSetId) => Results.StatusCode(501));

        app.MapPost("/apps/{appName}/eval_sets/{evalSetId}/add_session",
            (string appName, string evalSetId) => Results.StatusCode(501));

        app.MapGet("/apps/{appName}/eval_sets/{evalSetId}/evals",
            (string appName, string evalSetId) => Results.StatusCode(501));

        app.MapGet("/apps/{appName}/eval_sets/{evalSetId}/evals/{evalCaseId}",
            (string appName, string evalSetId, string evalCaseId) => Results.StatusCode(501));

        app.MapPut("/apps/{appName}/eval_sets/{evalSetId}/evals/{evalCaseId}",
            (string appName, string evalSetId, string evalCaseId) => Results.StatusCode(501));

        app.MapDelete("/apps/{appName}/eval_sets/{evalSetId}/evals/{evalCaseId}",
            (string appName, string evalSetId, string evalCaseId) => Results.StatusCode(501));

        app.MapPost("/apps/{appName}/eval_sets/{evalSetId}/run_eval",
            (string appName, string evalSetId) => Results.StatusCode(501));

        // ── Eval Results (stubs) ───────────────────────────────────────────
        app.MapGet("/apps/{appName}/eval_results",
            (string appName) => Results.Json(Array.Empty<object>(), s_jsonOptions));

        app.MapGet("/apps/{appName}/eval_results/{evalResultId}",
            (string appName, string evalResultId) => Results.StatusCode(501));

        app.MapGet("/apps/{appName}/eval_metrics",
            (string appName) => Results.StatusCode(501));

        // ── Artifacts ──────────────────────────────────────────────────────
        app.MapGet("/apps/{appName}/users/{userId}/sessions/{sessionId}/artifacts/{artifactName}/versions/{versionId}",
            async (string appName, string userId, string sessionId, string artifactName, int versionId, RunnerManager mgr) =>
            {
                if (mgr.ArtifactService == null) return Results.NotFound();

                var part = await mgr.ArtifactService.LoadArtifactAsync(new GoogleAdk.Core.Abstractions.Artifacts.LoadArtifactRequest
                {
                    AppName = appName,
                    UserId = userId,
                    SessionId = sessionId,
                    Filename = artifactName,
                    Version = versionId
                });

                if (part == null) return Results.NotFound();

                // The UI expects InlineData or FileData. If it's just Text, wrap it in InlineData.
                if (part.InlineData == null && part.FileData == null && part.Text != null)
                {
                    var bytes = System.Text.Encoding.UTF8.GetBytes(part.Text);
                    part = new GoogleAdk.Core.Abstractions.Models.Part
                    {
                        InlineData = new GoogleAdk.Core.Abstractions.Models.InlineData
                        {
                            Data = Convert.ToBase64String(bytes),
                            MimeType = "text/plain",
                            DisplayName = artifactName
                        }
                    };
                }

                return Results.Json(part, s_jsonOptions);
            });

        app.MapGet("/apps/{appName}/users/{userId}/sessions/{sessionId}/artifacts/{artifactName}/versions/{versionId}/metadata",
            async (string appName, string userId, string sessionId, string artifactName, int versionId, RunnerManager mgr) =>
            {
                if (mgr.ArtifactService == null) return Results.NotFound();

                var metadata = await mgr.ArtifactService.GetArtifactVersionAsync(new GoogleAdk.Core.Abstractions.Artifacts.LoadArtifactRequest
                {
                    AppName = appName,
                    UserId = userId,
                    SessionId = sessionId,
                    Filename = artifactName,
                    Version = versionId
                });

                if (metadata == null) return Results.NotFound();
                return Results.Json(metadata, s_jsonOptions);
            });

        app.MapGet("/apps/{appName}/users/{userId}/sessions/{sessionId}/artifacts/{artifactName}/versions/metadata",
            async (string appName, string userId, string sessionId, string artifactName, RunnerManager mgr) =>
            {
                if (mgr.ArtifactService == null) return Results.NotFound();

                var metadata = await mgr.ArtifactService.GetArtifactVersionAsync(new GoogleAdk.Core.Abstractions.Artifacts.LoadArtifactRequest
                {
                    AppName = appName,
                    UserId = userId,
                    SessionId = sessionId,
                    Filename = artifactName,
                    Version = null
                });

                if (metadata == null) return Results.NotFound();
                return Results.Json(metadata, s_jsonOptions);
            });

        app.MapGet("/apps/{appName}/users/{userId}/sessions/{sessionId}/artifacts/{artifactName}/versions",
            async (string appName, string userId, string sessionId, string artifactName, RunnerManager mgr) =>
            {
                if (mgr.ArtifactService == null) return Results.NotFound();

                var versions = await mgr.ArtifactService.ListVersionsAsync(new GoogleAdk.Core.Abstractions.Artifacts.ListVersionsRequest
                {
                    AppName = appName,
                    UserId = userId,
                    SessionId = sessionId,
                    Filename = artifactName
                });

                return Results.Json(versions, s_jsonOptions);
            });

        app.MapGet("/apps/{appName}/users/{userId}/sessions/{sessionId}/artifacts",
            async (string appName, string userId, string sessionId, RunnerManager mgr) =>
            {
                if (mgr.ArtifactService == null) return Results.NotFound();

                var filenames = await mgr.ArtifactService.ListArtifactKeysAsync(new GoogleAdk.Core.Abstractions.Artifacts.ListArtifactKeysRequest
                {
                    AppName = appName,
                    UserId = userId,
                    SessionId = sessionId
                });

                return Results.Json(filenames, s_jsonOptions);
            });

        app.MapDelete("/apps/{appName}/users/{userId}/sessions/{sessionId}/artifacts/{artifactName}",
            async (string appName, string userId, string sessionId, string artifactName, RunnerManager mgr) =>
            {
                if (mgr.ArtifactService == null) return Results.NotFound();

                await mgr.ArtifactService.DeleteArtifactAsync(new GoogleAdk.Core.Abstractions.Artifacts.DeleteArtifactRequest
                {
                    AppName = appName,
                    UserId = userId,
                    SessionId = sessionId,
                    Filename = artifactName
                });

                return Results.Ok();
            });

        return app;
    }
}
