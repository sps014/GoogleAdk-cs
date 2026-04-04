using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Abstractions.Sessions;
using GoogleAdk.Core.Sessions;
using Microsoft.Extensions.FileProviders;
using System.Diagnostics;
using GoogleAdk.Core.Abstractions.Artifacts;
using GoogleAdk.Core.Abstractions.Memory;
using GoogleAdk.Core.Artifacts;
using GoogleAdk.Core.Telemetry;
using OpenTelemetry.Trace;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace GoogleAdk.ApiServer;

/// <summary>
/// Simple static entry point for hosting an agent in the ADK dev server.
/// <code>
/// await AdkServer.RunAsync(myAgent);
/// </code>
/// </summary>
public static class AdkServer
{
    /// <summary>
    /// Starts the ADK dev server with the UI, serving the specified root agent.
    /// </summary>
    public static async Task RunAsync(
        BaseAgent rootAgent,
        IBaseArtifactService? artifactService = null,
        IBaseMemoryService? memoryService = null,
        int port = 8080, 
        string host = "localhost", 
        bool showAdkWebUI = true, 
        bool showSwaggerUI = true,
        bool enableA2a = false,
        Dictionary<string, object?>? initialState = null,
        bool enableCloudTracing = false)
    {
        var agentLoader = new AgentLoader(".");
        agentLoader.Register(rootAgent.Name, rootAgent);

        var sessionService = new InMemorySessionService();
        artifactService ??= new InMemoryArtifactService();

        var builder = WebApplication.CreateBuilder();

        if (enableCloudTracing)
        {
            builder.Services.AddOpenTelemetry()
                .WithTracing(tracing => tracing
                    .AddSource(AdkTracing.ActivitySource.Name)
                    .AddGoogleCloudTracing());
        }

        builder.Services.AddSingleton(agentLoader);
        builder.Services.AddSingleton<BaseSessionService>(sessionService);
        builder.Services.AddSingleton(new RunnerManager(agentLoader, sessionService, artifactService, memoryService, initialState));
        builder.Services.AddSingleton(new InMemoryTraceCollector());

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "ADK Server API", Version = "v1" });
        });

        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
                policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
        });

        var app = builder.Build();
        
        if (showSwaggerUI)
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "ADK Server API v1");
            });
        }
        
        app.UseWebSockets();
        app.UseCors();
        app.MapAdkApi();
        if (enableA2a)
            app.MapA2aApi();

        if (showAdkWebUI)
        {
            var embeddedProvider = new EmbeddedFileProvider(
                typeof(AdkServer).Assembly, "GoogleAdk.ApiServer.wwwroot");

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

        var grid = new Grid()
            .AddColumn(new GridColumn().NoWrap().PadRight(2))
            .AddColumn();

        grid.AddRow("[bold cyan]Server[/]", $"[link={url}]{url}[/]");
        
        if (showAdkWebUI)
            grid.AddRow("[bold green]Dev UI[/]", $"[link={url}/dev-ui]{url}/dev-ui[/]");
            
        if (showSwaggerUI)
            grid.AddRow("[bold yellow]Swagger UI[/]", $"[link={url}/swagger]{url}/swagger[/]");
        grid.AddRow("[bold magenta]Agent[/]", rootAgent.Name);
        
        if (enableA2a)
            grid.AddRow("[bold blue]A2A[/]", $"[link={url}/a2a/{rootAgent.Name}/]{url}/a2a/{rootAgent.Name}/[/]");

        AnsiConsole.WriteLine();
        AnsiConsole.Write(
            new Panel(grid)
                .Header("[bold white]ADK Dev Server[/]")
                .Border(BoxBorder.Rounded)
                .Expand()
        );
        AnsiConsole.MarkupLine("[dim italic]Press Ctrl+C to stop.[/]");
        AnsiConsole.WriteLine();

        var shutdownToken = DevServerLifetime.Register(app);
        await app.RunAsync(shutdownToken);
    }
}
