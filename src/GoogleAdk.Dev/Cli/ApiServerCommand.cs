// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using System.CommandLine;

namespace GoogleAdk.Dev.Cli;

/// <summary>
/// The "api_server" command — starts the API server without the dev UI.
/// Usage: adk api_server [agents_dir] [--port 8080] [--bind localhost]
/// </summary>
public static class ApiServerCommand
{
    public static Command Create()
    {
        var agentsDirArg = new Argument<string>("agents_dir")
        {
            Description = "Directory containing agent assemblies or projects.",
            DefaultValueFactory = _ => ".",
        };

        var portOption = new Option<int>("--port", "-p")
        {
            Description = "Port to listen on.",
            DefaultValueFactory = _ => 8080,
        };

        var hostOption = new Option<string>("--bind")
        {
            Description = "Host to bind to.",
            DefaultValueFactory = _ => "localhost",
        };

        var originsOption = new Option<string>("--allow-origins")
        {
            Description = "CORS allowed origins (comma-separated).",
            DefaultValueFactory = _ => "*",
        };

        var a2aOption = new Option<bool>("--a2a")
        {
            Description = "Enable A2A protocol endpoints.",
            DefaultValueFactory = _ => false,
        };

        var command = new Command("api_server", "Start the ADK API server (no UI).");
        command.Arguments.Add(agentsDirArg);
        command.Options.Add(portOption);
        command.Options.Add(hostOption);
        command.Options.Add(originsOption);
        command.Options.Add(a2aOption);

        command.SetAction(async parseResult =>
        {
            var agentsDir = parseResult.GetValue(agentsDirArg);
            var port = parseResult.GetValue(portOption);
            var host = parseResult.GetValue(hostOption);
            var origins = parseResult.GetValue(originsOption);
            var a2a = parseResult.GetValue(a2aOption);
            await WebCommand.StartServer(agentsDir!, port, host!, origins!, serveUi: false, enableA2a: a2a);
        });

        return command;
    }
}
