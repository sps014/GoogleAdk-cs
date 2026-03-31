// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Events;

namespace GoogleAdk.Core.Tests;

public class StructuredEventTests
{
    [Fact]
    public void ThoughtEvent_HasCorrectType()
    {
        var evt = new ThoughtEvent("I should search first.");
        Assert.Equal(EventType.Thought, evt.Type);
        Assert.Equal("I should search first.", evt.Text);
    }

    [Fact]
    public void ContentEvent_HasCorrectType()
    {
        var evt = new ContentEvent("Here is the answer.");
        Assert.Equal(EventType.Content, evt.Type);
        Assert.Equal("Here is the answer.", evt.Text);
    }

    [Fact]
    public void ToolCallEvent_HasCorrectType()
    {
        var call = new FunctionCall { Name = "search", Args = new Dictionary<string, object?> { ["q"] = "test" } };
        var evt = new ToolCallEvent(call);
        Assert.Equal(EventType.ToolCall, evt.Type);
        Assert.Equal("search", evt.Call.Name);
    }

    [Fact]
    public void ToolResultEvent_HasCorrectType()
    {
        var response = new FunctionResponse
        {
            Name = "search",
            Response = new Dictionary<string, object?> { ["results"] = 5 }
        };
        var evt = new ToolResultEvent(response);
        Assert.Equal(EventType.ToolResult, evt.Type);
        Assert.Equal("search", evt.Result.Name);
    }

    [Fact]
    public void CallCodeEvent_HasCorrectType()
    {
        var evt = new CallCodeEvent("print('hello')");
        Assert.Equal(EventType.CallCode, evt.Type);
        Assert.Equal("print('hello')", evt.Code);
    }

    [Fact]
    public void CodeResultEvent_HasCorrectType()
    {
        var result = new CodeExecutionResult { Output = "hello", Outcome = "OUTCOME_OK" };
        var evt = new CodeResultEvent(result);
        Assert.Equal(EventType.CodeResult, evt.Type);
        Assert.Equal("hello", evt.Result.Output);
    }

    [Fact]
    public void ErrorEvent_HasCorrectType()
    {
        var evt = new ErrorEvent("Something went wrong");
        Assert.Equal(EventType.Error, evt.Type);
        Assert.Equal("Something went wrong", evt.ErrorMessage);
    }

    [Fact]
    public void ActivityEvent_HasCorrectType()
    {
        var detail = new Dictionary<string, object?> { ["progress"] = 50 };
        var evt = new ActivityEvent("loading", detail);
        Assert.Equal(EventType.Activity, evt.Type);
        Assert.Equal("loading", evt.Kind);
        Assert.Equal(50, evt.Detail["progress"]);
    }

    [Fact]
    public void ToolConfirmationEvent_HasCorrectType()
    {
        var confirmations = new Dictionary<string, Abstractions.Tools.ToolConfirmation>
        {
            ["call-1"] = new() { FunctionCallId = "call-1" }
        };
        var evt = new ToolConfirmationEvent(confirmations);
        Assert.Equal(EventType.ToolConfirmation, evt.Type);
        Assert.Single(evt.Confirmations);
    }

    [Fact]
    public void FinishedEvent_HasCorrectType()
    {
        var evt = new FinishedEvent("done");
        Assert.Equal(EventType.Finished, evt.Type);
        Assert.Equal("done", evt.Output);
    }

    [Fact]
    public void FinishedEvent_NullOutput()
    {
        var evt = new FinishedEvent();
        Assert.Equal(EventType.Finished, evt.Type);
        Assert.Null(evt.Output);
    }

    [Fact]
    public void EventType_HasAllExpectedValues()
    {
        var values = Enum.GetValues<EventType>();
        Assert.Equal(10, values.Length);
        Assert.Contains(EventType.Thought, values);
        Assert.Contains(EventType.Content, values);
        Assert.Contains(EventType.ToolCall, values);
        Assert.Contains(EventType.ToolResult, values);
        Assert.Contains(EventType.CallCode, values);
        Assert.Contains(EventType.CodeResult, values);
        Assert.Contains(EventType.Error, values);
        Assert.Contains(EventType.Activity, values);
        Assert.Contains(EventType.ToolConfirmation, values);
        Assert.Contains(EventType.Finished, values);
    }
}
