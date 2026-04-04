using System.Text.Json;
using GoogleAdk.Core.A2a;
using GoogleAdk.Core.Runner;
using Microsoft.AspNetCore.Mvc;
using A2aTask = GoogleAdk.Core.A2a.A2aTask;
using A2aTaskStatus = GoogleAdk.Core.A2a.TaskStatus;
using RunConfig = GoogleAdk.Core.Agents.RunConfig;
using StreamingMode = GoogleAdk.Core.Agents.StreamingMode;
using Task = System.Threading.Tasks.Task;

namespace GoogleAdk.ApiServer;

public static class A2aApiEndpoints
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public static WebApplication MapA2aApi(this WebApplication app)
    {
        app.MapGet("/a2a/{appName}/" + AgentCardConstants.AgentCardPath,
            async (HttpContext http, string appName, AgentLoader loader) =>
            {
                var agent = loader.GetAgent(appName);
                var baseUrl = $"{http.Request.Scheme}://{http.Request.Host}/a2a/{appName}";
                var transports = new[]
                {
                    new AgentInterface { Url = $"{baseUrl}/jsonrpc", Transport = "JSONRPC" },
                    new AgentInterface { Url = $"{baseUrl}/rest", Transport = "HTTP+JSON" },
                };
                var card = await AgentCardBuilder.GetA2AAgentCardAsync(agent, transports);
                return Results.Json(card, s_jsonOptions);
            })
            .Produces<AgentCard>()
            .WithTags("A2A");

        app.MapPost("/a2a/{appName}/jsonrpc", async (
            HttpContext http,
            string appName,
            [FromBody] JsonRpcRequest request,
            AgentLoader loader,
            RunnerManager manager) =>
        {
            if (request.Method == "message/stream")
            {
                http.Response.ContentType = "text/event-stream";
                http.Response.Headers.CacheControl = "no-cache";
                http.Response.Headers.Connection = "keep-alive";
                http.Response.Headers["X-Accel-Buffering"] = "no";
                await http.Response.Body.FlushAsync(http.RequestAborted);

                await foreach (var evt in ExecuteA2aAsync(appName, request, loader, manager, http.RequestAborted))
                {
                    var rpc = new JsonRpcResponse
                    {
                        JsonRpc = "2.0",
                        Id = request.Id,
                        Result = JsonSerializer.SerializeToElement(evt, evt.GetType(), s_jsonOptions),
                    };
                    var json = JsonSerializer.Serialize(rpc, s_jsonOptions);
                    await http.Response.WriteAsync($"data: {json}\n\n", http.RequestAborted);
                    await http.Response.Body.FlushAsync(http.RequestAborted);
                }
                return Results.Empty;
            }

            var single = await ExecuteA2aSingleAsync(appName, request, loader, manager, http.RequestAborted);
            return Results.Json(single, s_jsonOptions);
        })
        .Produces<JsonRpcResponse>()
        .WithTags("A2A");

        app.MapPost("/a2a/{appName}/rest/message:send", async (
            HttpContext http,
            string appName,
            [FromBody] MessageSendParams request,
            AgentLoader loader,
            RunnerManager manager) =>
        {
            var result = await ExecuteA2aSingleAsync(appName, request, loader, manager, http.RequestAborted);
            return Results.Json(result, s_jsonOptions);
        })
        .Produces<RestSendResponse>()
        .WithTags("A2A");

        app.MapPost("/a2a/{appName}/rest/message:stream", async (
            HttpContext http,
            string appName,
            [FromBody] MessageSendParams request,
            AgentLoader loader,
            RunnerManager manager) =>
        {
            http.Response.ContentType = "text/event-stream";
            http.Response.Headers.CacheControl = "no-cache";
            http.Response.Headers.Connection = "keep-alive";
            http.Response.Headers["X-Accel-Buffering"] = "no";
            await http.Response.Body.FlushAsync(http.RequestAborted);

            await foreach (var evt in ExecuteA2aAsync(appName, request, loader, manager, http.RequestAborted))
            {
                var json = JsonSerializer.Serialize(evt, evt.GetType(), s_jsonOptions);
                await http.Response.WriteAsync($"data: {json}\n\n", http.RequestAborted);
                await http.Response.Body.FlushAsync(http.RequestAborted);
            }

            return Results.Empty;
        })
        .WithTags("A2A");

        return app;
    }

    private static async IAsyncEnumerable<IA2aEvent> ExecuteA2aAsync(
        string appName,
        JsonRpcRequest request,
        AgentLoader loader,
        RunnerManager manager,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (request.Params is null)
            yield break;
        var parameters = JsonSerializer.Deserialize<MessageSendParams>(
            JsonSerializer.Serialize(request.Params, s_jsonOptions), s_jsonOptions);
        if (parameters == null) yield break;

        await foreach (var evt in ExecuteA2aAsync(appName, parameters, loader, manager, cancellationToken))
            yield return evt;
    }

    private static async IAsyncEnumerable<IA2aEvent> ExecuteA2aAsync(
        string appName,
        MessageSendParams request,
        AgentLoader loader,
        RunnerManager manager,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var agent = loader.GetAgent(appName);
        var runner = manager.GetOrCreate(appName);
        var executor = new A2aAgentExecutor(new AgentExecutorConfig
        {
            Runner = new RunnerOrRunnerConfig { Runner = runner },
            RunConfig = new RunConfig
            {
                StreamingMode = StreamingMode.Sse,
            },
        });

        await foreach (var evt in executor.ExecuteAsync(request, cancellationToken))
            yield return evt;
    }

    private static async Task<object> ExecuteA2aSingleAsync(
        string appName,
        JsonRpcRequest request,
        AgentLoader loader,
        RunnerManager manager,
        CancellationToken cancellationToken)
    {
        if (request.Params == null)
            return new JsonRpcResponse { JsonRpc = "2.0", Id = request.Id, Error = new JsonRpcError { Code = -32602, Message = "Invalid params" } };

        var parameters = JsonSerializer.Deserialize<MessageSendParams>(
            JsonSerializer.Serialize(request.Params, s_jsonOptions), s_jsonOptions);
        if (parameters == null)
            return new JsonRpcResponse { JsonRpc = "2.0", Id = request.Id, Error = new JsonRpcError { Code = -32602, Message = "Invalid params" } };

        var task = await ExecuteA2aTaskAsync(appName, parameters, loader, manager, cancellationToken);
        var payloadElement = JsonSerializer.SerializeToElement(task, s_jsonOptions);
        return new JsonRpcResponse { JsonRpc = "2.0", Id = request.Id, Result = payloadElement };
    }

    private static async Task<object> ExecuteA2aSingleAsync(
        string appName,
        MessageSendParams request,
        AgentLoader loader,
        RunnerManager manager,
        CancellationToken cancellationToken)
    {
        var task = await ExecuteA2aTaskAsync(appName, request, loader, manager, cancellationToken);
        return new RestSendResponse { Task = task };
    }

    private static async Task<A2aTask> ExecuteA2aTaskAsync(
        string appName,
        MessageSendParams request,
        AgentLoader loader,
        RunnerManager manager,
        CancellationToken cancellationToken)
    {
        var events = new List<IA2aEvent>();
        await foreach (var evt in ExecuteA2aAsync(appName, request, loader, manager, cancellationToken))
            events.Add(evt);
        return BuildTaskFromEvents(request, events);
    }

    private static A2aTask BuildTaskFromEvents(MessageSendParams request, List<IA2aEvent> events)
    {
        var taskId = request.Message.TaskId ?? Guid.NewGuid().ToString();
        var contextId = request.Message.ContextId ?? Guid.NewGuid().ToString();

        var artifacts = new Dictionary<string, TaskArtifact>();
        TaskStatusUpdateEvent? finalStatus = null;
        foreach (var evt in events)
        {
            if (evt is TaskStatusUpdateEvent status && status.Final)
                finalStatus = status;
            if (evt is TaskArtifactUpdateEvent artifactUpdate)
            {
                var id = artifactUpdate.Artifact.ArtifactId;
                if (!artifacts.TryGetValue(id, out var artifact))
                {
                    artifact = new TaskArtifact { ArtifactId = id, Parts = new List<A2aPart>() };
                    artifacts[id] = artifact;
                }

                if (artifactUpdate.Append == true && artifact.Parts != null)
                    artifact.Parts.AddRange(artifactUpdate.Artifact.Parts ?? new List<A2aPart>());
                else
                    artifact.Parts = artifactUpdate.Artifact.Parts ?? new List<A2aPart>();
            }
        }

        return new A2aTask
        {
            Id = taskId,
            ContextId = contextId,
            Status = finalStatus?.Status ?? new A2aTaskStatus { State = TaskState.Completed },
            Artifacts = artifacts.Values.ToList(),
            History = new List<Message> { request.Message },
            Metadata = finalStatus?.Metadata,
        };
    }
}

