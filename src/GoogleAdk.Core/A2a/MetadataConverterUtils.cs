// Copyright 2026 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Events;

namespace GoogleAdk.Core.A2a;

public static class A2aMetadataKeys
{
    private const string AdkPrefix = "adk_";
    public const string AppName = AdkPrefix + "app_name";
    public const string UserId = AdkPrefix + "user_id";
    public const string SessionId = AdkPrefix + "session_id";
    public const string InvocationId = AdkPrefix + "invocation_id";
    public const string Author = AdkPrefix + "author";
    public const string Branch = AdkPrefix + "branch";
    public const string DataPartType = AdkPrefix + "type";
    public const string Partial = AdkPrefix + "partial";
    public const string Escalate = AdkPrefix + "escalate";
    public const string TransferToAgent = AdkPrefix + "transfer_to_agent";
    public const string IsLongRunning = AdkPrefix + "is_long_running";
    public const string Thought = AdkPrefix + "thought";
    public const string ErrorCode = AdkPrefix + "error_code";
    public const string ErrorMessage = AdkPrefix + "error_message";
    public const string CitationMetadata = AdkPrefix + "citation_metadata";
    public const string GroundingMetadata = AdkPrefix + "grounding_metadata";
    public const string UsageMetadata = AdkPrefix + "usage_metadata";
    public const string CustomMetadata = AdkPrefix + "custom_metadata";
    public const string VideoMetadata = AdkPrefix + "video_metadata";
}

public static class AdkMetadataKeys
{
    private const string A2aPrefix = "a2a:";
    public const string TaskId = A2aPrefix + "task_id";
    public const string ContextId = A2aPrefix + "context_id";
}

public static class MetadataConverterUtils
{
    public static Dictionary<string, object?> GetAdkEventMetadata(IA2aEvent a2aEvent)
    {
        return a2aEvent switch
        {
            Task task => new Dictionary<string, object?>
            {
                [AdkMetadataKeys.TaskId] = task.Id,
                [AdkMetadataKeys.ContextId] = task.ContextId,
            },
            TaskStatusUpdateEvent status => new Dictionary<string, object?>
            {
                [AdkMetadataKeys.TaskId] = status.TaskId,
                [AdkMetadataKeys.ContextId] = status.ContextId,
            },
            TaskArtifactUpdateEvent artifact => new Dictionary<string, object?>
            {
                [AdkMetadataKeys.TaskId] = artifact.TaskId,
                [AdkMetadataKeys.ContextId] = artifact.ContextId,
            },
            Message msg => new Dictionary<string, object?>
            {
                [AdkMetadataKeys.TaskId] = msg.TaskId,
                [AdkMetadataKeys.ContextId] = msg.ContextId,
            },
            _ => new Dictionary<string, object?>(),
        };
    }

    public static Dictionary<string, object?> GetA2AEventMetadata(
        Event adkEvent,
        string appName,
        string userId,
        string sessionId)
    {
        return new Dictionary<string, object?>
        {
            [A2aMetadataKeys.Escalate] = adkEvent.Actions?.Escalate,
            [A2aMetadataKeys.TransferToAgent] = adkEvent.Actions?.TransferToAgent,
            [A2aMetadataKeys.AppName] = appName,
            [A2aMetadataKeys.UserId] = userId,
            [A2aMetadataKeys.SessionId] = sessionId,
            [A2aMetadataKeys.InvocationId] = adkEvent.InvocationId,
            [A2aMetadataKeys.Author] = adkEvent.Author,
            [A2aMetadataKeys.Branch] = adkEvent.Branch,
            [A2aMetadataKeys.ErrorCode] = adkEvent.ErrorCode,
            [A2aMetadataKeys.ErrorMessage] = adkEvent.ErrorMessage,
            [A2aMetadataKeys.CitationMetadata] = adkEvent.CitationMetadata,
            [A2aMetadataKeys.GroundingMetadata] = adkEvent.GroundingMetadata,
            [A2aMetadataKeys.UsageMetadata] = adkEvent.UsageMetadata,
            [A2aMetadataKeys.CustomMetadata] = adkEvent.CustomMetadata,
            [A2aMetadataKeys.Partial] = adkEvent.Partial,
            [A2aMetadataKeys.IsLongRunning] = (adkEvent.LongRunningToolIds ?? new List<string>()).Count > 0,
        };
    }

    public static Dictionary<string, object?> GetA2ASessionMetadata(
        string appName,
        string userId,
        string sessionId)
    {
        return new Dictionary<string, object?>
        {
            [A2aMetadataKeys.AppName] = appName,
            [A2aMetadataKeys.UserId] = userId,
            [A2aMetadataKeys.SessionId] = sessionId,
        };
    }

    public static Dictionary<string, object?> GetA2AEventMetadataFromActions(EventActions actions)
    {
        return new Dictionary<string, object?>
        {
            [A2aMetadataKeys.Escalate] = actions.Escalate,
            [A2aMetadataKeys.TransferToAgent] = actions.TransferToAgent,
        };
    }
}

