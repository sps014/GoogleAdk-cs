// Copyright 2026 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;

namespace GoogleAdk.Core.A2a;

public static class EventProcessorUtils
{
    public static TaskStatusUpdateEvent GetFinalTaskStatusUpdate(
        List<Event> adkEvents,
        ExecutorContext context)
    {
        var finalActions = EventActions.Create();
        foreach (var evt in adkEvents)
        {
            if (!string.IsNullOrWhiteSpace(evt.ErrorCode) || !string.IsNullOrWhiteSpace(evt.ErrorMessage))
            {
                return A2aEventHelpers.CreateTaskFailedEvent(
                    context.TaskId,
                    context.ContextId,
                    new Exception(evt.ErrorMessage ?? evt.ErrorCode ?? "Task failed"),
                    new Dictionary<string, object?>
                    {
                        { A2aMetadataKeys.AppName, context.AppName },
                        { A2aMetadataKeys.UserId, context.UserId },
                        { A2aMetadataKeys.SessionId, context.SessionId },
                        { A2aMetadataKeys.Escalate, finalActions.Escalate },
                        { A2aMetadataKeys.TransferToAgent, finalActions.TransferToAgent },
                    });
            }

            if (evt.Actions?.Escalate == true) finalActions.Escalate = true;
            if (!string.IsNullOrWhiteSpace(evt.Actions?.TransferToAgent))
                finalActions.TransferToAgent = evt.Actions.TransferToAgent;
        }

        var inputRequiredEvent = ScanForInputRequiredEvents(adkEvents, context);
        if (inputRequiredEvent != null)
        {
            inputRequiredEvent.Metadata ??= new Dictionary<string, object?>();
            inputRequiredEvent.Metadata[A2aMetadataKeys.Escalate] = finalActions.Escalate;
            if (!string.IsNullOrWhiteSpace(finalActions.TransferToAgent))
                inputRequiredEvent.Metadata[A2aMetadataKeys.TransferToAgent] = finalActions.TransferToAgent;
            return inputRequiredEvent;
        }

        var completed = A2aEventHelpers.CreateTaskCompletedEvent(
            context.TaskId,
            context.ContextId,
            MetadataConverterUtils.GetA2ASessionMetadata(context.AppName, context.UserId, context.SessionId));
        completed.Metadata ??= new Dictionary<string, object?>();
        completed.Metadata[A2aMetadataKeys.Escalate] = finalActions.Escalate;
        if (!string.IsNullOrWhiteSpace(finalActions.TransferToAgent))
            completed.Metadata[A2aMetadataKeys.TransferToAgent] = finalActions.TransferToAgent;
        return completed;
    }

    private static TaskStatusUpdateEvent? ScanForInputRequiredEvents(
        List<Event> adkEvents,
        ExecutorContext context)
    {
        var inputRequiredParts = new List<Part>();
        var inputRequiredFunctionCallIds = new HashSet<string>();

        foreach (var adkEvent in adkEvents)
        {
            if (adkEvent.Content?.Parts == null) continue;
            foreach (var part in adkEvent.Content.Parts)
            {
                var longRunningId = GetLongRunningFunctionCallId(
                    part,
                    adkEvent.LongRunningToolIds,
                    inputRequiredParts);
                if (string.IsNullOrWhiteSpace(longRunningId)) continue;
                if (inputRequiredFunctionCallIds.Contains(longRunningId)) continue;

                inputRequiredParts.Add(part);
                inputRequiredFunctionCallIds.Add(longRunningId);
            }
        }

        if (inputRequiredParts.Count == 0) return null;

        return A2aEventHelpers.CreateTaskInputRequiredEvent(
            context.TaskId,
            context.ContextId,
            PartConverterUtils.ToA2aParts(inputRequiredParts, inputRequiredFunctionCallIds.ToList()),
            MetadataConverterUtils.GetA2ASessionMetadata(context.AppName, context.UserId, context.SessionId));
    }

    private static string? GetLongRunningFunctionCallId(
        Part part,
        List<string>? longRunningToolIds,
        List<Part> inputRequiredParts)
    {
        longRunningToolIds ??= new List<string>();
        var functionCallId = part.FunctionCall?.Id;
        var functionResponseId = part.FunctionResponse?.Id;
        if (functionCallId == null && functionResponseId == null) return null;

        if (functionCallId != null && longRunningToolIds.Contains(functionCallId))
            return functionCallId;

        if (functionResponseId != null && longRunningToolIds.Contains(functionResponseId))
            return functionResponseId;

        foreach (var existing in inputRequiredParts)
        {
            if (existing.FunctionCall?.Id == functionResponseId) return functionResponseId;
        }

        return null;
    }

    public static TaskStatusUpdateEvent? GetTaskInputRequiredEvent(Task task, Content userContent)
    {
        if (!A2aEventHelpers.IsInputRequiredTaskStatusUpdateEvent(task) ||
            task.Status.Message == null)
            return null;

        var statusMsg = task.Status.Message;
        var taskParts = PartConverterUtils.ToParts(statusMsg.Parts);
        foreach (var taskPart in taskParts)
        {
            var functionCallId = taskPart.FunctionCall?.Id;
            if (string.IsNullOrWhiteSpace(functionCallId)) continue;

            var hasMatchingResponse = (userContent.Parts ?? new List<Part>())
                .Any(p => p.FunctionResponse?.Id == functionCallId);
            if (!hasMatchingResponse)
            {
                var a2aParts = statusMsg.Parts
                    .Where(p => p.Metadata == null || !p.Metadata.ContainsKey("validation_error"))
                    .ToList();
                a2aParts.Add(new A2aPart
                {
                    Kind = "text",
                    Text = $"No input provided for function call id {functionCallId}",
                    Metadata = new Dictionary<string, object?> { ["validation_error"] = true },
                });
                return A2aEventHelpers.CreateInputMissingErrorEvent(
                    task.Id,
                    task.ContextId ?? string.Empty,
                    a2aParts);
            }
        }

        return null;
    }
}

