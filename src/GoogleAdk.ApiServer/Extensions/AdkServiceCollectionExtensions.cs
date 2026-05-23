using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Sessions;
using GoogleAdk.Core.Artifacts;
using GoogleAdk.Core.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;

namespace GoogleAdk.ApiServer;

public static class AdkServiceCollectionExtensions
{
    public static IServiceCollection AddAdk(
        this IServiceCollection services,
        BaseAgent rootAgent,
        Action<AdkServerOptions>? configureOptions = null)
    {
        var options = new AdkServerOptions();
        configureOptions?.Invoke(options);

        // Add AdkServerOptions to DI for later use in middleware/endpoints
        services.AddSingleton(options);

        // We build an intermediate provider to get the logger for AgentLoader.
        var sp = services.BuildServiceProvider();
        var agentLoaderLogger = sp.GetService<Microsoft.Extensions.Logging.ILogger<AgentLoader>>();
        
        var agentLoader = new AgentLoader(options.AgentsDirectory, agentLoaderLogger);
        agentLoader.Register(rootAgent.Name, rootAgent);

        var sessionService = new InMemorySessionService();
        var artifactService = options.ArtifactService ?? new InMemoryArtifactService();

        if (options.EnableCloudTracing)
        {
            services.AddOpenTelemetry()
                .WithTracing(tracing => tracing
                    .AddSource(AdkTracing.ActivitySource.Name)
                    .AddGoogleCloudTracing());
        }

        services.AddSingleton(agentLoader);
        services.AddSingleton<GoogleAdk.Core.Abstractions.Sessions.BaseSessionService>(sessionService);
        services.AddSingleton(new RunnerManager(agentLoader, sessionService, artifactService, options.MemoryService, options.InitialState));
        services.AddSingleton(new InMemoryTraceCollector());

        services.AddEndpointsApiExplorer();
        
        if (options.ShowSwaggerUI)
        {
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "ADK Server API", Version = "v1" });
            });
        }

        services.AddCors(corsOptions =>
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

        return services;
    }
}
