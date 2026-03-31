// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Abstractions.Sessions;
using GoogleAdk.Core.Sessions;
using GoogleAdk.Dev.Server;
using Microsoft.Extensions.FileProviders;
using System.Diagnostics;

namespace GoogleAdk.Dev;

/// <summary>
/// Simple static entry point for hosting an agent in the ADK dev server.
/// <code>
/// AdkWeb.Root = myAgent;
/// await AdkWeb.RunAsync();
/// </code>
/// </summary>
public static class AdkWeb
{
    private static BaseAgent? _root;

    /// <summary>
    /// The root agent to serve. Must be set before calling <see cref="RunAsync"/>.
    /// </summary>
    public static BaseAgent Root
    {
        get => _root ?? throw new InvalidOperationException("AdkWeb.Root has not been set. Assign your root agent before calling RunAsync().");
        set => _root = value;
    }

    /// <summary>
    /// Starts the ADK dev server with the UI, serving <see cref="Root"/>.
    /// </summary>
    public static Task RunAsync(int port = 8080, string host = "localhost", bool serveUi = true)
    {
        var agent = Root; // will throw if not set
        var agentLoader = new AgentLoader(".");
        agentLoader.Register(agent.Name, agent);

        var sessionService = new InMemorySessionService();

        var builder = WebApplication.CreateBuilder();

        builder.Services.AddSingleton(agentLoader);
        builder.Services.AddSingleton<BaseSessionService>(sessionService);
        builder.Services.AddSingleton(new RunnerManager(agentLoader, sessionService));
        builder.Services.AddSingleton(new InMemoryTraceCollector());

        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
                policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
        });

        var app = builder.Build();
        app.UseCors();
        app.MapAdkApi();

        if (serveUi)
        {
            var embeddedProvider = new EmbeddedFileProvider(
                typeof(AdkWeb).Assembly, "GoogleAdk.Dev.wwwroot");

            app.UseDefaultFiles(new DefaultFilesOptions
            {
                FileProvider = embeddedProvider,
                RequestPath = "/dev-ui",
            });
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = embeddedProvider,
                RequestPath = "/dev-ui",
                ServeUnknownFileTypes = false,
            });
            app.MapGet("/", () => Results.Redirect("/dev-ui"));
        }

        var url = $"http://{host}:{port}";
        app.Urls.Add(url);

        Console.WriteLine();
        Console.WriteLine($"  ADK Dev Server running at {url}");
        Console.WriteLine($"  Dev UI: {url}/dev-ui");
        Console.WriteLine($"  Agent: {agent.Name}");
        Console.WriteLine($"  Press Ctrl+C to stop.");
        Console.WriteLine();

        return app.RunAsync();
    }
}
