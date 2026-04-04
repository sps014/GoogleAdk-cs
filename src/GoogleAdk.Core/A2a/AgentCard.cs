using System.Text.Json;
using System.Text.Json.Serialization;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Tools;
using Task = System.Threading.Tasks.Task;

namespace GoogleAdk.Core.A2a;

public static class AgentCardConstants
{
    public const string AgentCardPath = ".well-known/agent-card.json";
}

public sealed class AgentCard
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; set; } = "0.3.0";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("skills")]
    public List<AgentSkill> Skills { get; set; } = new();

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("preferredTransport")]
    public string PreferredTransport { get; set; } = "JSONRPC";

    [JsonPropertyName("capabilities")]
    public AgentCapabilities Capabilities { get; set; } = new();

    [JsonPropertyName("defaultInputModes")]
    public List<string> DefaultInputModes { get; set; } = new() { "text/plain" };

    [JsonPropertyName("defaultOutputModes")]
    public List<string> DefaultOutputModes { get; set; } = new() { "text/plain" };

    [JsonPropertyName("additionalInterfaces")]
    public List<AgentInterface> AdditionalInterfaces { get; set; } = new();
}

public sealed class AgentCapabilities
{
    [JsonPropertyName("extensions")]
    public List<string> Extensions { get; set; } = new();

    [JsonPropertyName("stateTransitionHistory")]
    public bool StateTransitionHistory { get; set; }

    [JsonPropertyName("pushNotifications")]
    public bool PushNotifications { get; set; }

    [JsonPropertyName("streaming")]
    public bool Streaming { get; set; } = true;
}

public sealed class AgentInterface
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("transport")]
    public string Transport { get; set; } = "JSONRPC";
}

public sealed class AgentSkill
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();
}

public static class AgentCardBuilder
{
    public static async Task<AgentCard> ResolveAgentCardAsync(string source, HttpClient? client = null)
    {
        if (source.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            source.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            client ??= new HttpClient();
            var json = await client.GetStringAsync(source);
            return JsonSerializer.Deserialize<AgentCard>(json) ?? throw new InvalidOperationException("Invalid AgentCard JSON.");
        }

        var content = await File.ReadAllTextAsync(source);
        return JsonSerializer.Deserialize<AgentCard>(content) ?? throw new InvalidOperationException("Invalid AgentCard JSON.");
    }

    public static async Task<AgentCard> GetA2AAgentCardAsync(
        BaseAgent agent,
        IEnumerable<AgentInterface> transports)
    {
        var transportList = transports.ToList();
        var preferred = transportList.FirstOrDefault();
        return new AgentCard
        {
            Name = agent.Name,
            Description = string.IsNullOrWhiteSpace(agent.Description) ? agent.Name : agent.Description,
            ProtocolVersion = "0.3.0",
            Version = "1.0.0",
            Skills = await BuildAgentSkillsAsync(agent),
            Url = preferred?.Url ?? string.Empty,
            PreferredTransport = preferred?.Transport ?? "JSONRPC",
            Capabilities = new AgentCapabilities
            {
                Extensions = new List<string>(),
                StateTransitionHistory = false,
                PushNotifications = false,
                Streaming = true,
            },
            DefaultInputModes = new List<string> { "text/plain" },
            DefaultOutputModes = new List<string> { "text/plain" },
            AdditionalInterfaces = transportList,
        };
    }

    public static async Task<List<AgentSkill>> BuildAgentSkillsAsync(BaseAgent agent)
    {
        var primary = await BuildPrimarySkillsAsync(agent);
        var sub = await BuildSubAgentSkillsAsync(agent);
        return primary.Concat(sub).ToList();
    }

    private static async Task<List<AgentSkill>> BuildPrimarySkillsAsync(BaseAgent agent)
    {
        if (agent is LlmAgent llm)
            return await BuildLlmAgentSkillsAsync(llm);
        return BuildNonLlmAgentSkills(agent);
    }

    private static async Task<List<AgentSkill>> BuildSubAgentSkillsAsync(BaseAgent agent)
    {
        var result = new List<AgentSkill>();
        foreach (var sub in agent.SubAgents)
        {
            var skills = await BuildPrimarySkillsAsync(sub);
            foreach (var subSkill in skills)
            {
                result.Add(new AgentSkill
                {
                    Id = $"{sub.Name}_{subSkill.Id}",
                    Name = $"{sub.Name}: {subSkill.Name}",
                    Description = subSkill.Description,
                    Tags = new List<string> { $"sub_agent:{sub.Name}" }.Concat(subSkill.Tags).ToList(),
                });
            }
        }
        return result;
    }

    private static async Task<List<AgentSkill>> BuildLlmAgentSkillsAsync(LlmAgent agent)
    {
        var skills = new List<AgentSkill>
        {
            new()
            {
                Id = agent.Name,
                Name = "model",
                Description = await BuildDescriptionFromInstructionsAsync(agent),
                Tags = new List<string> { "llm" },
            },
        };

        var tools = await agent.CanonicalToolsAsync();
        foreach (var tool in tools)
        {
            skills.Add(ToolToSkill(agent.Name, tool));
        }

        return skills;
    }

    private static AgentSkill ToolToSkill(string prefix, BaseTool tool)
    {
        var description = tool.Description ?? $"Tool: {tool.Name}";
        return new AgentSkill
        {
            Id = $"{prefix}-{tool.Name}",
            Name = tool.Name,
            Description = description,
            Tags = new List<string> { "llm", "tools" },
        };
    }

    private static List<AgentSkill> BuildNonLlmAgentSkills(BaseAgent agent)
    {
        var skills = new List<AgentSkill>
        {
            new()
            {
                Id = agent.Name,
                Name = GetAgentSkillName(agent),
                Description = BuildAgentDescription(agent),
                Tags = new List<string> { GetAgentTypeTag(agent) },
            },
        };

        if (agent.SubAgents.Count > 0)
        {
            var descriptions = agent.SubAgents.Select(s => s.Description ?? "No description");
            skills.Add(new AgentSkill
            {
                Id = $"{agent.Name}-sub-agents",
                Name = "sub-agents",
                Description = $"Orchestrates: {string.Join("; ", descriptions)}",
                Tags = new List<string> { GetAgentTypeTag(agent), "orchestration" },
            });
        }

        return skills;
    }

    private static string BuildAgentDescription(BaseAgent agent)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(agent.Description))
            parts.Add(agent.Description);

        if (agent.SubAgents.Count > 0)
        {
            if (agent is LoopAgent loop) parts.Add(BuildLoopAgentDescription(loop));
            else if (agent is ParallelAgent parallel) parts.Add(BuildParallelAgentDescription(parallel));
            else if (agent is SequentialAgent sequential) parts.Add(BuildSequentialAgentDescription(sequential));
        }

        return parts.Count > 0 ? string.Join(" ", parts) : GetDefaultAgentDescription(agent);
    }

    private static string BuildSequentialAgentDescription(BaseAgent agent)
    {
        var descriptions = new List<string>();
        for (var i = 0; i < agent.SubAgents.Count; i++)
        {
            var sub = agent.SubAgents[i];
            var subDescription = sub.Description ?? $"execute the {sub.Name} agent";
            if (i == 0) descriptions.Add($"First, this agent will {subDescription}.");
            else if (i == agent.SubAgents.Count - 1) descriptions.Add($"Finally, this agent will {subDescription}.");
            else descriptions.Add($"Then, this agent will {subDescription}.");
        }
        return string.Join(" ", descriptions);
    }

    private static string BuildParallelAgentDescription(BaseAgent agent)
    {
        var descriptions = new List<string>();
        for (var i = 0; i < agent.SubAgents.Count; i++)
        {
            var sub = agent.SubAgents[i];
            var subDescription = sub.Description ?? $"execute the {sub.Name} agent";
            if (i == 0) descriptions.Add($"This agent will {subDescription}");
            else if (i == agent.SubAgents.Count - 1) descriptions.Add($"and {subDescription}");
            else descriptions.Add($", {subDescription}");
        }
        return $"{string.Join(" ", descriptions)} simultaneously.";
    }

    private static string BuildLoopAgentDescription(LoopAgent agent)
    {
        var max = agent.MaxIterations < int.MaxValue ? agent.MaxIterations.ToString() : "unlimited";
        var descriptions = new List<string>();
        for (var i = 0; i < agent.SubAgents.Count; i++)
        {
            var sub = agent.SubAgents[i];
            var subDescription = sub.Description ?? $"execute the {sub.Name} agent";
            if (i == 0) descriptions.Add($"This agent will {subDescription}");
            else if (i == agent.SubAgents.Count - 1) descriptions.Add($"and {subDescription}");
            else descriptions.Add($", {subDescription}");
        }
        return $"{string.Join(" ", descriptions)} in a loop (max {max} iterations).";
    }

    private static async Task<string> BuildDescriptionFromInstructionsAsync(LlmAgent agent)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(agent.Description))
            parts.Add(agent.Description);

        var readonlyContext = new ReadonlyContext(new InvocationContext { Agent = agent, Session = GoogleAdk.Core.Abstractions.Sessions.Session.Create("dummy", "dummy") });
        var (instruction, _) = await agent.ResolveInstructionAsync(readonlyContext);
        if (!string.IsNullOrWhiteSpace(instruction))
            parts.Add(ReplacePronouns(instruction));

        var root = agent.RootAgent;
        if (root is LlmAgent rootLlm)
        {
            var (globalInstruction, _) = await rootLlm.ResolveGlobalInstructionAsync(readonlyContext);
            if (!string.IsNullOrWhiteSpace(globalInstruction))
                parts.Add(ReplacePronouns(globalInstruction));
        }

        return parts.Count > 0 ? string.Join(" ", parts) : GetDefaultAgentDescription(agent);
    }

    private static string ReplacePronouns(string instruction)
    {
        var substitutions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["you were"] = "I was",
            ["you are"] = "I am",
            ["you're"] = "I am",
            ["you've"] = "I have",
            ["yours"] = "mine",
            ["your"] = "my",
            ["you"] = "I",
        };

        var result = instruction;
        foreach (var (original, target) in substitutions)
        {
            result = System.Text.RegularExpressions.Regex.Replace(
                result,
                $@"\b{System.Text.RegularExpressions.Regex.Escape(original)}\b",
                target,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        return result;
    }

    private static string GetDefaultAgentDescription(BaseAgent agent) =>
        agent switch
        {
            LoopAgent => "A loop workflow agent",
            SequentialAgent => "A sequential workflow agent",
            ParallelAgent => "A parallel workflow agent",
            LlmAgent => "An LLM-based agent",
            _ => "A custom agent",
        };

    private static string GetAgentTypeTag(BaseAgent agent) =>
        agent switch
        {
            LoopAgent => "loop_workflow",
            SequentialAgent => "sequential_workflow",
            ParallelAgent => "parallel_workflow",
            LlmAgent => "llm_agent",
            _ => "custom_agent",
        };

    private static string GetAgentSkillName(BaseAgent agent)
    {
        if (agent is LlmAgent) return "model";
        if (agent is LoopAgent || agent is SequentialAgent || agent is ParallelAgent) return "workflow";
        return "custom";
    }
}

