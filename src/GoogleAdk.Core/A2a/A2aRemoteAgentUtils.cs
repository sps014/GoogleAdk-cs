// Copyright 2026 Google LLC
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Abstractions.Sessions;
using GoogleAdk.Core.Agents;

namespace GoogleAdk.Core.A2a;

public sealed class UserFunctionCall
{
    public required Event Response { get; init; }
    public required string TaskId { get; init; }
    public required string ContextId { get; init; }
}

public static class A2aRemoteAgentUtils
{
    public static UserFunctionCall? GetUserFunctionCallAt(Session session, int index)
    {
        if (index < 0 || index >= session.Events.Count) return null;
        var candidate = session.Events[index];
        if (candidate.Author != MessageRole.User) return null;

        var fnCallId = GetFunctionResponseCallId(candidate);
        if (fnCallId == null) return null;

        for (var i = index - 1; i >= 0; i--)
        {
            var request = session.Events[i];
            if (!IsFunctionCallEvent(request, fnCallId)) continue;
            var metadata = request.CustomMetadata ?? new Dictionary<string, object?>();
            var taskId = metadata.TryGetValue(AdkMetadataKeys.TaskId, out var t) ? t as string : "";
            var contextId = metadata.TryGetValue(AdkMetadataKeys.ContextId, out var c) ? c as string : "";
            return new UserFunctionCall
            {
                Response = candidate,
                TaskId = taskId ?? "",
                ContextId = contextId ?? "",
            };
        }

        return null;
    }

    public static bool IsFunctionCallEvent(Event evt, string callId)
    {
        if (evt.Content?.Parts == null) return false;
        return evt.Content.Parts.Any(part => part.FunctionCall?.Id == callId);
    }

    public static string? GetFunctionResponseCallId(Event evt)
    {
        if (evt.Content?.Parts == null) return null;
        var responsePart = evt.Content.Parts.FirstOrDefault(p => p.FunctionResponse != null);
        return responsePart?.FunctionResponse?.Id;
    }

    public static (List<A2aPart> parts, string? contextId) ToMissingRemoteSessionParts(
        InvocationContext ctx,
        Session session)
    {
        var events = session.Events;
        var contextId = default(string);
        var lastRemoteResponseIndex = -1;

        for (var i = events.Count - 1; i >= 0; i--)
        {
            var evt = events[i];
            if (evt.Author == ctx.Agent.Name)
            {
                lastRemoteResponseIndex = i;
                if (evt.CustomMetadata != null && evt.CustomMetadata.TryGetValue(AdkMetadataKeys.ContextId, out var c))
                    contextId = c as string;
                break;
            }
        }

        var missingParts = new List<A2aPart>();
        for (var i = lastRemoteResponseIndex + 1; i < events.Count; i++)
        {
            var evt = events[i];
            if (evt.Author != MessageRole.User && evt.Author != ctx.Agent.Name)
                evt = PresentAsUserMessage(ctx, evt);

            if (evt.Content?.Parts == null || evt.Content.Parts.Count == 0) continue;
            var parts = PartConverterUtils.ToA2aParts(evt.Content.Parts, evt.LongRunningToolIds);
            missingParts.AddRange(parts);
        }

        return (missingParts, contextId);
    }

    public static Event PresentAsUserMessage(InvocationContext ctx, Event agentEvent)
    {
        var evt = Event.Create(e =>
        {
            e.Author = MessageRole.User;
            e.InvocationId = ctx.InvocationId;
        });

        if (agentEvent.Content?.Parts == null) return evt;

        var parts = new List<Part> { new() { Text = "For context:" } };
        foreach (var part in agentEvent.Content.Parts)
        {
            if (part.Thought == true) continue;
            if (!string.IsNullOrWhiteSpace(part.Text))
            {
                parts.Add(new Part { Text = $"[{agentEvent.Author}] said: {part.Text}" });
            }
            else if (part.FunctionCall != null)
            {
                parts.Add(new Part
                {
                    Text = $"[{agentEvent.Author}] called tool {part.FunctionCall.Name} with parameters: {JsonSerializer.Serialize(part.FunctionCall.Args)}"
                });
            }
            else if (part.FunctionResponse != null)
            {
                parts.Add(new Part
                {
                    Text = $"[{agentEvent.Author}] {part.FunctionResponse.Name} tool returned result: {JsonSerializer.Serialize(part.FunctionResponse.Response)}"
                });
            }
            else
            {
                parts.Add(part);
            }
        }

        if (parts.Count > 1)
        {
            evt.Content = new Content
            {
                Role = "user",
                Parts = parts,
            };
        }

        return evt;
    }
}

