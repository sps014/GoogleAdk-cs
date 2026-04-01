using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Abstractions.Sessions;
using GoogleAdk.Core.Sessions;
using GoogleAdk.Dev.Server;
using Microsoft.Extensions.FileProviders;
using System.Diagnostics;
using GoogleAdk.Core.Abstractions.Artifacts;
using GoogleAdk.Core.Artifacts;

namespace GoogleAdk.Dev;

/// <summary>
/// Simple static entry point for hosting an agent in the ADK dev server.
/// <code>
/// await AdkWeb.RunAsync(myAgent);
/// </code>
/// </summary>
public static class AdkWeb
{
    /// <summary>
    /// Starts the ADK dev server with the UI, serving the specified root agent.
    /// </summary>
    public static async Task RunAsync(
        BaseAgent rootAgent,
        IBaseArtifactService? artifactService = null,
        int port = 8080, 
        string host = "localhost", 
        bool serveUi = true, 
        bool enableA2a = false)
    {
        var agentLoader = new AgentLoader(".");
        agentLoader.Register(rootAgent.Name, rootAgent);

        var sessionService = new InMemorySessionService();
        artifactService ??= new InMemoryArtifactService();

        var builder = WebApplication.CreateBuilder();

        builder.Services.AddSingleton(agentLoader);
        builder.Services.AddSingleton<BaseSessionService>(sessionService);
        builder.Services.AddSingleton(new RunnerManager(agentLoader, sessionService, artifactService));
        builder.Services.AddSingleton(new InMemoryTraceCollector());

        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
                policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
        });

        var app = builder.Build();
        app.UseCors();
        app.MapAdkApi();
        if (enableA2a)
            app.MapA2aApi();

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
        Console.WriteLine($"  Agent: {rootAgent.Name}");
        if (enableA2a)
            Console.WriteLine($"  A2A: {url}/a2a/{rootAgent.Name}/");
        Console.WriteLine($"  Press Ctrl+C to stop.");
        Console.WriteLine();

        var shutdownToken = DevServerLifetime.Register(app);
        await app.RunAsync(shutdownToken);
    }
}
