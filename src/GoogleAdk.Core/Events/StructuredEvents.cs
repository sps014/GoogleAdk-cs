// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;

namespace GoogleAdk.Core.Events;

/// <summary>
/// The types of events that can be parsed from a raw Event.
/// </summary>
public enum EventType
{
    Thought,
    Content,
    ToolCall,
    ToolResult,
    CallCode,
    CodeResult,
    Error,
    Activity,
    ToolConfirmation,
    Finished
}

/// <summary>
/// A standard structured event parsed from the raw Event stream.
/// </summary>
public abstract class StructuredEvent
{
    public EventType Type { get; }
    protected StructuredEvent(EventType type) => Type = type;
}

public class ThoughtEvent : StructuredEvent
{
    public string Text { get; }
    public ThoughtEvent(string text) : base(EventType.Thought) => Text = text;
}

public class ContentEvent : StructuredEvent
{
    public string Text { get; }
    public ContentEvent(string text) : base(EventType.Content) => Text = text;
}

public class ToolCallEvent : StructuredEvent
{
    public FunctionCall Call { get; }
    public ToolCallEvent(FunctionCall call) : base(EventType.ToolCall) => Call = call;
}

public class ToolResultEvent : StructuredEvent
{
    public FunctionResponse Result { get; }
    public ToolResultEvent(FunctionResponse result) : base(EventType.ToolResult) => Result = result;
}

public class CallCodeEvent : StructuredEvent
{
    public string Code { get; }
    public CallCodeEvent(string code) : base(EventType.CallCode) => Code = code;
}

public class CodeResultEvent : StructuredEvent
{
    public CodeExecutionResult Result { get; }
    public CodeResultEvent(CodeExecutionResult result) : base(EventType.CodeResult) => Result = result;
}

public class ErrorEvent : StructuredEvent
{
    public string ErrorMessage { get; }
    public ErrorEvent(string errorMessage) : base(EventType.Error) => ErrorMessage = errorMessage;
}

public class ActivityEvent : StructuredEvent
{
    public string Kind { get; }
    public Dictionary<string, object?> Detail { get; }
    public ActivityEvent(string kind, Dictionary<string, object?> detail) : base(EventType.Activity)
    {
        Kind = kind;
        Detail = detail;
    }
}

public class ToolConfirmationEvent : StructuredEvent
{
    public Dictionary<string, Abstractions.Tools.ToolConfirmation> Confirmations { get; }
    public ToolConfirmationEvent(Dictionary<string, Abstractions.Tools.ToolConfirmation> confirmations)
        : base(EventType.ToolConfirmation) => Confirmations = confirmations;
}

public class FinishedEvent : StructuredEvent
{
    public object? Output { get; }
    public FinishedEvent(object? output = null) : base(EventType.Finished) => Output = output;
}

/// <summary>
/// Converts internal Events to structured events for easier consumption.
/// </summary>
public static class StructuredEventConverter
{
    /// <summary>
    /// Converts an internal Event to a list of structured events.
    /// </summary>
    public static List<StructuredEvent> ToStructuredEvents(Event evt)
    {
        var result = new List<StructuredEvent>();

        if (!string.IsNullOrEmpty(evt.ErrorCode))
        {
            result.Add(new ErrorEvent(evt.ErrorMessage ?? evt.ErrorCode!));
            return result;
        }

        if (evt.Content?.Parts != null)
        {
            foreach (var part in evt.Content.Parts)
            {
                if (part.FunctionCall != null)
                    result.Add(new ToolCallEvent(part.FunctionCall));
                else if (part.FunctionResponse != null)
                    result.Add(new ToolResultEvent(part.FunctionResponse));
                else if (part.CodeExecutionResult != null)
                    result.Add(new CodeResultEvent(part.CodeExecutionResult));
                else if (part.Text != null)
                    result.Add(new ContentEvent(part.Text));
            }
        }

        if (evt.Actions.RequestedToolConfirmations.Count > 0)
            result.Add(new ToolConfirmationEvent(evt.Actions.RequestedToolConfirmations));

        if (evt.IsFinalResponse())
            result.Add(new FinishedEvent());

        return result;
    }
}
