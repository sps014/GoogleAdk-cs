using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Sessions;
using Spectre.Console;
using Spectre.Console.Json;
using System;
using System.Linq;
using System.Text.Json;

namespace GoogleAdk.Core.Runner;

public static partial class ConsoleRunner
{
    internal static void ProcessEvent(
        Event evt,
        ConsoleRunnerConfig config,
        ConsoleRenderState state)
    {
        // Skip user messages or empty authors
        if (evt.Author == "user" || string.IsNullOrEmpty(evt.Author))
            return;

        if (config.DebugMode && evt.Author != state.CurrentAgent)
        {
            FlushThinkingBuffer(state);
            FlushTextBuffer(state);
            AnsiConsole.MarkupLine($"[yellow]=> Subagent Transfer: {state.CurrentAgent} -> {evt.Author}[/]");
            state.CurrentAgent = evt.Author;
        }

        // Process Tool Calls (if debug)
        // We only print tool calls on the final (non-partial) event so we don't double-print them
        if (config.DebugMode && evt.Partial != true)
        {
            var calls = evt.GetFunctionCalls();
            if (calls.Count > 0)
            {
                FlushThinkingBuffer(state);
                FlushTextBuffer(state);
            }

            foreach (var call in calls)
            {
                var argsJson = JsonSerializer.Serialize(call.Args, new JsonSerializerOptions { 
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
                var panel = new Panel(new JsonText(argsJson))
                    .Header($"[yellow]Tool Call: {Markup.Escape(call.Name)}[/]")
                    .BorderColor(Color.Yellow)
                    .Expand();
                AnsiConsole.Write(panel);
            }

            var responses = evt.GetFunctionResponses();
            if (responses.Count > 0)
            {
                FlushThinkingBuffer(state);
                FlushTextBuffer(state);
            }

            foreach (var resp in responses)
            {
                var respJson = JsonSerializer.Serialize(resp.Response, new JsonSerializerOptions { 
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
                var panel = new Panel(new JsonText(respJson))
                    .Header($"[green]Tool Result: {Markup.Escape(resp.Name)}[/]")
                    .BorderColor(Color.Green)
                    .Expand();
                AnsiConsole.Write(panel);
            }
        }

        // Process Thought Parts (model-native thinking / reasoning)
        var thoughtParts = evt.Content?.Parts?.Where(p => p.Thought == true && !string.IsNullOrEmpty(p.Text)).ToList();
        if (thoughtParts?.Count > 0)
        {
            var thoughtText = string.Join("", thoughtParts.Select(p => p.Text));
            if (evt.Partial == true)
            {
                if (state.StreamingThinkingAuthor != evt.Author)
                {
                    FlushThinkingBuffer(state);
                    state.StreamingThinkingAuthor = evt.Author;
                    // Print indicator once when thinking starts, not for every token
                    AnsiConsole.Markup($"\r[grey]{Markup.Escape(evt.Author!)}[/] thinking... ");
                }
                state.ThinkingBuffer.Append(thoughtText);
            }
            else
            {
                // Non-partial: thinking arrived without streaming (non-stream mode).
                // If we were already streaming thoughts, the buffer is complete — don't append again.
                if (state.StreamingThinkingAuthor == evt.Author)
                {
                    AnsiConsole.Write(new string(' ', Console.WindowWidth));
                    AnsiConsole.Markup("\r");
                    // Buffer already contains all streamed tokens; do NOT re-append thoughtText
                }
                else
                {
                    // Fresh non-streamed thought — put it directly in the buffer
                    state.ThinkingBuffer.Append(thoughtText);
                }
                var thinkPanel = new Panel(Markup.Escape(state.ThinkingBuffer.ToString().TrimEnd()))
                    .Header($"[grey]Thinking ({Markup.Escape(evt.Author!)})[/]")
                    .BorderColor(Color.Grey)
                    .Expand();
                AnsiConsole.Write(thinkPanel);
                state.StreamingThinkingAuthor = null;
                state.ThinkingBuffer.Clear();
            }
        }

        // Process Errors and Safety
        if (!string.IsNullOrEmpty(evt.ErrorMessage) || evt.ErrorCode != null)
        {
            FlushThinkingBuffer(state);
            FlushTextBuffer(state);

            var errorMsg = evt.ErrorMessage ?? "Unknown Error";
            var errorCode = evt.ErrorCode != null ? $" (Code: {evt.ErrorCode})" : "";
            var errorPanel = new Panel($"[red]{Markup.Escape(errorMsg)}{Markup.Escape(errorCode)}[/]")
                .Header($"[red]Error ({Markup.Escape(evt.Author ?? "System")})[/]")
                .BorderColor(Color.Red)
                .Expand();
            AnsiConsole.Write(errorPanel);
        }
        else if (evt.FinishReason == "SAFETY")
        {
            FlushThinkingBuffer(state);
            FlushTextBuffer(state);

            var safetyPanel = new Panel("[yellow]Response was blocked due to safety settings.[/]")
                .Header($"[yellow]Safety Block ({Markup.Escape(evt.Author ?? "System")})[/]")
                .BorderColor(Color.Yellow)
                .Expand();
            AnsiConsole.Write(safetyPanel);
        }

        // Process AI Text Response
        var textContent = evt.StringifyContent();
        if (!string.IsNullOrWhiteSpace(textContent))
        {
            // Flush any pending thinking buffer before rendering response text.
            FlushThinkingBuffer(state);

            if (evt.Partial == true)
            {
                if (state.StreamingAuthor != evt.Author)
                {
                    if (state.StreamingAuthor != null)
                    {
                        var panel = new Panel(MarkdownConsoleRenderer.Render(state.TextBuffer.ToString().TrimEnd()))
                            .Header($"[blue]{Markup.Escape(state.StreamingAuthor ?? "System")}[/]")
                            .BorderColor(Color.Blue)
                            .Expand();
                        AnsiConsole.Write(panel);
                        state.TextBuffer.Clear();
                    }
                    state.StreamingAuthor = evt.Author;
                    // Print indicator once when streaming starts, not for every token
                    AnsiConsole.Markup($"\r[blue]{Markup.Escape(state.StreamingAuthor ?? "System")}[/] streaming... ");
                }
                state.TextBuffer.Append(textContent);
            }
            else
            {
                if (state.StreamingAuthor == evt.Author)
                {
                    // We already streamed it, just finalize the panel
                    // Clear the "streaming..." line
                    AnsiConsole.Write(new string(' ', Console.WindowWidth));
                    AnsiConsole.Markup("\r");
                    
                    var panel = new Panel(MarkdownConsoleRenderer.Render(state.TextBuffer.ToString().TrimEnd()))
                        .Header($"[blue]{Markup.Escape(state.StreamingAuthor ?? "System")}[/]")
                        .BorderColor(Color.Blue)
                        .Expand();
                    AnsiConsole.Write(panel);
                    
                    state.StreamingAuthor = null;
                    state.TextBuffer.Clear();
                }
                else
                {
                    // Did not stream, print the whole thing in a panel
                    var panel = new Panel(MarkdownConsoleRenderer.Render(textContent))
                        .Header($"[blue]{Markup.Escape(evt.Author ?? "System")}[/]")
                        .BorderColor(Color.Blue)
                        .Expand();
                    AnsiConsole.Write(panel);
                }
            }
        }

        // Process FileData/CodeExecutionResult Artifacts
        if (evt.Content?.Parts != null)
        {
            foreach (var part in evt.Content.Parts)
            {
                if (part.FileData != null)
                {
                    FlushTextBuffer(state);

                    var fileName = part.FileData.FileUri ?? "generated_file";
                    AnsiConsole.MarkupLine($"[grey]💾 Saved Artifact: {Markup.Escape(fileName)}[/]");
                }
                
                if (part.CodeExecutionResult != null)
                {
                    FlushTextBuffer(state);

                    var outcome = part.CodeExecutionResult.Outcome ?? "Unknown";
                    var output = part.CodeExecutionResult.Output ?? string.Empty;
                    var execPanel = new Panel(Markup.Escape(output))
                        .Header($"[grey]Code Execution ({outcome})[/]")
                        .BorderColor(Color.Grey)
                        .Expand();
                    AnsiConsole.Write(execPanel);
                }
            }
        }
    }

    internal static void FlushThinkingBuffer(ConsoleRenderState state)
    {
        if (state.StreamingThinkingAuthor != null)
        {
            AnsiConsole.Write(new string(' ', Console.WindowWidth));
            AnsiConsole.Markup("\r");
            var panel = new Panel(Markup.Escape(state.ThinkingBuffer.ToString().TrimEnd()))
                .Header($"[grey]Thinking ({Markup.Escape(state.StreamingThinkingAuthor)})[/]")
                .BorderColor(Color.Grey)
                .Expand();
            AnsiConsole.Write(panel);
            state.StreamingThinkingAuthor = null;
            state.ThinkingBuffer.Clear();
        }
    }

    internal static void FlushTextBuffer(ConsoleRenderState state)
    {
        if (state.StreamingAuthor != null)
        {
            // Clear the "streaming..." line
            AnsiConsole.Write(new string(' ', Console.WindowWidth));
            AnsiConsole.Markup("\r");
            
            var panel = new Panel(MarkdownConsoleRenderer.Render(state.TextBuffer.ToString().TrimEnd()))
                .Header($"[blue]{Markup.Escape(state.StreamingAuthor ?? "System")}[/]")
                .BorderColor(Color.Blue)
                .Expand();
            AnsiConsole.Write(panel);
            
            state.StreamingAuthor = null;
            state.TextBuffer.Clear();
        }
    }
}
