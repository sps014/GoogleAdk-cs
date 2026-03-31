// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using GoogleAdk.Core.Telemetry;

namespace GoogleAdk.Core.Tests;

[Collection("Telemetry")]
public class TelemetryTests
{
    [Fact]
    public void ActivitySource_HasCorrectName()
    {
        Assert.Equal("gcp.vertex.agent", AdkTracing.ActivitySource.Name);
    }

    [Fact]
    public void ActivitySource_HasVersion()
    {
        Assert.NotNull(AdkTracing.ActivitySource.Version);
    }

    [Fact]
    public void CreateAdkActivityListener_ReturnsListener()
    {
        using var listener = TelemetrySetup.CreateAdkActivityListener();
        Assert.NotNull(listener);
    }

    [Fact]
    public void CreateAdkActivityListener_CapturesSpans()
    {
        var capturedActivities = new List<Activity>();
        using var listener = TelemetrySetup.CreateAdkActivityListener(
            onActivityStopped: a => capturedActivities.Add(a));

        using (var activity = AdkTracing.ActivitySource.StartActivity("test_operation"))
        {
            activity?.SetTag("gen_ai.agent.name", "test_agent");
        }

        Assert.Single(capturedActivities);
        Assert.Equal("test_operation", capturedActivities[0].OperationName);
    }

    [Fact]
    public void CreateAdkActivityListener_SetsTagsCorrectly()
    {
        var capturedActivities = new List<Activity>();
        using var listener = TelemetrySetup.CreateAdkActivityListener(
            onActivityStopped: a => capturedActivities.Add(a));

        using (var activity = AdkTracing.ActivitySource.StartActivity("agent_invoke"))
        {
            activity?.SetTag("gen_ai.agent.name", "my_agent");
            activity?.SetTag("gen_ai.operation.name", "invoke_agent");
            activity?.SetTag("gen_ai.conversation.id", "session-42");
        }

        Assert.Single(capturedActivities);
        var tags = capturedActivities[0].Tags.ToDictionary(t => t.Key, t => t.Value);
        Assert.Equal("my_agent", tags["gen_ai.agent.name"]);
        Assert.Equal("invoke_agent", tags["gen_ai.operation.name"]);
        Assert.Equal("session-42", tags["gen_ai.conversation.id"]);
    }

    [Fact]
    public void NestedSpans_HaveParentRelationship()
    {
        var capturedActivities = new List<Activity>();
        using var listener = TelemetrySetup.CreateAdkActivityListener(
            onActivityStopped: a => capturedActivities.Add(a));

        using (var parent = AdkTracing.ActivitySource.StartActivity("parent_op"))
        {
            using (var child = AdkTracing.ActivitySource.StartActivity("child_op"))
            {
                // child span should have parent
            }
        }

        Assert.Equal(2, capturedActivities.Count);
        // Child is stopped first
        Assert.Equal("child_op", capturedActivities[0].OperationName);
        Assert.Equal("parent_op", capturedActivities[1].OperationName);
        Assert.Equal(capturedActivities[1].Id, capturedActivities[0].ParentId);
    }
}
