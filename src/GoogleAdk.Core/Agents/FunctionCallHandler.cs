// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Abstractions.Tools;
using GoogleAdk.Core.Tools;

namespace GoogleAdk.Core.Agents;

/// <summary>
/// Handles function call execution with proper plugin integration,
/// before/after callbacks, error handling, and long-running tool support.
/// </summary>
public static class FunctionCallHandler
{
    private const string FunctionCallIdPrefix = "adk-";
    public const string RequestEucFunctionCallName = "adk_request_credential";
    public const string RequestConfirmationFunctionCallName = "adk_request_confirmation";

    /// <summary>
    /// Generates a unique client-side function call ID.
    /// </summary>
    public static string GenerateClientFunctionCallId()
        => $"{FunctionCallIdPrefix}{Guid.NewGuid()}";

    /// <summary>
    /// Populates client-side function call IDs for any function calls missing an ID.
    /// </summary>
    public static void PopulateClientFunctionCallId(Event modelResponseEvent)
    {
        foreach (var fc in modelResponseEvent.GetFunctionCalls())
        {
            if (string.IsNullOrEmpty(fc.Id))
                fc.Id = GenerateClientFunctionCallId();
        }
    }

    /// <summary>
    /// Returns the set of function call IDs that correspond to long-running tools.
    /// </summary>
    public static HashSet<string> GetLongRunningFunctionCalls(
        List<FunctionCall> functionCalls,
        Dictionary<string, IBaseTool> toolsDict)
    {
        var result = new HashSet<string>();
        foreach (var fc in functionCalls)
        {
            if (fc.Name != null
                && toolsDict.TryGetValue(fc.Name, out var tool)
                && tool.IsLongRunning
                && fc.Id != null)
            {
                result.Add(fc.Id);
            }
        }
        return result;
    }

    /// <summary>
    /// Handles all function calls from a model response event, executing each tool
    /// with proper plugin and callback integration.
    /// </summary>
    public static async Task<Event?> HandleFunctionCallsAsync(
        InvocationContext invocationContext,
        Event functionCallEvent,
        Dictionary<string, IBaseTool> toolsDict,
        IReadOnlyList<BeforeToolCallback> beforeToolCallbacks,
        IReadOnlyList<AfterToolCallback> afterToolCallbacks)
    {
        var functionCalls = functionCallEvent.GetFunctionCalls();
        var responseEvents = new List<Event>();

        foreach (var functionCall in functionCalls)
        {
            if (functionCall.Name == null || !toolsDict.TryGetValue(functionCall.Name, out var toolRef))
            {
                responseEvents.Add(BuildErrorResponseEvent(
                    invocationContext, functionCall,
                    $"Function {functionCall.Name} is not found in the toolsDict."));
                continue;
            }

            if (toolRef is not BaseTool tool)
            {
                responseEvents.Add(BuildErrorResponseEvent(
                    invocationContext, functionCall, "Tool does not support execution."));
                continue;
            }

            var args = functionCall.Args ?? new Dictionary<string, object?>();
            var toolContext = new AgentContext(invocationContext, functionCallId: functionCall.Id);

            // Step 1: Plugin before_tool_callback
            Dictionary<string, object?>? functionResponse = null;
            if (invocationContext.PluginManager != null)
            {
                functionResponse = await invocationContext.PluginManager
                    .RunBeforeToolCallbackAsync(tool, args, toolContext);
            }

            // Step 2: Canonical before_tool_callbacks
            if (functionResponse == null)
            {
                foreach (var callback in beforeToolCallbacks)
                {
                    functionResponse = await callback(tool, args, toolContext);
                    if (functionResponse != null) break;
                }
            }

            // Step 3: Execute the tool
            string? errorMessage = null;
            if (functionResponse == null)
            {
                try
                {
                    var rawResult = await tool.RunAsync(args, toolContext);
                    functionResponse = rawResult as Dictionary<string, object?>
                        ?? new Dictionary<string, object?> { ["result"] = rawResult };
                }
                catch (Exception ex)
                {
                    // Try plugin error callback
                    if (invocationContext.PluginManager != null)
                    {
                        // Plugin could handle the error
                    }
                    errorMessage = ex.Message;
                }
            }

            // Step 4: Plugin after_tool_callback
            Dictionary<string, object?>? alteredResponse = null;
            if (functionResponse != null && invocationContext.PluginManager != null)
            {
                alteredResponse = await invocationContext.PluginManager
                    .RunAfterToolCallbackAsync(tool, args, toolContext, functionResponse);
            }

            // Step 5: Canonical after_tool_callbacks
            if (alteredResponse == null && functionResponse != null)
            {
                foreach (var callback in afterToolCallbacks)
                {
                    alteredResponse = await callback(tool, args, toolContext, functionResponse);
                    if (alteredResponse != null) break;
                }
            }

            if (alteredResponse != null)
                functionResponse = alteredResponse;

            // Allow long-running tools to return null
            if (tool.IsLongRunning && functionResponse == null)
                continue;

            if (errorMessage != null)
                functionResponse = new Dictionary<string, object?> { ["error"] = errorMessage };
            else if (functionResponse == null)
                functionResponse = new Dictionary<string, object?> { ["result"] = null };

            responseEvents.Add(BuildFunctionResponseEvent(
                invocationContext, functionCall, functionResponse, toolContext.EventActions));
        }

        if (responseEvents.Count == 0)
            return null;

        return MergeParallelFunctionResponseEvents(responseEvents);
    }

    /// <summary>
    /// Generates an authentication event from requested auth configs.
    /// </summary>
    public static Event? GenerateAuthEvent(InvocationContext invocationContext, Event functionResponseEvent)
    {
        if (functionResponseEvent.Actions.RequestedAuthConfigs.Count == 0)
            return null;

        var parts = new List<Part>();
        var longRunningToolIds = new List<string>();

        foreach (var (functionCallId, authConfig) in functionResponseEvent.Actions.RequestedAuthConfigs)
        {
            var id = GenerateClientFunctionCallId();
            longRunningToolIds.Add(id);
            parts.Add(new Part
            {
                FunctionCall = new FunctionCall
                {
                    Name = RequestEucFunctionCallName,
                    Args = new Dictionary<string, object?>
                    {
                        ["function_call_id"] = functionCallId,
                        ["auth_config"] = authConfig,
                    },
                    Id = id,
                }
            });
        }

        return Event.Create(e =>
        {
            e.InvocationId = invocationContext.InvocationId;
            e.Author = invocationContext.Agent.Name;
            e.Branch = invocationContext.Branch;
            e.Content = new Content
            {
                Parts = parts,
                Role = functionResponseEvent.Content!.Role,
            };
            e.LongRunningToolIds = longRunningToolIds;
        });
    }

    /// <summary>
    /// Generates a request confirmation event from tool confirmations.
    /// </summary>
    public static Event? GenerateRequestConfirmationEvent(
        InvocationContext invocationContext,
        Event functionCallEvent,
        Event functionResponseEvent)
    {
        if (functionResponseEvent.Actions.RequestedToolConfirmations.Count == 0)
            return null;

        var parts = new List<Part>();
        var longRunningToolIds = new List<string>();
        var functionCalls = functionCallEvent.GetFunctionCalls();

        foreach (var (functionCallId, toolConfirmation) in functionResponseEvent.Actions.RequestedToolConfirmations)
        {
            var originalFc = functionCalls.FirstOrDefault(fc => fc.Id == functionCallId);
            if (originalFc == null) continue;

            var id = GenerateClientFunctionCallId();
            longRunningToolIds.Add(id);
            parts.Add(new Part
            {
                FunctionCall = new FunctionCall
                {
                    Name = RequestConfirmationFunctionCallName,
                    Args = new Dictionary<string, object?>
                    {
                        ["originalFunctionCall"] = originalFc,
                        ["toolConfirmation"] = toolConfirmation,
                    },
                    Id = id,
                }
            });
        }

        if (parts.Count == 0) return null;

        return Event.Create(e =>
        {
            e.InvocationId = invocationContext.InvocationId;
            e.Author = invocationContext.Agent.Name;
            e.Branch = invocationContext.Branch;
            e.Content = new Content
            {
                Parts = parts,
                Role = functionResponseEvent.Content?.Role,
            };
            e.Actions = functionResponseEvent.Actions;
            e.LongRunningToolIds = longRunningToolIds;
        });
    }

    private static Event BuildFunctionResponseEvent(
        InvocationContext invocationContext,
        FunctionCall functionCall,
        Dictionary<string, object?> response,
        EventActions actions)
    {
        return Event.Create(e =>
        {
            e.InvocationId = invocationContext.InvocationId;
            e.Author = invocationContext.Agent.Name;
            e.Branch = invocationContext.Branch;
            e.Content = new Content
            {
                Role = "user",
                Parts = new List<Part>
                {
                    new()
                    {
                        FunctionResponse = new FunctionResponse
                        {
                            Id = functionCall.Id,
                            Name = functionCall.Name ?? "unknown",
                            Response = response,
                        }
                    }
                }
            };
            e.Actions = actions;
        });
    }

    private static Event BuildErrorResponseEvent(
        InvocationContext invocationContext,
        FunctionCall functionCall,
        string error)
    {
        return BuildFunctionResponseEvent(
            invocationContext, functionCall,
            new Dictionary<string, object?> { ["error"] = error },
            new EventActions());
    }

    private static Event MergeParallelFunctionResponseEvents(List<Event> events)
    {
        if (events.Count == 1) return events[0];

        var mergedParts = new List<Part>();
        var mergedActions = new EventActions();

        foreach (var evt in events)
        {
            if (evt.Content?.Parts != null)
                mergedParts.AddRange(evt.Content.Parts);

            // Merge state deltas
            foreach (var (k, v) in evt.Actions.StateDelta)
                mergedActions.StateDelta[k] = v;

            // Merge artifact deltas
            foreach (var (k, v) in evt.Actions.ArtifactDelta)
                mergedActions.ArtifactDelta[k] = v;

            // Merge auth configs
            foreach (var (k, v) in evt.Actions.RequestedAuthConfigs)
                mergedActions.RequestedAuthConfigs[k] = v;

            // Merge tool confirmations
            foreach (var (k, v) in evt.Actions.RequestedToolConfirmations)
                mergedActions.RequestedToolConfirmations[k] = v;

            if (evt.Actions.TransferToAgent != null)
                mergedActions.TransferToAgent = evt.Actions.TransferToAgent;
            if (evt.Actions.Escalate == true)
                mergedActions.Escalate = true;
        }

        return Event.Create(e =>
        {
            e.InvocationId = events[0].InvocationId;
            e.Author = events[0].Author;
            e.Branch = events[0].Branch;
            e.Content = new Content { Role = "user", Parts = mergedParts };
            e.Actions = mergedActions;
        });
    }
}
