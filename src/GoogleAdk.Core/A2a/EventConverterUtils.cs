// Copyright 2026 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;

namespace GoogleAdk.Core.A2a;

public static class EventConverterUtils
{
    public static Message ToA2aMessage(
        Event adkEvent,
        string appName,
        string userId,
        string sessionId)
    {
        return new Message
        {
            Kind = "message",
            MessageId = Guid.NewGuid().ToString(),
            Role = adkEvent.Author == MessageRole.User ? MessageRole.User : MessageRole.Agent,
            Parts = PartConverterUtils.ToA2aParts(adkEvent.Content?.Parts, adkEvent.LongRunningToolIds),
            Metadata = MetadataConverterUtils.GetA2AEventMetadata(adkEvent, appName, userId, sessionId),
        };
    }

    public static Event? ToAdkEvent(IA2aEvent a2aEvent, string invocationId, string agentName)
    {
        return a2aEvent switch
        {
            Message msg => MessageToAdkEvent(msg, invocationId, agentName),
            Task task => TaskToAdkEvent(task, invocationId, agentName),
            TaskArtifactUpdateEvent artifact => ArtifactUpdateToAdkEvent(artifact, invocationId, agentName),
            TaskStatusUpdateEvent status => status.Final
                ? FinalTaskStatusUpdateToAdkEvent(status, invocationId, agentName)
                : TaskStatusUpdateToAdkEvent(status, invocationId, agentName),
            _ => null,
        };
    }

    private static Event MessageToAdkEvent(
        Message msg,
        string invocationId,
        string agentName,
        TaskStatusUpdateEvent? parent = null)
    {
        var parts = PartConverterUtils.ToParts(msg.Parts);
        IA2aEvent source = parent == null ? msg : parent;
        var evt = CreateAdkEventFromMetadata(source);
        evt.InvocationId = invocationId;
        evt.Author = msg.Role == MessageRole.User ? MessageRole.User : agentName;
        evt.Content = new Content
        {
            Role = msg.Role == MessageRole.User ? "user" : "model",
            Parts = parts,
        };
        evt.TurnComplete = true;
        evt.Partial = false;
        return evt;
    }

    private static Event? ArtifactUpdateToAdkEvent(
        TaskArtifactUpdateEvent a2aEvent,
        string invocationId,
        string agentName)
    {
        var partsToConvert = a2aEvent.Artifact?.Parts ?? new List<A2aPart>();
        if (partsToConvert.Count == 0)
            return null;

        var partial = GetEventMetadataFlag(a2aEvent, A2aMetadataKeys.Partial) ||
                      (a2aEvent.Append ?? false) ||
                      !(a2aEvent.LastChunk ?? true);

        var evt = CreateAdkEventFromMetadata(a2aEvent);
        evt.InvocationId = invocationId;
        evt.Author = agentName;
        evt.Content = new Content
        {
            Role = "model",
            Parts = PartConverterUtils.ToParts(partsToConvert),
        };
        evt.LongRunningToolIds = GetLongRunningToolIds(partsToConvert);
        evt.Partial = partial;
        return evt;
    }

    private static Event? FinalTaskStatusUpdateToAdkEvent(
        TaskStatusUpdateEvent a2aEvent,
        string invocationId,
        string agentName)
    {
        var partsToConvert = a2aEvent.Status.Message?.Parts ?? new List<A2aPart>();
        if (partsToConvert.Count == 0)
            return null;

        var isFailed = A2aEventHelpers.IsFailedTaskStatusUpdateEvent(a2aEvent);
        var parts = PartConverterUtils.ToParts(partsToConvert);

        var evt = CreateAdkEventFromMetadata(a2aEvent);
        evt.InvocationId = invocationId;
        evt.Author = agentName;
        evt.ErrorMessage = isFailed ? A2aEventHelpers.GetFailedTaskStatusUpdateEventError(a2aEvent) : null;
        evt.Content = !isFailed && parts.Count > 0
            ? new Content { Role = "model", Parts = parts }
            : null;
        evt.LongRunningToolIds = GetLongRunningToolIds(partsToConvert);
        evt.TurnComplete = true;
        return evt;
    }

    private static Event? TaskStatusUpdateToAdkEvent(
        TaskStatusUpdateEvent a2aEvent,
        string invocationId,
        string agentName)
    {
        var msg = a2aEvent.Status.Message;
        if (msg == null) return null;
        var parts = PartConverterUtils.ToParts(msg.Parts);

        var evt = CreateAdkEventFromMetadata(a2aEvent);
        evt.InvocationId = invocationId;
        evt.Author = agentName;
        evt.Content = new Content { Role = "model", Parts = parts };
        evt.TurnComplete = false;
        evt.Partial = true;
        return evt;
    }

    private static Event? TaskToAdkEvent(
        Task task,
        string invocationId,
        string agentName)
    {
        var parts = new List<Part>();
        var longRunning = new List<string>();

        if (task.Artifacts != null)
        {
            foreach (var artifact in task.Artifacts)
            {
                if (artifact.Parts?.Count > 0)
                {
                    parts.AddRange(PartConverterUtils.ToParts(artifact.Parts));
                    longRunning.AddRange(GetLongRunningToolIds(artifact.Parts));
                }
            }
        }

        if (task.Status.Message != null)
        {
            var statusParts = task.Status.Message.Parts;
            parts.AddRange(PartConverterUtils.ToParts(statusParts));
            longRunning.AddRange(GetLongRunningToolIds(statusParts));
        }

        var isTerminal = A2aEventHelpers.IsTerminalTaskStatusUpdateEvent(task) ||
                         A2aEventHelpers.IsInputRequiredTaskStatusUpdateEvent(task);
        var isFailed = A2aEventHelpers.IsFailedTaskStatusUpdateEvent(task);

        if (parts.Count == 0 && !isTerminal) return null;

        var evt = CreateAdkEventFromMetadata(task);
        evt.InvocationId = invocationId;
        evt.Author = agentName;
        evt.Content = isFailed ? null : new Content { Role = "model", Parts = parts };
        evt.ErrorMessage = isFailed ? A2aEventHelpers.GetFailedTaskStatusUpdateEventError(new TaskStatusUpdateEvent
        {
            Status = task.Status,
            TaskId = task.Id,
            ContextId = task.ContextId,
        }) : null;
        evt.LongRunningToolIds = longRunning;
        evt.TurnComplete = isTerminal;
        return evt;
    }

    private static Event CreateAdkEventFromMetadata(IA2aEvent a2aEvent)
    {
        var metadata = a2aEvent.Metadata ?? new Dictionary<string, object?>();
        var evt = Event.Create(e =>
        {
            e.Branch = metadata.TryGetValue(A2aMetadataKeys.Branch, out var branch) ? branch as string : null;
            e.Author = metadata.TryGetValue(A2aMetadataKeys.Author, out var author) ? author as string : null;
            e.Partial = metadata.TryGetValue(A2aMetadataKeys.Partial, out var partial) ? partial as bool? : null;
            e.ErrorCode = metadata.TryGetValue(A2aMetadataKeys.ErrorCode, out var errorCode) ? errorCode as string : null;
            e.ErrorMessage = metadata.TryGetValue(A2aMetadataKeys.ErrorMessage, out var errorMessage) ? errorMessage as string : null;
            e.CitationMetadata = metadata.TryGetValue(A2aMetadataKeys.CitationMetadata, out var citation) ? citation as CitationMetadata : null;
            e.GroundingMetadata = metadata.TryGetValue(A2aMetadataKeys.GroundingMetadata, out var grounding) ? grounding as GroundingMetadata : null;
            e.UsageMetadata = metadata.TryGetValue(A2aMetadataKeys.UsageMetadata, out var usage) ? usage as UsageMetadata : null;
            e.CustomMetadata = metadata.TryGetValue(A2aMetadataKeys.CustomMetadata, out var custom) ? custom as Dictionary<string, object?> : null;
            e.Actions = EventActions.Create(actions =>
            {
                if (metadata.TryGetValue(A2aMetadataKeys.Escalate, out var esc)) actions.Escalate = esc as bool?;
                if (metadata.TryGetValue(A2aMetadataKeys.TransferToAgent, out var transfer)) actions.TransferToAgent = transfer as string;
            });
        });
        return evt;
    }

    private static List<string> GetLongRunningToolIds(List<A2aPart> parts)
    {
        var ids = new List<string>();
        foreach (var part in parts)
        {
            if (part.Metadata != null &&
                part.Metadata.TryGetValue(A2aMetadataKeys.IsLongRunning, out var val) &&
                val is bool b && b)
            {
                var p = PartConverterUtils.ToPart(part);
                if (p.FunctionCall?.Id != null) ids.Add(p.FunctionCall.Id);
            }
        }
        return ids;
    }

    private static bool GetEventMetadataFlag(IA2aEvent evt, string key)
    {
        return evt.Metadata != null &&
               evt.Metadata.TryGetValue(key, out var value) &&
               value is bool b && b;
    }
}

