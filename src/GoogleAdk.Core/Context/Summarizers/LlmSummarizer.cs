using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Events;

namespace GoogleAdk.Core.Context.Summarizers;

/// <summary>
/// A summarizer that uses an LLM to generate a compacted representation of existing events.
/// </summary>
public class LlmSummarizer : IBaseSummarizer
{
    private readonly BaseLlm _llm;
    private readonly string _prompt;

    private const string DefaultPrompt =
        "The following is a conversation history between a user and an AI" +
        " agent. Please summarize the conversation, focusing on key" +
        " information and decisions made, as well as any unresolved" +
        " questions or tasks. The summary should be concise and capture the" +
        " essence of the interaction.";

    public LlmSummarizer(BaseLlm llm, string? prompt = null)
    {
        _llm = llm;
        _prompt = prompt ?? DefaultPrompt;
    }

    public async Task<CompactedEvent> SummarizeAsync(List<Event> events)
    {
        if (events.Count == 0)
            throw new ArgumentException("Cannot summarize an empty list of events.");

        var startTime = events[0].Timestamp;
        var endTime = events[^1].Timestamp;

        // Format events for the LLM
        var formattedEvents = string.Empty;
        for (int i = 0; i < events.Count; i++)
        {
            formattedEvents += $"[Event {i + 1} - Author: {events[i].Author}]\n";
            formattedEvents += $"{StringifyContent(events[i])}\n\n";
        }

        var fullPrompt = $"{_prompt}\n\n{formattedEvents}";

        var request = new LlmRequest
        {
            Contents = new List<Content>
            {
                new() { Role = "user", Parts = new List<Part> { new() { Text = fullPrompt } } }
            }
        };

        var compactedContent = string.Empty;
        await foreach (var response in _llm.GenerateContentAsync(request, false))
        {
            if (response.Content?.Parts != null)
            {
                foreach (var part in response.Content.Parts)
                {
                    if (part.Text != null)
                        compactedContent += part.Text;
                }
            }
        }

        if (string.IsNullOrEmpty(compactedContent))
            throw new InvalidOperationException("LLM failed to return a valid summary.");

        return CompactedEvent.CreateCompacted(evt =>
        {
            evt.Author = "system";
            evt.Content = new Content
            {
                Role = "model",
                Parts = new List<Part> { new() { Text = compactedContent } }
            };
            evt.StartTime = startTime;
            evt.EndTime = endTime;
            evt.CompactedContent = compactedContent;
        });
    }

    private static string StringifyContent(Event evt)
    {
        return evt.StringifyContent();
    }
}
