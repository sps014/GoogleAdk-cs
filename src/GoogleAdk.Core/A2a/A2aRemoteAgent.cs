// Copyright 2026 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Agents;

namespace GoogleAdk.Core.A2a;

public delegate global::System.Threading.Tasks.Task BeforeA2aRequestCallback(InvocationContext ctx, MessageSendParams parameters);
public delegate global::System.Threading.Tasks.Task AfterA2aRequestCallback(InvocationContext ctx, IA2aEvent response);

public sealed class RemoteA2aAgentConfig : BaseAgentConfig
{
    public AgentCard? AgentCard { get; set; }
    public string? AgentCardSource { get; set; }
    public A2aClient? Client { get; set; }
    public Func<AgentCard, A2aClient>? ClientFactory { get; set; }
    public MessageSendConfiguration? MessageSendConfig { get; set; }
    public List<BeforeA2aRequestCallback>? BeforeRequestCallbacks { get; set; }
    public List<AfterA2aRequestCallback>? AfterRequestCallbacks { get; set; }
}

public sealed class RemoteA2aAgent : BaseAgent
{
    private readonly RemoteA2aAgentConfig _config;
    private bool _isInitialized;
    private AgentCard? _card;
    private A2aClient? _client;

    public RemoteA2aAgent(RemoteA2aAgentConfig config) : base(config)
    {
        _config = config;
        if (_config.AgentCard == null && _config.AgentCardSource == null && _config.Client == null)
            throw new ArgumentException("Either AgentCard, AgentCardSource, or Client must be provided.");
    }

    private async global::System.Threading.Tasks.Task InitAsync()
    {
        if (_isInitialized) return;

        if (_config.Client != null)
            _client = _config.Client;

        if (_config.AgentCard != null)
            _card = _config.AgentCard;
        else if (!string.IsNullOrWhiteSpace(_config.AgentCardSource))
            _card = await AgentCardBuilder.ResolveAgentCardAsync(_config.AgentCardSource!);

        if (_client == null && _card != null)
        {
            if (_config.ClientFactory != null)
                _client = _config.ClientFactory(_card);
            else
                _client = new A2aClient(_card.Url, _card.PreferredTransport);
        }

        _isInitialized = true;
    }

    protected override async IAsyncEnumerable<Event> RunAsyncImpl(
        InvocationContext context,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await InitAsync();

        var events = context.Session.Events;
        if (events.Count == 0)
            throw new InvalidOperationException("No events in session to send.");

        var userFnCall = A2aRemoteAgentUtils.GetUserFunctionCallAt(context.Session, events.Count - 1);
        List<A2aPart> parts;
        string? taskId = null;
        string? contextId = null;

        if (userFnCall != null)
        {
            var evt = userFnCall.Response;
            parts = PartConverterUtils.ToA2aParts(evt.Content?.Parts, evt.LongRunningToolIds);
            taskId = userFnCall.TaskId;
            contextId = userFnCall.ContextId;
        }
        else
        {
            var missing = A2aRemoteAgentUtils.ToMissingRemoteSessionParts(context, context.Session);
            parts = missing.parts;
            contextId = missing.contextId;
        }

        var msg = new Message
        {
            Kind = "message",
            MessageId = Guid.NewGuid().ToString(),
            Role = MessageRole.User,
            Parts = parts,
            Metadata = MetadataConverterUtils.GetA2ASessionMetadata(
                context.Session.AppName,
                context.Session.UserId,
                context.Session.Id),
            TaskId = taskId,
            ContextId = contextId,
        };

        var parameters = new MessageSendParams
        {
            Message = msg,
            Configuration = _config.MessageSendConfig,
        };

        var processor = new A2aRemoteAgentRunProcessor(parameters);

        if (_config.BeforeRequestCallbacks != null)
        {
            foreach (var callback in _config.BeforeRequestCallbacks)
                await callback(context, parameters);
        }

        var useStreaming = _card?.Capabilities.Streaming != false;
        if (useStreaming)
        {
            await foreach (var chunk in _client!.SendMessageStreamAsync(parameters, cancellationToken))
            {
                if (_config.AfterRequestCallbacks != null)
                {
                    foreach (var callback in _config.AfterRequestCallbacks)
                        await callback(context, chunk);
                }

                var adkEvent = EventConverterUtils.ToAdkEvent(chunk, context.InvocationId, Name);
                if (adkEvent == null) continue;
                processor.UpdateCustomMetadata(adkEvent, chunk);
                var eventsToEmit = processor.AggregatePartial(context, chunk, adkEvent);
                foreach (var ev in eventsToEmit)
                    yield return ev;
            }
        }
        else
        {
            var result = await _client!.SendMessageAsync(parameters, cancellationToken);
            if (_config.AfterRequestCallbacks != null)
            {
                foreach (var callback in _config.AfterRequestCallbacks)
                    await callback(context, result);
            }
            var adkEvent = EventConverterUtils.ToAdkEvent(result, context.InvocationId, Name);
            if (adkEvent != null)
            {
                processor.UpdateCustomMetadata(adkEvent, result);
                yield return adkEvent;
            }
        }
    }
}

