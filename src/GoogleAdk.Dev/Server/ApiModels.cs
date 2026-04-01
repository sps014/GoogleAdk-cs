using System.Text.Json.Serialization;
using GoogleAdk.Core.Abstractions.Models;

namespace GoogleAdk.Dev.Server;

/// <summary>
/// Request body for /run and /run_sse endpoints.
/// </summary>
public class RunAgentRequest
{
    [JsonPropertyName("appName")]
    public string AppName { get; set; } = string.Empty;

    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("newMessage")]
    public Content? NewMessage { get; set; }

    /// <summary>
    /// Convenience: if set and NewMessage is null, wraps this string into a Content.
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("streaming")]
    public bool Streaming { get; set; }

    [JsonPropertyName("stateDelta")]
    public Dictionary<string, object?>? StateDelta { get; set; }

    [JsonPropertyName("runConfig")]
    public GoogleAdk.Core.Agents.RunConfig? RunConfig { get; set; }

    /// <summary>Resolves the user message, preferring NewMessage over Message.</summary>
    public Content ResolveMessage() =>
        NewMessage ?? new Content
        {
            Role = "user",
            Parts = new List<Part> { new() { Text = Message ?? string.Empty } }
        };
}

/// <summary>
/// Request body for session creation.
/// </summary>
public class CreateSessionBody
{
    [JsonPropertyName("state")]
    public Dictionary<string, object?>? State { get; set; }
}
