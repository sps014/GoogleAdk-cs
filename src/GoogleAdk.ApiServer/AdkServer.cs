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
using GoogleAdk.Core.A2a;

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
    /// Starts the ADK dev server with the UI, serving the specified root agent with default options.
    /// </summary>
    public static Task RunAsync(BaseAgent rootAgent) 
        => RunAsync(rootAgent, new AdkServerOptions());

    /// <summary>
    /// Starts the ADK dev server with the UI, serving the specified root agent with configured options.
    /// </summary>
    public static Task RunAsync(BaseAgent rootAgent, Action<AdkServerOptions> configureOptions)
    {
        var options = new AdkServerOptions();
        configureOptions?.Invoke(options);
        return RunAsync(rootAgent, options);
    }

    /// <summary>
    /// Starts the ADK dev server with the UI, serving the specified root agent with explicitly provided options.
    /// </summary>
    public static async Task RunAsync(BaseAgent rootAgent, AdkServerOptions options)
    {
        var agentLoader = new AgentLoader(".");
        agentLoader.Register(rootAgent.Name, rootAgent);

        var sessionService = new InMemorySessionService();
        var artifactService = options.ArtifactService ?? new InMemoryArtifactService();

        var builder = WebApplication.CreateBuilder();

        if (options.EnableCloudTracing)
        {
            builder.Services.AddOpenTelemetry()
                .WithTracing(tracing => tracing
                    .AddSource(AdkTracing.ActivitySource.Name)
                    .AddGoogleCloudTracing());
        }

        builder.Services.AddSingleton(agentLoader);
        builder.Services.AddSingleton<BaseSessionService>(sessionService);
        builder.Services.AddSingleton(new RunnerManager(agentLoader, sessionService, artifactService, options.MemoryService, options.InitialState));
        builder.Services.AddSingleton(new InMemoryTraceCollector());

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "ADK Server API", Version = "v1" });
        });

        builder.Services.AddCors(corsOptions =>
        {
            corsOptions.AddDefaultPolicy(policy =>
            {
                if (options.ConfigureCors != null)
                {
                    options.ConfigureCors(policy);
                }
                else
                {
                    policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
                }
            });
        });

        var app = builder.Build();

        if (options.ShowSwaggerUI)
        {
            app.UseSwagger(c =>
            {
                c.SerializeAsV2 = true;
            });
            app.UseSwaggerUI(c =>
            {
                c.RoutePrefix = "swagger";
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "ADK Server API v1");
            });
        }

        app.UseWebSockets();
        app.UseCors();
        app.MapAdkApi();
        if (options.EnableA2a)
            app.MapA2aApi();

        if (options.ShowAdkWebUI)
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

        var url = $"http://{options.Host}:{options.Port}";
        app.Urls.Add(url);

        var grid = new Grid()
            .AddColumn(new GridColumn().NoWrap().PadRight(2))
            .AddColumn();

        grid.AddRow("[bold cyan]Server[/]", $"[link={url}]{url}[/]");


        if (options.ShowSwaggerUI)
            grid.AddRow("[bold yellow]Swagger UI[/]", $"[link={url}/swagger]{url}/swagger[/]");
        grid.AddRow("[bold magenta]Agent[/]", rootAgent.Name);

        if (options.EnableA2a)
            grid.AddRow("[bold blue]A2A[/]", $"[link={url}/a2a/{rootAgent.Name}/{AgentCardConstants.AgentCardPath}]{url}/a2a/{rootAgent.Name}/{AgentCardConstants.AgentCardPath}[/]");

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
