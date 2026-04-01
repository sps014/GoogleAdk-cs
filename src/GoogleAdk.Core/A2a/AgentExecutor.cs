// Copyright 2026 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Abstractions.Sessions;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Runner;

namespace GoogleAdk.Core.A2a;

public sealed class AgentExecutorConfig
{
    public required RunnerOrRunnerConfig Runner { get; set; }
    public RunConfig? RunConfig { get; set; }
}

public delegate global::System.Threading.Tasks.Task<RunnerOrRunnerConfig> RunnerFactory();

public sealed class RunnerOrRunnerConfig
{
    public global::GoogleAdk.Core.Runner.Runner? Runner { get; init; }
    public global::GoogleAdk.Core.Runner.RunnerConfig? RunnerConfig { get; init; }
    public RunnerFactory? Factory { get; init; }
}

public sealed class A2aAgentExecutor
{
    private readonly AgentExecutorConfig _config;
    private readonly Dictionary<string, string> _agentPartialArtifactIdsMap = new();

    public A2aAgentExecutor(AgentExecutorConfig config)
    {
        _config = config;
    }

    public async IAsyncEnumerable<IA2aEvent> ExecuteAsync(
        MessageSendParams request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (request.Message == null)
            throw new InvalidOperationException("message not provided");

        var userId = $"A2A_USER_{request.Message.ContextId ?? Guid.NewGuid().ToString()}";
        var sessionId = request.Message.ContextId ?? Guid.NewGuid().ToString();
        var taskId = request.Message.TaskId ?? Guid.NewGuid().ToString();

        var userContent = PartConverterUtils.ToContent(request.Message);
        var runner = await GetRunnerAsync(_config.Runner);
        var session = await GetOrCreateSessionAsync(
            userId,
            sessionId,
            runner.SessionService,
            runner.AppName);
        var executorContext = ExecutorContextFactory.Create(session, userContent, request, taskId, sessionId);

        if (request.Message.TaskId == null)
        {
            yield return A2aEventHelpers.CreateTask(taskId, sessionId, request.Message);
        }

        yield return A2aEventHelpers.CreateTaskWorkingEvent(taskId, sessionId);

        var adkEvents = new List<Event>();
        await foreach (var adkEvent in runner.RunAsync(
            userId,
            sessionId,
            userContent,
            runConfig: _config.RunConfig,
            cancellationToken: cancellationToken))
        {
            adkEvents.Add(adkEvent);
            var a2aEvent = ConvertAdkEventToA2aEvent(adkEvent, executorContext);
            if (a2aEvent == null) continue;
            yield return a2aEvent;
        }

        var finalStatus = EventProcessorUtils.GetFinalTaskStatusUpdate(adkEvents, executorContext);
        yield return finalStatus;
    }

    private TaskArtifactUpdateEvent? ConvertAdkEventToA2aEvent(Event adkEvent, ExecutorContext context)
    {
        var a2aParts = PartConverterUtils.ToA2aParts(adkEvent.Content?.Parts, adkEvent.LongRunningToolIds);
        if (a2aParts.Count == 0) return null;

        var artifactId = _agentPartialArtifactIdsMap.TryGetValue(adkEvent.Author ?? string.Empty, out var existing)
            ? existing
            : Guid.NewGuid().ToString();

        var a2aEvent = A2aEventHelpers.CreateTaskArtifactUpdateEvent(
            context.TaskId,
            context.ContextId,
            artifactId,
            a2aParts,
            MetadataConverterUtils.GetA2AEventMetadata(adkEvent, context.AppName, context.UserId, context.SessionId),
            append: adkEvent.Partial,
            lastChunk: !adkEvent.Partial);

        if (adkEvent.Partial == true)
            _agentPartialArtifactIdsMap[adkEvent.Author ?? string.Empty] = artifactId;
        else
            _agentPartialArtifactIdsMap.Remove(adkEvent.Author ?? string.Empty);

        return a2aEvent;
    }

    private static async global::System.Threading.Tasks.Task<Session> GetOrCreateSessionAsync(
        string userId,
        string sessionId,
        BaseSessionService sessionService,
        string appName)
    {
        var session = await sessionService.GetSessionAsync(new GetSessionRequest
        {
            AppName = appName,
            UserId = userId,
            SessionId = sessionId,
        });
        if (session != null) return session;

        return await sessionService.CreateSessionAsync(new CreateSessionRequest
        {
            AppName = appName,
            UserId = userId,
            SessionId = sessionId,
        });
    }

    private static async global::System.Threading.Tasks.Task<global::GoogleAdk.Core.Runner.Runner> GetRunnerAsync(RunnerOrRunnerConfig runnerOrConfig)
    {
        if (runnerOrConfig.Factory != null)
        {
            var result = await runnerOrConfig.Factory();
            return await GetRunnerAsync(result);
        }

        if (runnerOrConfig.Runner != null)
            return runnerOrConfig.Runner;

        if (runnerOrConfig.RunnerConfig != null)
            return new global::GoogleAdk.Core.Runner.Runner(runnerOrConfig.RunnerConfig);

        throw new InvalidOperationException("Invalid runner configuration.");
    }
}

