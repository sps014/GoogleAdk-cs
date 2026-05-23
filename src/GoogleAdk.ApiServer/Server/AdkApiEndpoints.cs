using System.Text.Json;
using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Sessions;
using GoogleAdk.Core.Agents;
using Microsoft.AspNetCore.Mvc;
using System.Net.WebSockets;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace GoogleAdk.ApiServer;

/// <summary>
/// Registers all ADK API endpoints on the WebApplication, matching the JS ADK API surface.
/// </summary>
public static class AdkApiEndpoints
{
    public static WebApplication MapAdkApi(this WebApplication app)
    {
        // ── List Apps ──────────────────────────────────────────────────────
        app.MapGet("/list-apps", (AgentLoader loader) =>
        {
            return Results.Json(loader.ListAgents(), AdkJsonOptions.Default);
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
                return Results.Json(result.Sessions, AdkJsonOptions.Default);
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
                return session is null ? Results.NotFound() : Results.Json(session, AdkJsonOptions.Default);
            });

        app.MapPost("/apps/{appName}/users/{userId}/sessions",
            async (string appName, string userId, [FromBody] CreateSessionBody? body, RunnerManager mgr) =>
            {
                var session = await mgr.SessionService.CreateSessionAsync(new CreateSessionRequest
                {
                    AppName = appName,
                    UserId = userId,
                    State = body?.State ?? (mgr.InitialState != null ? new Dictionary<string, object?>(mgr.InitialState) : null),
                });
                return Results.Json(session, AdkJsonOptions.Default, statusCode: 201);
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
                    State = body?.State ?? (mgr.InitialState != null ? new Dictionary<string, object?>(mgr.InitialState) : null),
                });
                return Results.Json(session, AdkJsonOptions.Default, statusCode: 201);
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
        app.MapPost("/run", async (HttpContext http, [FromBody] RunAgentRequest req, RunnerManager mgr, AdkServerOptions options) =>
        {
            try
            {
                var runner = mgr.GetOrCreate(req.AppName);
                var message = req.ResolveMessage();
                var events = new List<Event>();

                var runConfig = req.RunConfig ?? new GoogleAdk.Core.Agents.RunConfig();
                runConfig.SaveInputBlobsAsArtifacts = req.RunConfig?.SaveInputBlobsAsArtifacts ?? true;
                if (req.RunConfig == null || req.RunConfig.MaxLlmCalls == 500) // Default in RunConfig is 500
                    runConfig.MaxLlmCalls = options.MaxLlmCalls;

                await foreach (var evt in runner.RunAsync(
                    req.UserId, req.SessionId, message,
                    stateDelta: req.StateDelta,
                    runConfig: runConfig,
                    cancellationToken: http.RequestAborted))
                {
                    events.Add(evt);
                }

                return Results.Json(events, AdkJsonOptions.Default);
            }
            catch (Exception ex)
            {
                http.RequestServices.GetService<ILoggerFactory>()?.CreateLogger("AdkApiEndpoints")?.LogError(ex, "Failed to run agent.");
                return Results.Json(new ApiErrorResponse($"Failed to run agent: {ex.Message}"),
                    statusCode: 500, options: AdkJsonOptions.Default);
            }
        });

        // ── Run SSE (streaming) ────────────────────────────────────────────
        app.MapPost("/run_sse", async (HttpContext http, [FromBody] RunAgentRequest req, RunnerManager mgr, AdkServerOptions options) =>
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
                if (req.RunConfig == null || req.RunConfig.MaxLlmCalls == 500)
                    runConfig.MaxLlmCalls = options.MaxLlmCalls;
                runConfig.StreamingMode = req.Streaming
                        ? GoogleAdk.Core.Agents.StreamingMode.Sse
                        : GoogleAdk.Core.Agents.StreamingMode.None;

                await foreach (var evt in runner.RunAsync(
                    req.UserId, req.SessionId, message,
                    stateDelta: req.StateDelta,
                    runConfig: runConfig,
                    cancellationToken: http.RequestAborted))
                {
                    // ADK Web UI handles rendering artifacts correctly.
                    var json = JsonSerializer.Serialize(evt, AdkJsonOptions.Default);
                    await http.Response.WriteAsync($"data: {json}\n\n", http.RequestAborted);
                    await http.Response.Body.FlushAsync(http.RequestAborted);
                }
            }
            catch (OperationCanceledException) { /* client disconnected */ }
            catch (Exception ex)
            {
                http.RequestServices.GetService<ILoggerFactory>()?.CreateLogger("AdkApiEndpoints")?.LogError(ex, "Failed to run agent (SSE).");
                if (headersSent)
                {
                    // Headers already sent — write error as SSE event, then end
                    var errorJson = JsonSerializer.Serialize(new ApiErrorResponse(ex.Message), AdkJsonOptions.Default);
                    await http.Response.WriteAsync($"data: {errorJson}\n\n", http.RequestAborted);
                }
                else
                {
                    http.Response.StatusCode = 500;
                    http.Response.ContentType = "application/json";
                    var errorJson = JsonSerializer.Serialize(
                        new ApiErrorResponse($"Failed to run agent: {ex.Message}"), AdkJsonOptions.Default);
                    await http.Response.WriteAsync(errorJson, http.RequestAborted);
                }
            }
        });

        // ── Run Live (WebSocket) ───────────────────────────────────────────
        app.Map("/run_live", async (HttpContext http, RunnerManager mgr, AdkServerOptions options) =>
        {
            if (!http.WebSockets.IsWebSocketRequest)
                return Results.BadRequest("WebSocket required.");

            using var socket = await http.WebSockets.AcceptWebSocketAsync();
            var initPayload = await ReceiveJsonAsync(socket, http.RequestAborted, options.WebSocketBufferSize);
            if (string.IsNullOrWhiteSpace(initPayload))
                return Results.BadRequest("Missing init payload.");

            var initReq = JsonSerializer.Deserialize<RunLiveRequest>(initPayload, AdkJsonOptions.Default);
            if (initReq == null || string.IsNullOrWhiteSpace(initReq.AppName))
                return Results.BadRequest("Invalid init payload.");

            var runner = mgr.GetOrCreate(initReq.AppName);
            var liveQueue = new LiveRequestQueue();

            var runConfig = initReq.RunConfig ?? new GoogleAdk.Core.Agents.RunConfig
            {
                StreamingMode = GoogleAdk.Core.Agents.StreamingMode.Bidi
            };
            if (initReq.RunConfig == null || initReq.RunConfig.MaxLlmCalls == 500)
                runConfig.MaxLlmCalls = options.MaxLlmCalls;

            var sendTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (var evt in runner.RunLiveAsync(
                        initReq.UserId, initReq.SessionId, liveQueue,
                        initialMessage: initReq.InitialMessage,
                        stateDelta: initReq.StateDelta,
                        runConfig: runConfig,
                        cancellationToken: http.RequestAborted))
                    {
                        var json = JsonSerializer.Serialize(evt, AdkJsonOptions.Default);
                        await SendJsonAsync(socket, json, http.RequestAborted);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    http.RequestServices.GetService<ILoggerFactory>()?.CreateLogger("AdkApiEndpoints")?.LogError(ex, "Error in /run_live sendTask.");
                    if (socket.State == WebSocketState.Open)
                    {
                        var errorJson = JsonSerializer.Serialize(new ApiErrorResponse(ex.Message), AdkJsonOptions.Default);
                        await SendJsonAsync(socket, errorJson, CancellationToken.None);
                    }
                }
            }, http.RequestAborted);

            try
            {
                while (socket.State == WebSocketState.Open && !http.RequestAborted.IsCancellationRequested)
                {
                    var payload = await ReceiveJsonAsync(socket, http.RequestAborted, options.WebSocketBufferSize);
                    if (payload == null) break;

                    var msg = JsonSerializer.Deserialize<LiveRequestMessage>(payload, AdkJsonOptions.Default);
                    if (msg == null) continue;

                    if (msg.Close)
                    {
                        liveQueue.Close();
                        break;
                    }

                    if (msg.Content != null)
                        await liveQueue.SendContentAsync(msg.Content, http.RequestAborted);
                    else if (msg.RealtimePart != null)
                        await liveQueue.SendRealtimeAsync(msg.RealtimePart, http.RequestAborted);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                http.RequestServices.GetService<ILoggerFactory>()?.CreateLogger("AdkApiEndpoints")?.LogError(ex, "Error in /run_live receive loop.");
                if (socket.State == WebSocketState.Open)
                {
                    var errorJson = JsonSerializer.Serialize(new ApiErrorResponse(ex.Message), AdkJsonOptions.Default);
                    await SendJsonAsync(socket, errorJson, CancellationToken.None);
                }
            }
            finally
            {
                liveQueue.Close();
                await sendTask;
            }

            return Results.Empty;
        });

        // ── Builder App (stub for Python ADK UI compatibility) ──────────
        app.MapGet("/builder/app/{appName}", (string appName) =>
        {
            return Results.Text("", "application/x-yaml");
        });

        // ── Version (stub for Python ADK UI compatibility) ──────────────
        app.MapGet("/version", () =>
        {
            return Results.Json(new
            {
                version = "0.1.0",
                language = "csharp",
                language_version = Environment.Version.ToString()
            }, AdkJsonOptions.Default);
        });

        // ── Dev UI ─────────────────────────────────────────────────────────
        app.MapGet("/dev/{appName}/graph", (string appName, AgentLoader loader, [FromQuery(Name = "dark_mode")] bool darkMode = false) =>
        {
            var agent = loader.GetAgent(appName);
            var graph = AgentGraphBuilder.BuildGraph(agent, null, darkMode);
            return Results.Json(new { dotSrc = graph }, AdkJsonOptions.Default);
        });

        app.MapGet("/apps/{appName}/agent-graph", (string appName, AgentLoader loader) =>
        {
            var agent = loader.GetAgent(appName);
            var graph = AgentGraphBuilder.BuildGraph(agent, null, false);
            return Results.Json(new { dotSrc = graph }, AdkJsonOptions.Default);
        });

        app.MapGet("/dev/build_graph/{appName}", (string appName, AgentLoader loader) =>
        {
            var agent = loader.GetAgent(appName);
            var rootAgentNode = AgentGraphBuilder.BuildGraph(agent);
            return Results.Json(new { name = appName, root_agent = rootAgentNode }, AdkJsonOptions.Default);
        });

        app.MapGet("/dev/build_graph_image/{appName}", (string appName, AgentLoader loader, [FromQuery(Name = "dark_mode")] bool darkMode = false, [FromQuery(Name = "node")] string? node = null) =>
        {
            var agent = loader.GetAgent(appName);
            var graph = AgentGraphBuilder.BuildGraph(agent, null, darkMode);

            if (!string.IsNullOrEmpty(node))
            {
                return Results.Json(new { dotSrc = graph }, AdkJsonOptions.Default);
            }

            // Return a dictionary where the key is the app name so the UI's Object.entries() loop works
            var responseDict = new Dictionary<string, object>
            {
                { appName, new { dotSrc = graph } }
            };
            return Results.Json(responseDict, AdkJsonOptions.Default);
        });

        // ── Debug Trace (per event) ────────────────────────────────────────
        app.MapGet("/debug/trace/{eventId}", (string eventId, InMemoryTraceCollector traces) =>
        {
            var trace = traces.GetTraceByEventId(eventId);
            if (trace == null) return Results.NotFound("Trace not found");
            return Results.Json(trace, AdkJsonOptions.Default);
        });

        // ── Debug Trace (per session) ──────────────────────────────────────
        app.MapGet("/debug/trace/session/{sessionId}", (string sessionId, InMemoryTraceCollector traces) =>
        {
            var spans = traces.GetSpansBySessionId(sessionId);
            return Results.Json(spans, AdkJsonOptions.Default);
        });

        // ── Event Graph (per event with highlights) ────────────────────────
        app.MapGet("/apps/{appName}/users/{userId}/sessions/{sessionId}/events/{eventId}/graph",
            async (string appName, string userId, string sessionId, string eventId,
                   RunnerManager mgr, AgentLoader loader, [FromQuery(Name = "dark_mode")] bool darkMode = false) =>
            {
                var session = await mgr.SessionService.GetSessionAsync(new GetSessionRequest
                {
                    AppName = appName,
                    UserId = userId,
                    SessionId = sessionId,
                });

                if (session is null)
                    return Results.NotFound(new ApiErrorResponse($"Session not found: {sessionId}"));

                var evt = session.Events?.FirstOrDefault(e => e.Id == eventId);
                if (evt is null)
                    return Results.NotFound(new ApiErrorResponse($"Event not found: {eventId}"));

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

                var graph = AgentGraphBuilder.BuildGraph(agent, highlights, darkMode);
                return Results.Json(new { dotSrc = graph }, AdkJsonOptions.Default);
            });

        // ── Eval Sets (stubs – matching JS ADK 501 behavior) ──────────────
        app.MapGet("/apps/{appName}/eval_sets",
            (string appName) => Results.Json(Array.Empty<object>(), AdkJsonOptions.Default));

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
            (string appName) => Results.Json(Array.Empty<object>(), AdkJsonOptions.Default));

        app.MapGet("/apps/{appName}/eval_results/{evalResultId}",
            (string appName, string evalResultId) => Results.StatusCode(501));

        app.MapGet("/apps/{appName}/eval_metrics",
            (string appName) => Results.StatusCode(501));

        // ── Artifacts ──────────────────────────────────────────────────────
        app.MapGet("/apps/{appName}/users/{userId}/sessions/{sessionId}/artifacts/{artifactName}/versions/{versionIdStr}",
            async (string appName, string userId, string sessionId, string artifactName, string versionIdStr, RunnerManager mgr) =>
            {
                if (mgr.ArtifactService == null) return Results.NotFound();

                int? versionId = null;
                if (versionIdStr != "latest" && int.TryParse(versionIdStr, out var v))
                {
                    versionId = v;
                }

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

                return Results.Json(part, AdkJsonOptions.Default);
            });

        app.MapGet("/apps/{appName}/users/{userId}/sessions/{sessionId}/artifacts/{artifactName}/versions/{versionIdStr}/metadata",
            async (string appName, string userId, string sessionId, string artifactName, string versionIdStr, RunnerManager mgr) =>
            {
                if (mgr.ArtifactService == null) return Results.NotFound();

                int? versionId = null;
                if (versionIdStr != "latest" && int.TryParse(versionIdStr, out var v))
                {
                    versionId = v;
                }

                var metadata = await mgr.ArtifactService.GetArtifactVersionAsync(new GoogleAdk.Core.Abstractions.Artifacts.LoadArtifactRequest
                {
                    AppName = appName,
                    UserId = userId,
                    SessionId = sessionId,
                    Filename = artifactName,
                    Version = versionId
                });

                if (metadata == null) return Results.NotFound();
                return Results.Json(metadata, AdkJsonOptions.Default);
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
                return Results.Json(metadata, AdkJsonOptions.Default);
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

                return Results.Json(versions, AdkJsonOptions.Default);
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

                return Results.Json(filenames, AdkJsonOptions.Default);
            });

        app.MapPost("/apps/{appName}/users/{userId}/sessions/{sessionId}/artifacts",
            async (string appName, string userId, string sessionId, [FromBody] SaveArtifactBody req, RunnerManager mgr) =>
            {
                if (mgr.ArtifactService == null) return Results.StatusCode(501);

                var version = await mgr.ArtifactService.SaveArtifactAsync(new GoogleAdk.Core.Abstractions.Artifacts.SaveArtifactRequest
                {
                    AppName = appName,
                    UserId = userId,
                    SessionId = sessionId,
                    Filename = req.Filename,
                    Artifact = req.Artifact,
                    CustomMetadata = req.CustomMetadata
                });

                var metadata = await mgr.ArtifactService.GetArtifactVersionAsync(new GoogleAdk.Core.Abstractions.Artifacts.LoadArtifactRequest
                {
                    AppName = appName,
                    UserId = userId,
                    SessionId = sessionId,
                    Filename = req.Filename,
                    Version = version
                });

                return Results.Json(metadata, AdkJsonOptions.Default);
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

    private static async Task<string?> ReceiveJsonAsync(WebSocket socket, CancellationToken cancellationToken, int bufferSize = 8192)
    {
        var buffer = new ArraySegment<byte>(new byte[bufferSize]);
        using var ms = new MemoryStream();

        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
                return null;

            ms.Write(buffer.Array!, buffer.Offset, result.Count);
            if (result.EndOfMessage) break;
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static Task SendJsonAsync(WebSocket socket, string json, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        return socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
    }
}
