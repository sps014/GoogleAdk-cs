using System.CommandLine;
using GoogleAdk.ApiServer.Server;
using GoogleAdk.Core.Sessions;
using GoogleAdk.Core.Abstractions.Sessions;
using Microsoft.Extensions.FileProviders;

namespace GoogleAdk.ApiServer.Cli;

/// <summary>
/// The "web" command — starts the API server with the embedded dev UI.
/// Usage: adk web [agents_dir] [--port 8080] [--host localhost] [--allow-origins *]
/// </summary>
public static class WebCommand
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

        var hostOption = new Option<string>("--bind", "-b")
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

        var command = new Command("web", "Start the ADK dev server with UI.");
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
            await StartServer(agentsDir!, port, host!, origins!, serveUi: true, enableA2a: a2a);
        });

        return command;
    }

    internal static async Task StartServer(
        string agentsDir, int port, string host, string origins, bool serveUi, bool enableA2a = false)
    {
        var builder = WebApplication.CreateBuilder();

        // Services
        var agentLoader = new AgentLoader(agentsDir);
        var sessionService = new InMemorySessionService();

        builder.Services.AddSingleton(agentLoader);
        builder.Services.AddSingleton<BaseSessionService>(sessionService);
        builder.Services.AddSingleton(new RunnerManager(agentLoader, sessionService));
        builder.Services.AddSingleton(new InMemoryTraceCollector());

        // CORS
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                var originList = origins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (originList.Contains("*"))
                    policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
                else
                    policy.WithOrigins(originList).AllowAnyMethod().AllowAnyHeader();
            });
        });

        var app = builder.Build();

        app.UseWebSockets();
        app.UseCors();

        // Map API endpoints
        app.MapAdkApi();
        if (enableA2a)
            app.MapA2aApi();

        // Serve dev UI
        if (serveUi)
        {
            var embeddedProvider = new EmbeddedFileProvider(
                typeof(WebCommand).Assembly, "GoogleAdk.ApiServer.wwwroot");

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

            // Redirect root to dev UI
            app.MapGet("/", () => Results.Redirect("/dev-ui"));
        }

        var url = $"http://{host}:{port}";
        app.Urls.Add(url);

        Console.WriteLine();
        Console.WriteLine("╔══════════════════════════════════════════════════════╗");
        Console.WriteLine("║          Google ADK Dev Server for .NET             ║");
        Console.WriteLine("╠══════════════════════════════════════════════════════╣");
        Console.WriteLine($"║  Server:     {url,-39}║");
        if (serveUi)
            Console.WriteLine($"║  Dev UI:     {url + "/dev-ui",-39}║");
        Console.WriteLine($"║  API:        {url + "/list-apps",-39}║");
        if (enableA2a)
            Console.WriteLine($"║  A2A:        {url + "/a2a/{app}/",-39}║");
        Console.WriteLine($"║  Agents dir: {agentsDir,-39}║");
        Console.WriteLine("╠══════════════════════════════════════════════════════╣");

        var agents = agentLoader.ListAgents();
        if (agents.Count > 0)
        {
            Console.WriteLine($"║  Agents loaded: {agents.Count,-36}║");
            foreach (var name in agents)
                Console.WriteLine($"║    • {name,-47}║");
        }
        else
        {
            Console.WriteLine("║  No agents found. Register agents or provide DLLs. ║");
        }

        Console.WriteLine("╚══════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine("Press Ctrl+C to stop.");
        Console.WriteLine();

        var shutdownToken = DevServerLifetime.Register(app);
        await app.RunAsync(shutdownToken);
    }
}
