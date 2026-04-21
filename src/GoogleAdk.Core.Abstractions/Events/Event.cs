using GoogleAdk.Core.Abstractions.Models;

namespace GoogleAdk.Core.Abstractions.Events;

/// <summary>
/// Represents an event in a conversation between agents and users.
/// Stores the content of the conversation and actions taken by agents.
/// </summary>
public class Event : LlmResponse
{
    /// <summary>
    /// The unique identifier of the event.
    /// Do not assign the ID manually; it will be assigned by the session.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The invocation ID of the event. Should be non-empty before appending to a session.
    /// </summary>
    public string InvocationId { get; set; } = string.Empty;

    /// <summary>
    /// "user" or the name of the agent, indicating who appended the event to the session.
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// The actions taken by the agent.
    /// </summary>
    public EventActions Actions { get; set; } = new();

    /// <summary>
    /// Set of IDs of the long running function calls.
    /// Agent client will know from this field about which function call is long running.
    /// </summary>
    public List<string>? LongRunningToolIds { get; set; }

    /// <summary>
    /// The branch of the event.
    /// Format: agent_1.agent_2.agent_3, where agent_1 is the parent of agent_2.
    /// Used when multiple sub-agents shouldn't see their peer agents' conversation history.
    /// </summary>
    public string? Branch { get; set; }

    /// <summary>
    /// The timestamp of the event (Unix milliseconds).
    /// </summary>
    public long Timestamp { get; set; }

    private static readonly Random _random = new();
    private const string AlphanumericChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    /// <summary>
    /// Creates a new Event with default values and an auto-generated ID.
    /// </summary>
    public static Event Create(Action<Event>? configure = null)
    {
        var evt = new Event
        {
            Id = GenerateEventId(),
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Actions = new EventActions(),
            LongRunningToolIds = new List<string>()
        };
        configure?.Invoke(evt);
        return evt;
    }

    /// <summary>
    /// Returns whether the event is the final response of the agent.
    /// </summary>
    public bool IsFinalResponse()
    {
        if (Actions.SkipSummarization == true ||
            (LongRunningToolIds != null && LongRunningToolIds.Count > 0))
        {
            return true;
        }

        return GetFunctionCalls().Count == 0 &&
               GetFunctionResponses().Count == 0 &&
               Partial != true &&
               !HasTrailingCodeExecutionResult();
    }

    /// <summary>
    /// Returns the function calls in the event.
    /// </summary>
    public List<FunctionCall> GetFunctionCalls()
    {
        var calls = new List<FunctionCall>();
        if (Content?.Parts != null)
        {
            foreach (var part in Content.Parts)
            {
                if (part.FunctionCall != null)
                    calls.Add(part.FunctionCall);
            }
        }
        return calls;
    }

    /// <summary>
    /// Returns the function responses in the event.
    /// </summary>
    public List<FunctionResponse> GetFunctionResponses()
    {
        var responses = new List<FunctionResponse>();
        if (Content?.Parts != null)
        {
            foreach (var part in Content.Parts)
            {
                if (part.FunctionResponse != null)
                    responses.Add(part.FunctionResponse);
            }
        }
        return responses;
    }

    /// <summary>
    /// Returns whether the event has a trailing code execution result.
    /// </summary>
    public bool HasTrailingCodeExecutionResult()
    {
        if (Content?.Parts is { Count: > 0 })
        {
            var lastPart = Content.Parts[^1];
            return lastPart.CodeExecutionResult != null;
        }
        return false;
    }

    /// <summary>
    /// Extracts and concatenates all non-thought text from the parts of this event.
    /// Thought parts (where <see cref="Part.Thought"/> is <c>true</c>) are excluded
    /// so they do not bleed into tool results, output keys, or regular text output.
    /// </summary>
    public string StringifyContent()
    {
        if (Content?.Parts == null)
            return string.Empty;

        return string.Join("", Content.Parts.Where(p => p.Thought != true).Select(p => p.Text ?? ""));
    }

    /// <summary>
    /// Generates a random 8-character alphanumeric event ID.
    /// </summary>
    public static string GenerateEventId()
    {
        var chars = new char[8];
        lock (_random)
        {
            for (int i = 0; i < 8; i++)
                chars[i] = AlphanumericChars[_random.Next(AlphanumericChars.Length)];
        }
        return new string(chars);
    }
}
