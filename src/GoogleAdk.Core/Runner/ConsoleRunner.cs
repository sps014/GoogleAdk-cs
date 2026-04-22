using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Abstractions.Sessions;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Artifacts;
using GoogleAdk.Core.Memory;
using GoogleAdk.Core.Sessions;
using Spectre.Console;
using Spectre.Console.Json;
using System.Text.Json;

namespace GoogleAdk.Core.Runner;

/// <summary>
/// Configuration for the ConsoleRunner.
/// </summary>
public class ConsoleRunnerConfig : RunnerConfig
{
    /// <summary>
    /// Gets or sets whether debug mode is enabled.
    /// When true, displays tool calls, results, and subagent transfers.
    /// Defaults to true.
    /// </summary>
    public bool DebugMode { get; set; } = true;

    /// <summary>
    /// Gets or sets whether streaming mode is enabled.
    /// When true, text output streams chunk by chunk.
    /// Defaults to false.
    /// </summary>
    public bool EnableStreaming { get; set; } = false;

    /// <summary>
    /// Gets or sets the text to display in the figlet logo.
    /// Defaults to "Google ADK".
    /// </summary>
    public string FigletText { get; set; } = "Google ADK";
}

/// <summary>
/// A built-in runner that provides a beautiful CLI interface using Spectre.Console.
/// </summary>
public static class ConsoleRunner
{
    /// <summary>
    /// Runs the agent continuously in the console until interrupted.
    /// </summary>
    public static async Task RunAsync(BaseAgent rootAgent, Action<ConsoleRunnerConfig>? configure = null, Dictionary<string, object?>? initialState = null)
    {
        var config = new ConsoleRunnerConfig
        {
            AppName = rootAgent.Name,
            Agent = rootAgent,
            SessionService = null!, // Will be initialized below if not provided
        };

        configure?.Invoke(config);

        config.SessionService ??= new InMemorySessionService();
        config.ArtifactService ??= new InMemoryArtifactService();
        config.MemoryService ??= new InMemoryMemoryService();

        var runner = new Runner(config);

        string userId = "user";
        
        // We need to actually create the session first before running.
        var session = await config.SessionService.CreateSessionAsync(new CreateSessionRequest
        {
            AppName = config.AppName,
            UserId = userId,
        });

        string sessionId = session.Id;
        string currentAgent = rootAgent.Name;

        AnsiConsole.Write(
            new FigletText(config.FigletText)
                .Centered()
                .Color(Color.Blue));

        AnsiConsole.MarkupLine("[grey]Type '/bye' or press Ctrl+C to exit.[/]");
        AnsiConsole.MarkupLine("[grey]Type '/attach <filepath> [[filepath...]]' to upload files.[/]");
        AnsiConsole.MarkupLine($"[grey]Debug mode: {(config.DebugMode ? "ON" : "OFF")}[/]\n");

        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            var stagedFiles = new List<Part>();
            var currentStateDelta = initialState;

            while (!cts.Token.IsCancellationRequested)
            {
                AnsiConsole.Markup("[green]You:[/] ");
                var input = Console.ReadLine();
                
                if (input == null) break; // Handle EOF (Ctrl+D / Ctrl+Z)
                if (string.IsNullOrWhiteSpace(input)) continue;
                if (input.Trim().Equals("/bye", StringComparison.OrdinalIgnoreCase)) break;

                if (input.StartsWith("/attach ", StringComparison.OrdinalIgnoreCase))
                {
                    var filePaths = input.Substring("/attach ".Length).Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var path in filePaths)
                    {
                        try
                        {
                            var bytes = await File.ReadAllBytesAsync(path, cts.Token);
                            var extension = Path.GetExtension(path);
                            string mimeType = "application/octet-stream";
                            try
                            {
                                mimeType = MimeTypes.MimeTypeMap.GetMimeType(extension);
                            }
                            catch
                            {
                                // fallback to default
                            }

                            stagedFiles.Add(new Part
                            {
                                InlineData = new InlineData
                                {
                                    Data = Convert.ToBase64String(bytes),
                                    MimeType = mimeType,
                                    DisplayName = Path.GetFileName(path)
                                }
                            });
                            AnsiConsole.MarkupLine($"[grey]📎 Attached {Markup.Escape(Path.GetFileName(path))}[/]");
                        }
                        catch (Exception ex)
                        {
                            AnsiConsole.MarkupLine($"[red]Failed to attach '{Markup.Escape(path)}': {Markup.Escape(ex.Message)}[/]");
                        }
                    }
                    continue;
                }

                var contentParts = new List<Part> { new Part { Text = input } };
                if (stagedFiles.Count > 0)
                {
                    contentParts.AddRange(stagedFiles);
                    stagedFiles.Clear();
                }

                var content = new Content
                {
                    Role = "user",
                    Parts = contentParts
                };

                var runConfig = new RunConfig
                {
                    StreamingMode = config.EnableStreaming ? StreamingMode.Sse : StreamingMode.None,
                    SaveInputBlobsAsArtifacts = true
                };

                string? currentlyStreamingAuthor = null;
                var textBuffer = new System.Text.StringBuilder();
                string? currentlyStreamingThinkingAuthor = null;
                var thinkingBuffer = new System.Text.StringBuilder();

                await foreach (var evt in runner.RunAsync(userId, sessionId, content, currentStateDelta, runConfig, cts.Token))
                {
                    // Skip user messages or empty authors
                    if (evt.Author == "user" || string.IsNullOrEmpty(evt.Author))
                        continue;

                    // Process subagent transfers
                    if (config.DebugMode && evt.Author != currentAgent)
                    {
                        if (currentlyStreamingThinkingAuthor != null)
                        {
                            AnsiConsole.Write(new string(' ', Console.WindowWidth));
                            AnsiConsole.Markup("\r");
                            var thinkPanel = new Panel(thinkingBuffer.ToString().TrimEnd())
                                .Header($"[grey]Thinking ({Markup.Escape(currentlyStreamingThinkingAuthor)})[/]")
                                .BorderColor(Color.Grey)
                                .Expand();
                            AnsiConsole.Write(thinkPanel);
                            currentlyStreamingThinkingAuthor = null;
                            thinkingBuffer.Clear();
                        }

                        if (currentlyStreamingAuthor != null)
                        {
                            // Clear the "streaming..." line
                            AnsiConsole.Write(new string(' ', Console.WindowWidth));
                            AnsiConsole.Markup("\r");
                            
                            var panel = new Panel(textBuffer.ToString().TrimEnd())
                                .Header($"[blue]{Markup.Escape(currentlyStreamingAuthor)}[/]")
                                .BorderColor(Color.Blue)
                                .Expand();
                            AnsiConsole.Write(panel);
                            
                            currentlyStreamingAuthor = null;
                            textBuffer.Clear();
                        }
                        AnsiConsole.MarkupLine($"[yellow]=> Subagent Transfer: {currentAgent} -> {evt.Author}[/]");
                        currentAgent = evt.Author;
                    }

                    // Process Tool Calls (if debug)
                    // We only print tool calls on the final (non-partial) event so we don't double-print them
                    if (config.DebugMode && evt.Partial != true)
                    {
                        var calls = evt.GetFunctionCalls();
                        if (calls.Count > 0)
                        {
                            // Flush thinking panel first so it appears BEFORE the tool call
                            if (currentlyStreamingThinkingAuthor != null)
                            {
                                AnsiConsole.Write(new string(' ', Console.WindowWidth));
                                AnsiConsole.Markup("\r");
                                var thinkFlushPanel = new Panel(Markup.Escape(thinkingBuffer.ToString().TrimEnd()))
                                    .Header($"[grey]Thinking ({Markup.Escape(currentlyStreamingThinkingAuthor)})[/]")
                                    .BorderColor(Color.Grey)
                                    .Expand();
                                AnsiConsole.Write(thinkFlushPanel);
                                currentlyStreamingThinkingAuthor = null;
                                thinkingBuffer.Clear();
                            }

                            if (currentlyStreamingAuthor != null)
                            {
                                AnsiConsole.Write(new string(' ', Console.WindowWidth));
                                AnsiConsole.Markup("\r");
                                var panel = new Panel(textBuffer.ToString().TrimEnd())
                                    .Header($"[blue]{Markup.Escape(currentlyStreamingAuthor)}[/]")
                                    .BorderColor(Color.Blue)
                                    .Expand();
                                AnsiConsole.Write(panel);
                                currentlyStreamingAuthor = null;
                                textBuffer.Clear();
                            }
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
                            if (currentlyStreamingThinkingAuthor != null)
                            {
                                AnsiConsole.Write(new string(' ', Console.WindowWidth));
                                AnsiConsole.Markup("\r");
                                var thinkFlushPanel = new Panel(Markup.Escape(thinkingBuffer.ToString().TrimEnd()))
                                    .Header($"[grey]Thinking ({Markup.Escape(currentlyStreamingThinkingAuthor)})[/]")
                                    .BorderColor(Color.Grey)
                                    .Expand();
                                AnsiConsole.Write(thinkFlushPanel);
                                currentlyStreamingThinkingAuthor = null;
                                thinkingBuffer.Clear();
                            }

                            if (currentlyStreamingAuthor != null)
                            {
                                AnsiConsole.Write(new string(' ', Console.WindowWidth));
                                AnsiConsole.Markup("\r");
                                var panel = new Panel(textBuffer.ToString().TrimEnd())
                                    .Header($"[blue]{Markup.Escape(currentlyStreamingAuthor)}[/]")
                                    .BorderColor(Color.Blue)
                                    .Expand();
                                AnsiConsole.Write(panel);
                                currentlyStreamingAuthor = null;
                                textBuffer.Clear();
                            }
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
                            if (currentlyStreamingThinkingAuthor != evt.Author)
                            {
                                if (currentlyStreamingThinkingAuthor != null)
                                {
                                    AnsiConsole.Write(new string(' ', Console.WindowWidth));
                                    AnsiConsole.Markup("\r");
                                    var finishPanel = new Panel(Markup.Escape(thinkingBuffer.ToString().TrimEnd()))
                                        .Header($"[grey]Thinking ({Markup.Escape(currentlyStreamingThinkingAuthor)})[/]")
                                        .BorderColor(Color.Grey)
                                        .Expand();
                                    AnsiConsole.Write(finishPanel);
                                    thinkingBuffer.Clear();
                                }
                                currentlyStreamingThinkingAuthor = evt.Author;
                                // Print indicator once when thinking starts, not for every token
                                AnsiConsole.Markup($"\r[grey]{Markup.Escape(evt.Author!)}[/] thinking... ");
                            }
                            thinkingBuffer.Append(thoughtText);
                        }
                        else
                        {
                            // Non-partial: thinking arrived without streaming (non-stream mode).
                            // If we were already streaming thoughts, the buffer is complete — don't append again.
                            if (currentlyStreamingThinkingAuthor == evt.Author)
                            {
                                AnsiConsole.Write(new string(' ', Console.WindowWidth));
                                AnsiConsole.Markup("\r");
                                // Buffer already contains all streamed tokens; do NOT re-append thoughtText
                            }
                            else
                            {
                                // Fresh non-streamed thought — put it directly in the buffer
                                thinkingBuffer.Append(thoughtText);
                            }
                            var thinkPanel = new Panel(Markup.Escape(thinkingBuffer.ToString().TrimEnd()))
                                .Header($"[grey]Thinking ({Markup.Escape(evt.Author!)})[/]")
                                .BorderColor(Color.Grey)
                                .Expand();
                            AnsiConsole.Write(thinkPanel);
                            currentlyStreamingThinkingAuthor = null;
                            thinkingBuffer.Clear();
                        }
                    }

                    // Process AI Text Response
                    var textContent = evt.StringifyContent();
                    if (!string.IsNullOrWhiteSpace(textContent))
                    {
                        // Flush any pending thinking buffer before rendering response text.
                        if (currentlyStreamingThinkingAuthor != null)
                        {
                            AnsiConsole.Write(new string(' ', Console.WindowWidth));
                            AnsiConsole.Markup("\r");
                            var pendingThinkPanel = new Panel(Markup.Escape(thinkingBuffer.ToString().TrimEnd()))
                                .Header($"[grey]Thinking ({Markup.Escape(currentlyStreamingThinkingAuthor)})[/]")
                                .BorderColor(Color.Grey)
                                .Expand();
                            AnsiConsole.Write(pendingThinkPanel);
                            currentlyStreamingThinkingAuthor = null;
                            thinkingBuffer.Clear();
                        }

                        if (evt.Partial == true)
                        {
                            if (currentlyStreamingAuthor != evt.Author)
                            {
                                if (currentlyStreamingAuthor != null)
                                {
                                    var panel = new Panel(textBuffer.ToString().TrimEnd())
                                        .Header($"[blue]{Markup.Escape(currentlyStreamingAuthor)}[/]")
                                        .BorderColor(Color.Blue)
                                        .Expand();
                                    AnsiConsole.Write(panel);
                                    textBuffer.Clear();
                                }
                                currentlyStreamingAuthor = evt.Author;
                                // Print indicator once when streaming starts, not for every token
                                AnsiConsole.Markup($"\r[blue]{Markup.Escape(currentlyStreamingAuthor)}[/] streaming... ");
                            }
                            textBuffer.Append(textContent);
                        }
                        else
                        {
                            if (currentlyStreamingAuthor == evt.Author)
                            {
                                // We already streamed it, just finalize the panel
                                // Clear the "streaming..." line
                                AnsiConsole.Write(new string(' ', Console.WindowWidth));
                                AnsiConsole.Markup("\r");
                                
                                var panel = new Panel(textBuffer.ToString().TrimEnd())
                                    .Header($"[blue]{Markup.Escape(currentlyStreamingAuthor)}[/]")
                                    .BorderColor(Color.Blue)
                                    .Expand();
                                AnsiConsole.Write(panel);
                                
                                currentlyStreamingAuthor = null;
                                textBuffer.Clear();
                            }
                            else
                            {
                                // Did not stream, print the whole thing in a panel
                                var panel = new Panel(Markup.Escape(textContent))
                                    .Header($"[blue]{Markup.Escape(evt.Author)}[/]")
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
                                if (currentlyStreamingAuthor != null)
                                {
                                    AnsiConsole.Write(new string(' ', Console.WindowWidth));
                                    AnsiConsole.Markup("\r");
                                    var flushPanel = new Panel(textBuffer.ToString().TrimEnd())
                                        .Header($"[blue]{Markup.Escape(currentlyStreamingAuthor)}[/]")
                                        .BorderColor(Color.Blue)
                                        .Expand();
                                    AnsiConsole.Write(flushPanel);
                                    currentlyStreamingAuthor = null;
                                    textBuffer.Clear();
                                }

                                var fileName = part.FileData.FileUri ?? "generated_file";
                                AnsiConsole.MarkupLine($"[grey]💾 Saved Artifact: {Markup.Escape(fileName)}[/]");
                            }
                            
                            if (part.CodeExecutionResult != null)
                            {
                                if (currentlyStreamingAuthor != null)
                                {
                                    AnsiConsole.Write(new string(' ', Console.WindowWidth));
                                    AnsiConsole.Markup("\r");
                                    var flushPanel = new Panel(textBuffer.ToString().TrimEnd())
                                        .Header($"[blue]{Markup.Escape(currentlyStreamingAuthor)}[/]")
                                        .BorderColor(Color.Blue)
                                        .Expand();
                                    AnsiConsole.Write(flushPanel);
                                    currentlyStreamingAuthor = null;
                                    textBuffer.Clear();
                                }

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
                
                if (currentlyStreamingThinkingAuthor != null)
                {
                    AnsiConsole.Write(new string(' ', Console.WindowWidth));
                    AnsiConsole.Markup("\r");
                    var thinkPanel = new Panel(thinkingBuffer.ToString().TrimEnd())
                        .Header($"[grey]Thinking ({Markup.Escape(currentlyStreamingThinkingAuthor)})[/]")
                        .BorderColor(Color.Grey)
                        .Expand();
                    AnsiConsole.Write(thinkPanel);
                }

                if (currentlyStreamingAuthor != null)
                {
                    // Clear the "streaming..." line
                    AnsiConsole.Write(new string(' ', Console.WindowWidth));
                    AnsiConsole.Markup("\r");
                            
                    var panel = new Panel(textBuffer.ToString().TrimEnd())
                        .Header($"[blue]{Markup.Escape(currentlyStreamingAuthor)}[/]")
                        .BorderColor(Color.Blue)
                        .Expand();
                    AnsiConsole.Write(panel);
                }

                // Clear the state delta after the first run so it's not repeatedly applied
                currentStateDelta = null;
            }
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("\n[yellow]Session cancelled.[/]");
        }
        finally
        {
            AnsiConsole.MarkupLine("\n[grey]Goodbye![/]");
        }
    }
}
