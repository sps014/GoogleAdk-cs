// Copyright 2026 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Abstractions.Sessions;

namespace GoogleAdk.Core.A2a;

public sealed class ExecutorContext
{
    public required string UserId { get; init; }
    public required string SessionId { get; init; }
    public required string AppName { get; init; }
    public required Dictionary<string, object?> ReadonlyState { get; init; }
    public required List<Event> Events { get; init; }
    public required Content UserContent { get; init; }
    public required MessageSendParams Request { get; init; }
    public required string TaskId { get; init; }
    public required string ContextId { get; init; }
}

public static class ExecutorContextFactory
{
    public static ExecutorContext Create(Session session, Content userContent, MessageSendParams request, string taskId, string contextId)
    {
        return new ExecutorContext
        {
            UserId = session.UserId,
            SessionId = session.Id,
            AppName = session.AppName,
            ReadonlyState = session.State,
            Events = session.Events,
            UserContent = userContent,
            Request = request,
            TaskId = taskId,
            ContextId = contextId,
        };
    }
}

