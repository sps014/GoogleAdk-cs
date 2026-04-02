using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Context;
using GoogleAdk.Core.Plugins;

namespace GoogleAdk.Core.Apps;

/// <summary>
/// Bundles a root agent with app-level plugins and configuration.
/// </summary>
public sealed class AdkApp
{
    public string Name { get; }
    public BaseAgent RootAgent { get; }
    public IEnumerable<BasePlugin>? Plugins { get; set; }
    public EventsCompactionConfig? EventsCompactionConfig { get; set; }
    public ContextCacheConfig? ContextCacheConfig { get; set; }
    public ResumabilityConfig? ResumabilityConfig { get; set; }

    public AdkApp(string name, BaseAgent rootAgent)
    {
        Name = name;
        RootAgent = rootAgent;
    }
}

public sealed class EventsCompactionConfig
{
    public List<IContextCompactor>? Compactors { get; set; }
}

public sealed class ResumabilityConfig
{
    public bool Enable { get; set; }
}
