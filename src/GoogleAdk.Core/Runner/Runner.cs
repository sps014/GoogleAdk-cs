using GoogleAdk.Core.Abstractions.Artifacts;
using GoogleAdk.Core.Abstractions.Auth;
using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Memory;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Abstractions.Sessions;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Memory;
using GoogleAdk.Core.Plugins;
using System.Runtime.CompilerServices;

namespace GoogleAdk.Core.Runner;

/// <summary>
/// Configuration for the Runner.
/// </summary>
public class RunnerConfig
{
    public required string AppName { get; set; }
    public BaseAgent? Agent { get; set; }
    public Apps.AdkApp? App { get; set; }
    public required BaseSessionService SessionService { get; set; }
    public IBaseArtifactService? ArtifactService { get; set; }
    public IBaseMemoryService? MemoryService { get; set; }
    public IBaseCredentialService? CredentialService { get; set; }
    public IEnumerable<BasePlugin>? Plugins { get; set; }
    public Dictionary<string, object?>? InitialState { get; set; }
}

/// <summary>
/// The Runner orchestrates agent execution: sets up sessions, runs plugin hooks,
/// and yields events from the agent.
/// </summary>
public class Runner
{
    public string AppName { get; }
    public BaseAgent Agent { get; }
    public PluginManager PluginManager { get; }
    public IBaseArtifactService? ArtifactService { get; }
    public BaseSessionService SessionService { get; }
    public IBaseMemoryService? MemoryService { get; }
    public IBaseCredentialService? CredentialService { get; }
    public Dictionary<string, object?>? InitialState { get; }

    public Runner(RunnerConfig config)
    {
        AppName = config.AppName;
        Agent = config.App?.RootAgent ?? config.Agent
            ?? throw new InvalidOperationException("RunnerConfig requires Agent or App.");
        var plugins = config.App?.Plugins ?? config.Plugins;
        PluginManager = new PluginManager(plugins);
        ArtifactService = config.ArtifactService;
        SessionService = config.SessionService;
        MemoryService = config.MemoryService ?? new InMemoryMemoryService();
        CredentialService = config.CredentialService;
        InitialState = config.InitialState;
    }

    /// <summary>
    /// Runs the agent with a new, ephemeral session that is deleted after execution.
    /// </summary>
    public async IAsyncEnumerable<Event> RunEphemeralAsync(
        string userId,
        Content newMessage,
        Dictionary<string, object?>? stateDelta = null,
        RunConfig? runConfig = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var session = await SessionService.CreateSessionAsync(new CreateSessionRequest
        {
            AppName = AppName,
            UserId = userId,
            State = InitialState != null ? new Dictionary<string, object?>(InitialState) : null
        });

        try
        {
            await foreach (var evt in RunAsync(userId, session.Id, newMessage, stateDelta, runConfig, cancellationToken))
            {
                yield return evt;
            }
        }
        finally
        {
            await SessionService.DeleteSessionAsync(new DeleteSessionRequest
            {
                AppName = AppName,
                UserId = userId,
                SessionId = session.Id,
            });
        }
    }

    /// <summary>
    /// Runs the agent with the given message, yielding events as an async stream.
    /// </summary>
    public async IAsyncEnumerable<Event> RunAsync(
        string userId,
        string sessionId,
        Content newMessage,
        Dictionary<string, object?>? stateDelta = null,
        RunConfig? runConfig = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        runConfig ??= new RunConfig();

        // Setup session
        var session = await SessionService.GetSessionAsync(new GetSessionRequest
        {
            AppName = AppName,
            UserId = userId,
            SessionId = sessionId,
        }) ?? throw new InvalidOperationException($"Session not found: {sessionId}");

        // Create invocation context
        var invocationContext = new InvocationContext
        {
            ArtifactService = ArtifactService,
            SessionService = SessionService,
            MemoryService = MemoryService,
            CredentialService = CredentialService,
            InvocationId = $"e-{Guid.NewGuid()}",
            Agent = Agent,
            Session = session,
            UserContent = newMessage,
            RunConfig = runConfig,
            PluginManager = PluginManager,
        };

        // Plugin: onUserMessage
        var pluginUserMessage = await PluginManager.RunOnUserMessageCallbackAsync(invocationContext, newMessage);
        if (pluginUserMessage != null)
            newMessage = pluginUserMessage;

        // Append user message to session
        if (newMessage.Parts != null && newMessage.Parts.Count > 0)
        {
            Dictionary<string, int>? artifactDelta = null;
            if (runConfig.SaveInputBlobsAsArtifacts)
            {
                artifactDelta = await SaveArtifactsAsync(invocationContext.InvocationId, userId, session.Id, newMessage);
            }

            var userEvent = Event.Create(e =>
            {
                e.InvocationId = invocationContext.InvocationId;
                e.Author = "user";
                e.Content = newMessage;
                if (stateDelta != null)
                {
                    e.Actions = EventActions.Create(a => 
                    {
                        a.StateDelta = stateDelta;
                    });
                }
            });

            await SessionService.AppendEventAsync(new AppendEventRequest
            {
                Session = session,
                Event = userEvent,
            });

            // User-uploaded artifact deltas are saved but NOT propagated to
            // PendingArtifactDelta.  The Web UI already shows the user's upload
            // in the chat; emitting an extra artifactDelta would cause a
            // duplicate artifact chip to appear.

        }

        // Determine which agent should handle this (for session resumption)
        invocationContext.Agent = DetermineAgentForResumption(session, Agent);

        // Plugin: beforeRun
        var beforeRunResult = await PluginManager.RunBeforeRunCallbackAsync(invocationContext);
        if (beforeRunResult != null)
        {
            var earlyExitEvent = Event.Create(e =>
            {
                e.InvocationId = invocationContext.InvocationId;
                e.Author = "model";
                e.Content = beforeRunResult;
            });

            await SessionService.AppendEventAsync(new AppendEventRequest
            {
                Session = session,
                Event = earlyExitEvent,
            });
            yield return earlyExitEvent;
            yield break;
        }

        // Run the agent
        await foreach (var evt in invocationContext.Agent.RunAsync(invocationContext).WithCancellation(cancellationToken))
        {
            if (evt.Partial != true)
            {
                await SessionService.AppendEventAsync(new AppendEventRequest
                {
                    Session = session,
                    Event = evt,
                });
            }

            // Plugin: onEvent
            var modifiedEvent = await PluginManager.RunOnEventCallbackAsync(invocationContext, evt);
            yield return modifiedEvent ?? evt;
        }

        // Plugin: afterRun
        await PluginManager.RunAfterRunCallbackAsync(invocationContext);
    }

    /// <summary>
    /// Runs the agent in live/bidirectional mode.
    /// </summary>
    public async IAsyncEnumerable<Event> RunLiveAsync(
        string userId,
        string sessionId,
        LiveRequestQueue liveRequestQueue,
        Content? initialMessage = null,
        Dictionary<string, object?>? stateDelta = null,
        RunConfig? runConfig = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        runConfig ??= new RunConfig { StreamingMode = StreamingMode.Bidi };

        var session = await SessionService.GetSessionAsync(new GetSessionRequest
        {
            AppName = AppName,
            UserId = userId,
            SessionId = sessionId,
        }) ?? throw new InvalidOperationException($"Session not found: {sessionId}");

        var invocationContext = new InvocationContext
        {
            ArtifactService = ArtifactService,
            SessionService = SessionService,
            MemoryService = MemoryService,
            CredentialService = CredentialService,
            InvocationId = $"e-{Guid.NewGuid()}",
            Agent = Agent,
            Session = session,
            UserContent = initialMessage,
            RunConfig = runConfig,
            PluginManager = PluginManager,
            LiveRequestQueue = liveRequestQueue,
        };

        if (initialMessage != null)
        {
            var pluginUserMessage = await PluginManager.RunOnUserMessageCallbackAsync(invocationContext, initialMessage);
            if (pluginUserMessage != null)
                initialMessage = pluginUserMessage;

            if (initialMessage.Parts != null && initialMessage.Parts.Count > 0)
            {
                var userEvent = Event.Create(e =>
                {
                    e.InvocationId = invocationContext.InvocationId;
                    e.Author = "user";
                    e.Content = initialMessage;
                    if (stateDelta != null)
                    {
                        e.Actions = EventActions.Create(a => a.StateDelta = stateDelta);
                    }
                });

                await SessionService.AppendEventAsync(new AppendEventRequest
                {
                    Session = session,
                    Event = userEvent,
                });
            }
        }

        invocationContext.Agent = DetermineAgentForResumption(session, Agent);

        var beforeRunResult = await PluginManager.RunBeforeRunCallbackAsync(invocationContext);
        if (beforeRunResult != null)
        {
            var earlyExitEvent = Event.Create(e =>
            {
                e.InvocationId = invocationContext.InvocationId;
                e.Author = "model";
                e.Content = beforeRunResult;
            });

            await SessionService.AppendEventAsync(new AppendEventRequest
            {
                Session = session,
                Event = earlyExitEvent,
            });
            yield return earlyExitEvent;
            yield break;
        }

        await foreach (var evt in invocationContext.Agent
            .RunLiveAsync(invocationContext, liveRequestQueue, cancellationToken)
            .WithCancellation(cancellationToken))
        {
            if (evt.Partial != true)
            {
                await SessionService.AppendEventAsync(new AppendEventRequest
                {
                    Session = session,
                    Event = evt,
                });
            }

            var modifiedEvent = await PluginManager.RunOnEventCallbackAsync(invocationContext, evt);
            yield return modifiedEvent ?? evt;
        }
    }

    /// <summary>
    /// Determines which agent should handle the session based on the last event.
    /// Used for session resumption after agent transfers.
    /// </summary>
    private BaseAgent DetermineAgentForResumption(Session session, BaseAgent rootAgent)
    {
        if (session.Events.Count == 0)
            return rootAgent;

        // Case 1: If the last event is a function response, find the original caller
        var lastEvent = session.Events[^1];
        var functionResponse = lastEvent.Content?.Parts?.FirstOrDefault(p => p.FunctionResponse != null);
        if (functionResponse?.FunctionResponse?.Id != null)
        {
            var callId = functionResponse.FunctionResponse.Id;
            for (int i = session.Events.Count - 2; i >= 0; i--)
            {
                var callEvent = session.Events[i];
                var calls = callEvent.GetFunctionCalls();
                if (calls?.Any(c => c.Id == callId) == true)
                {
                    var foundAgent = rootAgent.FindAgent(callEvent.Author ?? string.Empty);
                    if (foundAgent != null) return foundAgent;
                }
            }
        }

        // Case 2: Find the last agent that emitted a message and is routable
        for (int i = session.Events.Count - 1; i >= 0; i--)
        {
            var evt = session.Events[i];
            if (evt.Author == "user" || string.IsNullOrEmpty(evt.Author))
                continue;

            if (evt.Author == rootAgent.Name)
                return rootAgent;

            var agent = rootAgent.FindSubAgent(evt.Author!);
            if (agent != null && IsRoutableLlmAgent(agent))
                return agent;
        }

        return rootAgent;
    }

    private async Task<Dictionary<string, int>?> SaveArtifactsAsync(
        string invocationId,
        string userId,
        string sessionId,
        Content message)
    {
        if (ArtifactService == null || message.Parts == null || message.Parts.Count == 0)
            return null;

        var delta = new Dictionary<string, int>();

        for (int i = 0; i < message.Parts.Count; i++)
        {
            var part = message.Parts[i];
            if (part.InlineData == null)
                continue;

            var fileName = part.InlineData.DisplayName;
            if (string.IsNullOrWhiteSpace(fileName))
            {
                var mimeType = part.InlineData.MimeType ?? "application/octet-stream";
                var extension = mimeType switch
                {
                    "text/plain" => ".txt",
                    "text/html" => ".html",
                    "application/json" => ".json",
                    "application/xml" => ".xml",
                    "image/png" => ".png",
                    "image/jpeg" => ".jpeg",
                    "image/gif" => ".gif",
                    "application/pdf" => ".pdf",
                    "application/msword" => ".doc",
                    _ => ""
                };
                fileName = $"artifact_{invocationId}_{i}{extension}";
            }

            var version = await ArtifactService.SaveArtifactAsync(new SaveArtifactRequest
            {
                AppName = AppName,
                UserId = userId,
                SessionId = sessionId,
                Filename = fileName,
                Artifact = part,
            });

            delta[fileName] = version;

            message.Parts[i] = new Part
            {
                Text = $"Uploaded file: {fileName}. It is saved into artifacts"
            };
        }

        return delta.Count > 0 ? delta : null;
    }

    private static bool IsRoutableLlmAgent(BaseAgent? agent)
    {
        while (agent != null)
        {
            if (agent is not LlmAgent llmAgent)
                return false;
            if (llmAgent.DisallowTransferToParent)
                return false;
            agent = agent.ParentAgent;
        }
        return true;
    }
}
