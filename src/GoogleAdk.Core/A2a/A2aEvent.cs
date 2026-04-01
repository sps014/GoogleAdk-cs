// Copyright 2026 Google LLC
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Serialization;

namespace GoogleAdk.Core.A2a;

/// <summary>
/// Message roles.
/// </summary>
public static class MessageRole
{
    public const string User = "user";
    public const string Agent = "agent";
}

/// <summary>
/// Task states.
/// </summary>
public static class TaskState
{
    public const string Submitted = "submitted";
    public const string Working = "working";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Canceled = "canceled";
    public const string Rejected = "rejected";
    public const string InputRequired = "input-required";
}

public interface IA2aEvent
{
    string Kind { get; }
    Dictionary<string, object?>? Metadata { get; }
}

public sealed class Message : IA2aEvent
{
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "message";

    [JsonPropertyName("messageId")]
    public string MessageId { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = MessageRole.User;

    [JsonPropertyName("parts")]
    public List<A2aPart> Parts { get; set; } = new();

    [JsonPropertyName("taskId")]
    public string? TaskId { get; set; }

    [JsonPropertyName("contextId")]
    public string? ContextId { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, object?>? Metadata { get; set; }
}

public sealed class Task : IA2aEvent
{
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "task";

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("contextId")]
    public string? ContextId { get; set; }

    [JsonPropertyName("status")]
    public TaskStatus Status { get; set; } = new();

    [JsonPropertyName("history")]
    public List<Message>? History { get; set; }

    [JsonPropertyName("artifacts")]
    public List<TaskArtifact>? Artifacts { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, object?>? Metadata { get; set; }
}

public sealed class TaskStatusUpdateEvent : IA2aEvent
{
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "status-update";

    [JsonPropertyName("taskId")]
    public string TaskId { get; set; } = string.Empty;

    [JsonPropertyName("contextId")]
    public string? ContextId { get; set; }

    [JsonPropertyName("final")]
    public bool Final { get; set; }

    [JsonPropertyName("status")]
    public TaskStatus Status { get; set; } = new();

    [JsonPropertyName("metadata")]
    public Dictionary<string, object?>? Metadata { get; set; }
}

public sealed class TaskArtifactUpdateEvent : IA2aEvent
{
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "artifact-update";

    [JsonPropertyName("taskId")]
    public string TaskId { get; set; } = string.Empty;

    [JsonPropertyName("contextId")]
    public string? ContextId { get; set; }

    [JsonPropertyName("artifact")]
    public TaskArtifact Artifact { get; set; } = new();

    [JsonPropertyName("append")]
    public bool? Append { get; set; }

    [JsonPropertyName("lastChunk")]
    public bool? LastChunk { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, object?>? Metadata { get; set; }
}

public sealed class TaskStatus
{
    [JsonPropertyName("state")]
    public string State { get; set; } = TaskState.Submitted;

    [JsonPropertyName("message")]
    public Message? Message { get; set; }

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }
}

public sealed class TaskArtifact
{
    [JsonPropertyName("artifactId")]
    public string ArtifactId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("parts")]
    public List<A2aPart>? Parts { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, object?>? Metadata { get; set; }
}

public sealed class A2aPart
{
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "text";

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("file")]
    public A2aFile? File { get; set; }

    [JsonPropertyName("data")]
    public Dictionary<string, object?>? Data { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, object?>? Metadata { get; set; }
}

public sealed class A2aFile
{
    [JsonPropertyName("uri")]
    public string? Uri { get; set; }

    [JsonPropertyName("bytes")]
    public string? Bytes { get; set; }

    [JsonPropertyName("mimeType")]
    public string? MimeType { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public static class A2aEventHelpers
{
    public static bool IsTaskStatusUpdateEvent(object? evt) =>
        (evt as TaskStatusUpdateEvent)?.Kind == "status-update";

    public static bool IsTaskArtifactUpdateEvent(object? evt) =>
        (evt as TaskArtifactUpdateEvent)?.Kind == "artifact-update";

    public static bool IsMessage(object? evt) =>
        (evt as Message)?.Kind == "message";

    public static bool IsTask(object? evt) =>
        (evt as Task)?.Kind == "task";

    public static Dictionary<string, object?> GetEventMetadata(IA2aEvent evt) =>
        evt.Metadata ?? new Dictionary<string, object?>();

    public static bool IsFailedTaskStatusUpdateEvent(object? evt) =>
        (evt is TaskStatusUpdateEvent tsu && tsu.Status.State == TaskState.Failed) ||
        (evt is Task task && task.Status.State == TaskState.Failed);

    public static bool IsTerminalTaskStatusUpdateEvent(object? evt)
    {
        var state = evt switch
        {
            TaskStatusUpdateEvent tsu => tsu.Status.State,
            Task task => task.Status.State,
            _ => null,
        };

        return state is TaskState.Completed or TaskState.Failed or TaskState.Canceled or TaskState.Rejected;
    }

    public static bool IsInputRequiredTaskStatusUpdateEvent(object? evt)
    {
        var state = evt switch
        {
            TaskStatusUpdateEvent tsu => tsu.Status.State,
            Task task => task.Status.State,
            _ => null,
        };
        return state == TaskState.InputRequired;
    }

    public static string? GetFailedTaskStatusUpdateEventError(TaskStatusUpdateEvent evt)
    {
        if (evt.Status.State != TaskState.Failed) return null;
        var parts = evt.Status.Message?.Parts ?? new List<A2aPart>();
        if (parts.Count == 0) return null;
        if (parts[0].Kind != "text") return null;
        return parts[0].Text;
    }

    public static TaskStatusUpdateEvent CreateTaskSubmittedEvent(
        string taskId,
        string contextId,
        Message message,
        Dictionary<string, object?>? metadata = null)
    {
        return new TaskStatusUpdateEvent
        {
            TaskId = taskId,
            ContextId = contextId,
            Final = false,
            Status = new TaskStatus
            {
                State = TaskState.Submitted,
                Message = message,
                Timestamp = DateTime.UtcNow.ToString("O"),
            },
            Metadata = metadata,
        };
    }

    public static Task CreateTask(
        string taskId,
        string contextId,
        Message message,
        Dictionary<string, object?>? metadata = null)
    {
        return new Task
        {
            Id = string.IsNullOrWhiteSpace(taskId) ? Guid.NewGuid().ToString() : taskId,
            ContextId = contextId,
            History = new List<Message> { message },
            Status = new TaskStatus
            {
                State = TaskState.Submitted,
                Timestamp = DateTime.UtcNow.ToString("O"),
            },
            Metadata = metadata,
        };
    }

    public static TaskStatusUpdateEvent CreateTaskWorkingEvent(
        string taskId,
        string contextId,
        Message? message = null,
        Dictionary<string, object?>? metadata = null)
    {
        return new TaskStatusUpdateEvent
        {
            TaskId = taskId,
            ContextId = contextId,
            Final = false,
            Status = new TaskStatus
            {
                State = TaskState.Working,
                Message = message,
                Timestamp = DateTime.UtcNow.ToString("O"),
            },
            Metadata = metadata,
        };
    }

    public static TaskStatusUpdateEvent CreateTaskCompletedEvent(
        string taskId,
        string contextId,
        Dictionary<string, object?>? metadata = null)
    {
        return new TaskStatusUpdateEvent
        {
            TaskId = taskId,
            ContextId = contextId,
            Final = true,
            Status = new TaskStatus
            {
                State = TaskState.Completed,
                Timestamp = DateTime.UtcNow.ToString("O"),
            },
            Metadata = metadata,
        };
    }

    public static TaskArtifactUpdateEvent CreateTaskArtifactUpdateEvent(
        string taskId,
        string contextId,
        string? artifactId,
        List<A2aPart>? parts = null,
        Dictionary<string, object?>? metadata = null,
        bool? append = null,
        bool? lastChunk = null)
    {
        return new TaskArtifactUpdateEvent
        {
            TaskId = taskId,
            ContextId = contextId,
            Append = append,
            LastChunk = lastChunk,
            Artifact = new TaskArtifact
            {
                ArtifactId = string.IsNullOrWhiteSpace(artifactId) ? Guid.NewGuid().ToString() : artifactId!,
                Parts = parts ?? new List<A2aPart>(),
            },
            Metadata = metadata,
        };
    }

    public static TaskStatusUpdateEvent CreateTaskFailedEvent(
        string taskId,
        string contextId,
        Exception error,
        Dictionary<string, object?>? metadata = null)
    {
        return new TaskStatusUpdateEvent
        {
            TaskId = taskId,
            ContextId = contextId,
            Final = true,
            Status = new TaskStatus
            {
                State = TaskState.Failed,
                Message = new Message
                {
                    Kind = "message",
                    MessageId = Guid.NewGuid().ToString(),
                    Role = MessageRole.Agent,
                    TaskId = taskId,
                    ContextId = contextId,
                    Parts = new List<A2aPart>
                    {
                        new() { Kind = "text", Text = error.Message }
                    },
                },
                Timestamp = DateTime.UtcNow.ToString("O"),
            },
            Metadata = metadata,
        };
    }

    public static TaskStatusUpdateEvent CreateTaskInputRequiredEvent(
        string taskId,
        string contextId,
        List<A2aPart> parts,
        Dictionary<string, object?>? metadata = null)
    {
        return new TaskStatusUpdateEvent
        {
            TaskId = taskId,
            ContextId = contextId,
            Final = true,
            Status = new TaskStatus
            {
                State = TaskState.InputRequired,
                Message = new Message
                {
                    Kind = "message",
                    MessageId = Guid.NewGuid().ToString(),
                    Role = MessageRole.Agent,
                    TaskId = taskId,
                    ContextId = contextId,
                    Parts = parts,
                },
                Timestamp = DateTime.UtcNow.ToString("O"),
            },
            Metadata = metadata,
        };
    }

    public static TaskStatusUpdateEvent CreateInputMissingErrorEvent(
        string taskId,
        string contextId,
        List<A2aPart> parts,
        Dictionary<string, object?>? metadata = null)
    {
        return new TaskStatusUpdateEvent
        {
            TaskId = taskId,
            ContextId = contextId,
            Final = true,
            Status = new TaskStatus
            {
                State = TaskState.InputRequired,
                Message = new Message
                {
                    Kind = "message",
                    MessageId = Guid.NewGuid().ToString(),
                    Role = MessageRole.Agent,
                    TaskId = taskId,
                    ContextId = contextId,
                    Parts = parts,
                },
                Timestamp = DateTime.UtcNow.ToString("O"),
            },
            Metadata = metadata,
        };
    }
}

