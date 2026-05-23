using System.Collections.Concurrent;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Runner;
using GoogleAdk.Core.Abstractions.Sessions;
using GoogleAdk.Core.Abstractions.Artifacts;
using GoogleAdk.Core.Abstractions.Memory;
using Microsoft.Extensions.Logging;

namespace GoogleAdk.ApiServer;

/// <summary>
/// Manages Runner instances, caching one per agent app.
/// </summary>
public class RunnerManager
{
    private readonly ConcurrentDictionary<string, Runner> _runners = new(StringComparer.OrdinalIgnoreCase);
    private readonly AgentLoader _agentLoader;
    private readonly BaseSessionService _sessionService;
    private readonly IBaseArtifactService? _artifactService;
    private readonly IBaseMemoryService? _memoryService;
    private readonly Dictionary<string, object?>? _initialState;
    private readonly ILogger<RunnerManager>? _logger;

    public RunnerManager(
        AgentLoader agentLoader, 
        BaseSessionService sessionService, 
        IBaseArtifactService? artifactService = null, 
        IBaseMemoryService? memoryService = null, 
        Dictionary<string, object?>? initialState = null,
        ILogger<RunnerManager>? logger = null)
    {
        _agentLoader = agentLoader;
        _sessionService = sessionService;
        _artifactService = artifactService;
        _memoryService = memoryService;
        _initialState = initialState;
        _logger = logger;
    }

    public Runner GetOrCreate(string appName)
    {
        return _runners.GetOrAdd(appName, name =>
        {
            var agent = _agentLoader.GetAgent(name);
            return new Runner(new RunnerConfig
            {
                AppName = name,
                Agent = agent,
                SessionService = _sessionService,
                ArtifactService = _artifactService,
                MemoryService = _memoryService,
                InitialState = _initialState
            });
        });
    }

    public BaseSessionService SessionService => _sessionService;
    public IBaseArtifactService? ArtifactService => _artifactService;
    public IBaseMemoryService? MemoryService => _memoryService;
    public Dictionary<string, object?>? InitialState => _initialState;
}
