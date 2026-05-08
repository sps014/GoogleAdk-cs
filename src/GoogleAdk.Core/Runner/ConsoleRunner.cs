using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Abstractions.Sessions;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Artifacts;
using GoogleAdk.Core.Memory;
using GoogleAdk.Core.Sessions;
using Spectre.Console;
using Spectre.Console.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GoogleAdk.Core.Runner;

/// <summary>
/// A built-in runner that provides a beautiful CLI interface using Spectre.Console.
/// </summary>
public static partial class ConsoleRunner
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

        var state = new ConsoleRenderState
        {
            CurrentAgent = rootAgent.Name
        };

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
            Environment.Exit(0);
        };

        try
        {
            var stagedFiles = new List<Part>();
            var currentStateDelta = initialState;
            bool isFirstTurn = true;

            while (!cts.Token.IsCancellationRequested)
            {
                string? input = null;

                if (isFirstTurn && !string.IsNullOrEmpty(config.InitialMessage))
                {
                    input = config.InitialMessage;
                    AnsiConsole.MarkupLine($"[green]You:[/] {Markup.Escape(input)}");
                }
                else
                {
                    if (config.CloseOnFinish && !isFirstTurn)
                    {
                        break;
                    }

                    AnsiConsole.Markup("[green]You:[/] ");
                    input = Console.ReadLine();
                }

                isFirstTurn = false;
                
                if (input == null) break; // Handle EOF (Ctrl+D / Ctrl+Z)
                if (string.IsNullOrWhiteSpace(input)) continue;
                if (input.Trim().Equals("/bye", StringComparison.OrdinalIgnoreCase)) break;

                if (input.StartsWith("/attach ", StringComparison.OrdinalIgnoreCase))
                {
                    await HandleAttachCommandAsync(input, stagedFiles, cts.Token);
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

                state.StreamingAuthor = null;
                state.TextBuffer.Clear();
                state.StreamingThinkingAuthor = null;
                state.ThinkingBuffer.Clear();

                await foreach (var evt in runner.RunAsync(userId, sessionId, content, currentStateDelta, runConfig, cts.Token))
                {
                    ProcessEvent(evt, config, state);
                }
                
                FlushThinkingBuffer(state);
                FlushTextBuffer(state);

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
