using GoogleAdk.Core.Agents;

namespace GoogleAdk.ApiServer;

/// <summary>
/// Builds a JSON-friendly graph representation of an agent hierarchy.
/// Used by the dev UI to visualize agent topology.
/// </summary>
public static class AgentGraphBuilder
{
    public static AgentGraphNode BuildGraph(BaseAgent agent)
    {
        return BuildNode(agent);
    }

    /// <summary>
    /// Builds a DOT-format graph string with optional highlight pairs.
    /// Matches the JS ADK getAgentGraphAsDot behavior.
    /// </summary>
    public static string BuildGraph(BaseAgent agent, List<(string from, string to)>? highlights)
    {
        var highlightSet = new HashSet<string>();
        var highlightEdges = new HashSet<(string, string)>();

        if (highlights != null)
        {
            foreach (var (from, to) in highlights)
            {
                if (!string.IsNullOrEmpty(from)) highlightSet.Add(from);
                if (!string.IsNullOrEmpty(to)) highlightSet.Add(to);
                if (!string.IsNullOrEmpty(from) && !string.IsNullOrEmpty(to))
                    highlightEdges.Add((from, to));
            }
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("digraph G {");
        sb.AppendLine("  rankdir=TB;");
        sb.AppendLine("  node [shape=box, style=filled, fillcolor=\"#ffffff\", color=\"#cccccc\"];");
        sb.AppendLine("  edge [color=\"#cccccc\"];");

        BuildDotNode(sb, agent, highlightSet, highlightEdges);

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void BuildDotNode(
        System.Text.StringBuilder sb,
        BaseAgent agent,
        HashSet<string> highlightSet,
        HashSet<(string, string)> highlightEdges)
    {
        var isHighlighted = highlightSet.Contains(agent.Name);
        var fillColor = isHighlighted ? "#69CB87" : "#ffffff";
        var borderColor = isHighlighted ? "#0F5223" : "#cccccc";

        var label = agent.Name;

        sb.AppendLine($"  \"{agent.Name}\" [label=\"{label}\", fillcolor=\"{fillColor}\", color=\"{borderColor}\"];");

        foreach (var sub in agent.SubAgents)
        {
            var edgeHighlighted = highlightEdges.Contains((agent.Name, sub.Name))
                                || highlightEdges.Contains((sub.Name, agent.Name));
            var edgeColor = edgeHighlighted ? "#0F5223" : "#cccccc";
            sb.AppendLine($"  \"{agent.Name}\" -> \"{sub.Name}\" [color=\"{edgeColor}\"];");

            BuildDotNode(sb, sub, highlightSet, highlightEdges);
        }

        // Also add tool nodes for LlmAgent
        if (agent is LlmAgent llm2)
        {
            foreach (var tool in llm2.Tools)
            {
                var toolHighlighted = highlightSet.Contains(tool.Name);
                var toolFill = toolHighlighted ? "#69CB87" : "#ffffff";
                var toolBorder = toolHighlighted ? "#0F5223" : "#cccccc";
                sb.AppendLine($"  \"{tool.Name}\" [label=\"{tool.Name}\", shape=ellipse, fillcolor=\"{toolFill}\", color=\"{toolBorder}\"];");

                var toolEdgeHighlighted = highlightEdges.Contains((agent.Name, tool.Name))
                                        || highlightEdges.Contains((tool.Name, agent.Name));
                var toolEdgeColor = toolEdgeHighlighted ? "#0F5223" : "#cccccc";
                sb.AppendLine($"  \"{agent.Name}\" -> \"{tool.Name}\" [color=\"{toolEdgeColor}\", style=dashed];");
            }
        }
    }

    private static AgentGraphNode BuildNode(BaseAgent agent)
    {
        var node = new AgentGraphNode
        {
            Name = agent.Name,
            Description = agent.Description,
            Type = agent.GetType().Name,
        };

        // Add tools for LlmAgent
        if (agent is LlmAgent llm)
        {
            foreach (var tool in llm.Tools)
            {
                node.Tools.Add(new AgentGraphTool
                {
                    Name = tool.Name,
                    Description = tool.Description,
                });
            }
        }

        // Recurse into sub-agents
        foreach (var sub in agent.SubAgents)
        {
            node.Children.Add(BuildNode(sub));
        }

        return node;
    }
}

public class AgentGraphNode
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Type { get; set; } = string.Empty;
    public List<AgentGraphTool> Tools { get; set; } = new();
    public List<AgentGraphNode> Children { get; set; } = new();
}

public class AgentGraphTool
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
