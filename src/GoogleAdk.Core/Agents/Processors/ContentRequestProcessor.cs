// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using System.Text.Json;
using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;

namespace GoogleAdk.Core.Agents.Processors;

/// <summary>
/// Populates the LLM request contents from the session event history.
/// Handles include_contents modes: 'default' (full history) or 'none' (current turn only).
/// Filters by branch, skips auth/confirmation events, and converts foreign agent events.
/// </summary>
public class ContentRequestProcessor : BaseLlmRequestProcessor
{
    public static readonly ContentRequestProcessor Instance = new();

    public override async IAsyncEnumerable<Event> RunAsync(
        InvocationContext invocationContext,
        LlmRequest llmRequest)
    {
        await Task.CompletedTask;

        if (invocationContext.Agent is not LlmAgent agent)
            yield break;

        var events = invocationContext.Session.Events;
        if (events == null || events.Count == 0)
            yield break;

        if (agent.IncludeContents == IncludeContentsMode.None)
        {
            llmRequest.Contents = GetCurrentTurnContents(events, agent.Name, invocationContext.Branch);
        }
        else
        {
            llmRequest.Contents = GetContents(events, agent.Name, invocationContext.Branch);
        }

        // Ensure the request ends with a user content so the model can respond
        llmRequest.MaybeAppendUserContent();
    }

    /// <summary>
    /// Gets full conversation history as contents for the LLM request.
    /// </summary>
    public static List<Content> GetContents(IReadOnlyList<Event> events, string agentName, string? currentBranch)
    {
        var filteredEvents = new List<Event>();

        foreach (var evt in events)
        {
            // Skip events without content
            if (evt.Content?.Role == null)
                continue;

            // Skip empty text parts
            if (evt.Content.Parts?.Count == 1 && evt.Content.Parts[0].Text == "")
                continue;

            // Skip events not in the current branch
            if (currentBranch != null && evt.Branch != null && !currentBranch.StartsWith(evt.Branch))
                continue;

            // Skip auth events
            if (IsAuthEvent(evt))
                continue;

            // Skip tool confirmation events
            if (IsToolConfirmationEvent(evt))
                continue;

            // Convert events from other agents to user messages
            if (IsEventFromAnotherAgent(agentName, evt))
                filteredEvents.Add(ConvertForeignEvent(evt));
            else
                filteredEvents.Add(evt);
        }

        // Build contents, rearranging function responses as needed
        var contents = new List<Content>();
        foreach (var evt in filteredEvents)
        {
            if (evt.Content != null)
                contents.Add(CloneContent(evt.Content));
        }
        return contents;
    }

    /// <summary>
    /// Gets contents for the current turn only (no conversation history).
    /// </summary>
    public static List<Content> GetCurrentTurnContents(IReadOnlyList<Event> events, string agentName, string? currentBranch)
    {
        // Find the latest event that starts the current turn
        for (int i = events.Count - 1; i >= 0; i--)
        {
            var evt = events[i];
            if (evt.Author == "user" || IsEventFromAnotherAgent(agentName, evt))
            {
                var slice = new List<Event>();
                for (int j = i; j < events.Count; j++)
                    slice.Add(events[j]);
                return GetContents(slice, agentName, currentBranch);
            }
        }
        return GetContents(events, agentName, currentBranch);
    }

    private static bool IsAuthEvent(Event evt)
    {
        if (evt.Content?.Parts == null) return false;
        return evt.Content.Parts.Any(p =>
            p.FunctionCall?.Name == FunctionCallHandler.RequestEucFunctionCallName ||
            p.FunctionResponse?.Name == FunctionCallHandler.RequestEucFunctionCallName);
    }

    private static bool IsToolConfirmationEvent(Event evt)
    {
        if (evt.Content?.Parts == null) return false;
        return evt.Content.Parts.Any(p =>
            p.FunctionCall?.Name == FunctionCallHandler.RequestConfirmationFunctionCallName ||
            p.FunctionResponse?.Name == FunctionCallHandler.RequestConfirmationFunctionCallName);
    }

    private static bool IsEventFromAnotherAgent(string agentName, Event evt)
        => evt.Author != null && evt.Author != "user" && evt.Author != agentName;

    private static Event ConvertForeignEvent(Event evt)
    {
        if (evt.Content?.Parts == null || evt.Content.Parts.Count == 0)
            return evt;

        // Convert ALL parts from foreign agents into text descriptions (role=user).
        // This provides context to the current agent without confusing it with
        // raw function calls/responses it never made.
        var textParts = new List<Part> { new Part { Text = "For context:" } };

        foreach (var part in evt.Content.Parts)
        {
            if (part.Text != null)
            {
                textParts.Add(new Part { Text = $"[{evt.Author}] said: {part.Text}" });
            }
            else if (part.FunctionCall != null)
            {
                var argsText = part.FunctionCall.Args != null
                    ? JsonSerializer.Serialize(part.FunctionCall.Args)
                    : "{}";
                textParts.Add(new Part
                {
                    Text = $"[{evt.Author}] called tool `{part.FunctionCall.Name}` with parameters: {argsText}"
                });
            }
            else if (part.FunctionResponse != null)
            {
                var responseText = part.FunctionResponse.Response != null
                    ? JsonSerializer.Serialize(part.FunctionResponse.Response)
                    : "{}";
                textParts.Add(new Part
                {
                    Text = $"[{evt.Author}] tool `{part.FunctionResponse.Name}` returned result: {responseText}"
                });
            }
            else
            {
                textParts.Add(part);
            }
        }

        return Event.Create(e =>
        {
            e.InvocationId = evt.InvocationId;
            e.Author = "user";
            e.Branch = evt.Branch;
            e.Content = new Content
            {
                Role = "user",
                Parts = textParts
            };
        });
    }

    private static Content CloneContent(Content original)
    {
        return new Content
        {
            Role = original.Role,
            Parts = original.Parts?.Select(p => new Part
            {
                Text = p.Text,
                FunctionCall = p.FunctionCall,
                FunctionResponse = p.FunctionResponse,
                InlineData = p.InlineData,
            }).ToList()
        };
    }
}

/// <summary>
/// Controls how conversation contents are included in LLM requests.
/// </summary>
public enum IncludeContentsMode
{
    /// <summary>Model receives relevant conversation history.</summary>
    Default,

    /// <summary>Model receives no prior history, operates solely on current instruction and input.</summary>
    None
}
