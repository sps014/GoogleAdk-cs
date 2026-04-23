using System.Text.Json;
using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Abstractions.Tools;

namespace GoogleAdk.Core.Agents.Processors;

/// <summary>
/// Resumes tool execution after user confirmation events are received.
/// </summary>
public class RequestConfirmationLlmRequestProcessor : BaseLlmRequestProcessor
{
    public static readonly RequestConfirmationLlmRequestProcessor Instance = new();

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

        var confirmationIndex = FindLatestConfirmationEventIndex(events);
        if (confirmationIndex < 0)
            yield break;

        var confirmationEvent = events[confirmationIndex];
        var confirmationsByCallId = ExtractConfirmationResponses(confirmationEvent);
        if (confirmationsByCallId.Count == 0)
            yield break;

        var pending = MapOriginalFunctionCalls(events, confirmationIndex, confirmationsByCallId);
        if (pending.Count == 0)
            yield break;

        RemoveAlreadyRespondedCalls(events, confirmationIndex, pending);
        if (pending.Count == 0)
            yield break;

        var tools = await agent.CanonicalToolsAsync(new ReadonlyContext(invocationContext));
        var toolsDict = tools.ToDictionary(t => t.Name, t => (IBaseTool)t);
        var functionCalls = pending.Values.Select(p => p.OriginalFunctionCall).ToList();
        var toolConfirmations = pending.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Confirmation);

        var functionCallEvent = Event.Create(e =>
        {
            e.InvocationId = invocationContext.InvocationId;
            e.Author = agent.Name;
            e.Branch = invocationContext.Branch;
            e.Content = new Content
            {
                Role = "model",
                Parts = functionCalls.Select(fc => new Part { FunctionCall = fc }).ToList()
            };
        });

        var responseEvent = await FunctionCallHandler.HandleFunctionCallsAsync(
            invocationContext,
            functionCallEvent,
            toolsDict,
            agent.BeforeToolCallback,
            agent.OnToolErrorCallback,
            agent.AfterToolCallback,
            toolConfirmations,
            new HashSet<string>(pending.Keys));

        if (responseEvent != null)
            yield return responseEvent;
    }

    private static int FindLatestConfirmationEventIndex(IReadOnlyList<Event> events)
    {
        for (var i = events.Count - 1; i >= 0; i--)
        {
            var evt = events[i];
            if (evt.Author != "user") continue;
            if (HasConfirmationResponse(evt)) return i;
        }
        return -1;
    }

    private static bool HasConfirmationResponse(Event evt)
    {
        if (evt.Content?.Parts == null) return false;
        return evt.Content.Parts.Any(p =>
            p.FunctionResponse?.Name == FunctionCallHandler.RequestConfirmationFunctionCallName);
    }

    private static Dictionary<string, ToolConfirmation> ExtractConfirmationResponses(Event evt)
    {
        var result = new Dictionary<string, ToolConfirmation>();
        if (evt.Content?.Parts == null) return result;

        foreach (var part in evt.Content.Parts)
        {
            var response = part.FunctionResponse;
            if (response?.Name != FunctionCallHandler.RequestConfirmationFunctionCallName)
                continue;

            if (response.Id == null)
                continue;

            if (TryParseToolConfirmation(response.Response, out var confirmation))
            {
                result[response.Id] = confirmation;
            }
        }

        return result;
    }

    private static Dictionary<string, (FunctionCall OriginalFunctionCall, ToolConfirmation Confirmation)> MapOriginalFunctionCalls(
        IReadOnlyList<Event> events,
        int confirmationIndex,
        Dictionary<string, ToolConfirmation> confirmationsByCallId)
    {
        var result = new Dictionary<string, (FunctionCall, ToolConfirmation)>();
        if (confirmationsByCallId.Count == 0) return result;

        for (var i = confirmationIndex - 1; i >= 0; i--)
        {
            var evt = events[i];
            if (evt.Content?.Parts == null) continue;

            foreach (var part in evt.Content.Parts)
            {
                var call = part.FunctionCall;
                if (call?.Name != FunctionCallHandler.RequestConfirmationFunctionCallName)
                    continue;

                if (call.Id == null || !confirmationsByCallId.TryGetValue(call.Id, out var confirmation))
                    continue;

                if (!TryGetOriginalFunctionCall(call.Args, out var originalCall) || originalCall.Id == null)
                    continue;

                result[originalCall.Id] = (originalCall, confirmation);
            }
        }

        return result;
    }

    private static void RemoveAlreadyRespondedCalls(
        IReadOnlyList<Event> events,
        int confirmationIndex,
        Dictionary<string, (FunctionCall OriginalFunctionCall, ToolConfirmation Confirmation)> pending)
    {
        if (pending.Count == 0) return;

        var respondedIds = new HashSet<string>();
        for (var i = confirmationIndex + 1; i < events.Count; i++)
        {
            var evt = events[i];
            if (evt.Content?.Parts == null) continue;

            foreach (var part in evt.Content.Parts)
            {
                if (part.FunctionResponse?.Id is { } id)
                    respondedIds.Add(id);
            }
        }

        foreach (var id in respondedIds)
            pending.Remove(id);
    }

    private static readonly JsonSerializerOptions s_caseInsensitiveOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static bool TryParseToolConfirmation(
        Dictionary<string, object?>? response,
        out ToolConfirmation confirmation)
    {
        confirmation = new ToolConfirmation();
        if (response == null) return false;

        // 1) Nested under "toolConfirmation" key (console app / structured format)
        if (response.TryGetValue("toolConfirmation", out var nested) &&
            TryDeserialize<ToolConfirmation>(nested, out var nestedConfirmation))
        {
            confirmation = nestedConfirmation;
            return true;
        }

        // 2) Web UI wrapper format: {"response": "<json_or_text>"}
        //    Matches Python ADK's _parse_tool_confirmation behavior.
        if (response.Count == 1 && response.TryGetValue("response", out var responseValue))
        {
            var text = responseValue?.ToString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                // Try parsing the inner value as a JSON ToolConfirmation
                try
                {
                    var parsed = JsonSerializer.Deserialize<ToolConfirmation>(text, s_caseInsensitiveOptions);
                    if (parsed != null)
                    {
                        confirmation = parsed;
                        return true;
                    }
                }
                catch { /* Not valid JSON — fall through to text interpretation */ }

                // Interpret free-text as confirmation/rejection
                confirmation.Accepted = IsApprovalText(text);
                return true;
            }
        }

        // 3) Direct ToolConfirmation shape
        if (TryDeserialize<ToolConfirmation>(response, out var directConfirmation) &&
            (!string.IsNullOrWhiteSpace(directConfirmation.FunctionCallId) || directConfirmation.Accepted != null))
        {
            confirmation = directConfirmation;
            return true;
        }

        // 4) Manual field extraction
        var functionCallId = GetString(response, "functionCallId")
                             ?? GetString(response, "function_call_id");

        confirmation.FunctionCallId = functionCallId ?? string.Empty;
        confirmation.Accepted = GetBool(response, "accepted") ?? GetBool(response, "approved");
        return confirmation.Accepted != null || !string.IsNullOrWhiteSpace(functionCallId);
    }

    private static bool IsApprovalText(string text)
    {
        var trimmed = text.Trim();
        return trimmed.StartsWith("y", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("approve", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("confirm", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("ok", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("sure", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("accept", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetString(Dictionary<string, object?> response, string key)
    {
        return response.TryGetValue(key, out var value) ? value?.ToString() : null;
    }

    private static bool? GetBool(Dictionary<string, object?> response, string key)
    {
        if (!response.TryGetValue(key, out var value) || value == null) return null;
        if (value is bool b) return b;
        if (bool.TryParse(value.ToString(), out var parsed)) return parsed;
        return null;
    }

    private static bool TryDeserialize<T>(object? value, out T result)
    {
        result = default!;
        if (value == null) return false;

        if (value is T typed)
        {
            result = typed;
            return true;
        }

        try
        {
            if (value is JsonElement element)
            {
                result = element.Deserialize<T>()!;
                return true;
            }

            if (value is Dictionary<string, object?> dict)
            {
                var json = JsonSerializer.Serialize(dict);
                result = JsonSerializer.Deserialize<T>(json)!;
                return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static bool TryGetOriginalFunctionCall(
        Dictionary<string, object?>? args,
        out FunctionCall originalCall)
    {
        originalCall = new FunctionCall();
        if (args == null) return false;
        if (!args.TryGetValue("originalFunctionCall", out var value)) return false;
        return TryDeserialize(value, out originalCall);
    }
}
