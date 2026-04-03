using GoogleAdk.Core.Agents;

namespace GoogleAdk.ApiServer;

/// <summary>
/// Discovers and loads agent assemblies from a directory.
/// Agents are .NET assemblies (DLLs) that export a static property or method returning a BaseAgent.
/// 
/// Convention:
///   agents_dir/
///     MyAgent/
///       MyAgent.dll          ← compiled agent assembly
///     MyAgent.dll            ← or flat DLL
/// 
/// Each assembly is scanned for a public static property named "RootAgent" of type BaseAgent,
/// or a public static method "CreateRootAgent()" returning BaseAgent.
/// </summary>
public class AgentLoader
{
    private readonly string _agentsDir;
    private readonly Dictionary<string, AgentEntry> _cache = new(StringComparer.OrdinalIgnoreCase);

    public AgentLoader(string agentsDir)
    {
        _agentsDir = Path.GetFullPath(agentsDir);
    }

    /// <summary>Lists all discovered agent names.</summary>
    public List<string> ListAgents()
    {
        EnsureLoaded();
        return _cache.Keys.OrderBy(k => k).ToList();
    }

    /// <summary>Gets the root agent for the given app name.</summary>
    public BaseAgent GetAgent(string appName)
    {
        EnsureLoaded();
        if (!_cache.TryGetValue(appName, out var entry))
            throw new KeyNotFoundException($"Agent '{appName}' not found. Available: {string.Join(", ", _cache.Keys)}");
        return entry.Agent;
    }

    /// <summary>Registers an agent directly (useful for programmatic setup).</summary>
    public void Register(string name, BaseAgent agent)
    {
        _cache[name] = new AgentEntry(name, agent, null);
    }

    private void EnsureLoaded()
    {
        if (_cache.Count > 0) return;
        if (!Directory.Exists(_agentsDir)) return;

        // Scan for .dll files
        var dllFiles = new List<(string name, string path)>();

        // Check subdirectories first (convention: folder name = agent name)
        foreach (var dir in Directory.GetDirectories(_agentsDir))
        {
            var dirName = Path.GetFileName(dir);
            var dllPath = Path.Combine(dir, $"{dirName}.dll");
            if (File.Exists(dllPath))
                dllFiles.Add((dirName, dllPath));
        }

        // Then check flat DLLs
        foreach (var dll in Directory.GetFiles(_agentsDir, "*.dll"))
        {
            var name = Path.GetFileNameWithoutExtension(dll);
            if (!_cache.ContainsKey(name))
                dllFiles.Add((name, dll));
        }

        foreach (var (name, path) in dllFiles)
        {
            try
            {
                var agent = LoadAgentFromAssembly(path);
                if (agent != null)
                    _cache[name] = new AgentEntry(name, agent, path);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[AgentLoader] Failed to load '{name}' from {path}: {ex.Message}");
            }
        }
    }

    private static BaseAgent? LoadAgentFromAssembly(string dllPath)
    {
        var context = new System.Runtime.Loader.AssemblyLoadContext(
            Path.GetFileNameWithoutExtension(dllPath), isCollectible: false);
        var assembly = context.LoadFromAssemblyPath(dllPath);

        foreach (var type in assembly.GetExportedTypes())
        {
            // Look for static "RootAgent" property
            var prop = type.GetProperty("RootAgent",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (prop != null && typeof(BaseAgent).IsAssignableFrom(prop.PropertyType))
                return (BaseAgent?)prop.GetValue(null);

            // Look for static "CreateRootAgent()" method
            var method = type.GetMethod("CreateRootAgent",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                Type.EmptyTypes);
            if (method != null && typeof(BaseAgent).IsAssignableFrom(method.ReturnType))
                return (BaseAgent?)method.Invoke(null, null);
        }

        return null;
    }

    private record AgentEntry(string Name, BaseAgent Agent, string? AssemblyPath);
}
