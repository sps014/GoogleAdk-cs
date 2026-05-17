using GoogleAdk.Core.Agents;

namespace GoogleAdk.ApiServer;

/// <summary>
/// Builds a JSON-friendly graph representation of an agent hierarchy.
/// Used by the dev UI to visualize agent topology.
/// </summary>
public static class AgentGraphBuilder
{
    // Palette — matches the Python ADK agent_graph.py
    private const string DarkGreen   = "#0F5223";
    private const string LightGreen  = "#69CB87";
    private const string LightGray   = "#cccccc";
    private const string White       = "#ffffff";

    public static AgentGraphNode BuildGraph(BaseAgent agent)
    {
        return BuildNode(agent);
    }

    /// <summary>
    /// Builds a DOT-format graph string with optional highlight pairs.
    /// Matches the Python ADK get_agent_graph behavior.
    /// </summary>
    public static string BuildGraph(BaseAgent agent, List<(string from, string to)>? highlights, bool darkMode = false)
    {
        var highlightSet   = new HashSet<string>();
        var highlightEdges = new HashSet<(string, string)>();

        if (highlights != null)
        {
            foreach (var (from, to) in highlights)
            {
                if (!string.IsNullOrEmpty(from)) highlightSet.Add(from);
                if (!string.IsNullOrEmpty(to))   highlightSet.Add(to);
                if (!string.IsNullOrEmpty(from) && !string.IsNullOrEmpty(to))
                    highlightEdges.Add((from, to));
            }
        }

        var bgColor = darkMode ? "#333537" : White;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("digraph G {");
        sb.AppendLine($"  rankdir=LR;");
        sb.AppendLine($"  bgcolor=\"{bgColor}\";");
        sb.AppendLine($"  node [fontname=\"Helvetica,Arial,sans-serif\"];");
        sb.AppendLine($"  edge [fontname=\"Helvetica,Arial,sans-serif\"];");

        BuildDotAgent(sb, agent, null, highlightSet, highlightEdges, indent: "  ");

        sb.AppendLine("}");
        return sb.ToString();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string GetNodeName(BaseAgent agent) => agent switch
    {
        SequentialAgent => agent.Name + " (Sequential Agent)",
        LoopAgent       => agent.Name + " (Loop Agent)",
        ParallelAgent   => agent.Name + " (Parallel Agent)",
        _               => agent.Name,
    };

    private static bool IsWorkflowAgent(BaseAgent agent) =>
        agent is SequentialAgent or LoopAgent or ParallelAgent;

    private static void BuildDotAgent(
        System.Text.StringBuilder sb,
        BaseAgent agent,
        BaseAgent? parentAgent,
        HashSet<string> highlightSet,
        HashSet<(string, string)> highlightEdges,
        string indent)
    {
        var nodeName = GetNodeName(agent);
        var isHighlighted = highlightSet.Contains(nodeName);

        if (IsWorkflowAgent(agent))
        {
            // Render as a labelled cluster subgraph
            var clusterName = "cluster_" + agent.Name;
            sb.AppendLine($"{indent}subgraph \"{clusterName}\" {{");
            sb.AppendLine($"{indent}  label=\"{EscapeDot(nodeName)}\";");
            sb.AppendLine($"{indent}  style=rounded;");
            sb.AppendLine($"{indent}  color=\"{LightGray}\";");
            sb.AppendLine($"{indent}  fontcolor=\"{LightGray}\";");

            BuildCluster(sb, agent, parentAgent, highlightSet, highlightEdges, indent + "  ");

            sb.AppendLine($"{indent}}}");
        }
        else
        {
            // Plain agent node — ellipse shape, rounded style
            string nodeStyle, nodeFill, nodeBorder, nodeFont;
            if (isHighlighted)
            {
                nodeStyle  = "filled,rounded";
                nodeFill   = DarkGreen;
                nodeBorder = DarkGreen;
                nodeFont   = LightGray;
            }
            else
            {
                nodeStyle  = "rounded";
                nodeFill   = "none";
                nodeBorder = LightGray;
                nodeFont   = LightGray;
            }

            var caption = "\U0001F916 " + agent.Name; // 🤖
            sb.AppendLine($"{indent}\"{EscapeDot(nodeName)}\" [label=\"{EscapeDot(caption)}\", shape=ellipse, style=\"{nodeStyle}\", fillcolor=\"{nodeFill}\", color=\"{nodeBorder}\", fontcolor=\"{nodeFont}\"];");

            // Sub-agents
            foreach (var sub in agent.SubAgents)
            {
                BuildDotAgent(sb, sub, agent, highlightSet, highlightEdges, indent);
                if (!IsWorkflowAgent(sub) && !IsWorkflowAgent(agent))
                    DrawEdge(sb, nodeName, GetNodeName(sub), highlightEdges, isWorkflow: false, indent);
            }

            // Tools for LlmAgent
            if (agent is LlmAgent llm)
            {
                foreach (var tool in llm.Tools)
                {
                    var toolHighlighted = highlightSet.Contains(tool.Name);
                    string tStyle, tFill, tBorder, tFont;
                    if (toolHighlighted)
                    {
                        tStyle  = "filled,rounded";
                        tFill   = DarkGreen;
                        tBorder = DarkGreen;
                        tFont   = LightGray;
                    }
                    else
                    {
                        tStyle  = "rounded";
                        tFill   = "none";
                        tBorder = LightGray;
                        tFont   = LightGray;
                    }

                    var toolCaption = "\U0001F527 " + tool.Name; // 🔧
                    sb.AppendLine($"{indent}\"{EscapeDot(tool.Name)}\" [label=\"{EscapeDot(toolCaption)}\", shape=box, style=\"{tStyle}\", fillcolor=\"{tFill}\", color=\"{tBorder}\", fontcolor=\"{tFont}\"];");
                    DrawEdge(sb, nodeName, tool.Name, highlightEdges, isWorkflow: false, indent);
                }
            }
        }
    }

    private static void BuildCluster(
        System.Text.StringBuilder sb,
        BaseAgent agent,
        BaseAgent? parentAgent,
        HashSet<string> highlightSet,
        HashSet<(string, string)> highlightEdges,
        string indent)
    {
        var subs = agent.SubAgents;
        if (subs.Count == 0) return;

        if (agent is LoopAgent)
        {
            // parentAgent -> first sub, then sequential edges with wrap-around
            if (parentAgent != null)
                DrawEdge(sb, GetNodeName(parentAgent), GetNodeName(subs[0]), highlightEdges, isWorkflow: true, indent);

            for (int i = 0; i < subs.Count; i++)
            {
                BuildDotAgent(sb, subs[i], agent, highlightSet, highlightEdges, indent);
                var next = subs[(i + 1) % subs.Count];
                DrawEdge(sb, GetNodeName(subs[i]), GetNodeName(next), highlightEdges, isWorkflow: true, indent);
            }
        }
        else if (agent is SequentialAgent)
        {
            if (parentAgent != null)
                DrawEdge(sb, GetNodeName(parentAgent), GetNodeName(subs[0]), highlightEdges, isWorkflow: true, indent);

            for (int i = 0; i < subs.Count; i++)
            {
                BuildDotAgent(sb, subs[i], agent, highlightSet, highlightEdges, indent);
                if (i < subs.Count - 1)
                    DrawEdge(sb, GetNodeName(subs[i]), GetNodeName(subs[i + 1]), highlightEdges, isWorkflow: true, indent);
            }
        }
        else // ParallelAgent
        {
            foreach (var sub in subs)
            {
                BuildDotAgent(sb, sub, agent, highlightSet, highlightEdges, indent);
                if (parentAgent != null)
                    DrawEdge(sb, GetNodeName(parentAgent), GetNodeName(sub), highlightEdges, isWorkflow: true, indent);
            }
        }
    }

    private static void DrawEdge(
        System.Text.StringBuilder sb,
        string from,
        string to,
        HashSet<(string, string)> highlightEdges,
        bool isWorkflow,
        string indent)
    {
        foreach (var (hFrom, hTo) in highlightEdges)
        {
            if (from == hFrom && to == hTo)
            {
                sb.AppendLine($"{indent}\"{EscapeDot(from)}\" -> \"{EscapeDot(to)}\" [color=\"{LightGreen}\"];");
                return;
            }
            if (from == hTo && to == hFrom)
            {
                sb.AppendLine($"{indent}\"{EscapeDot(from)}\" -> \"{EscapeDot(to)}\" [color=\"{LightGreen}\", dir=back];");
                return;
            }
        }

        // Non-highlighted
        if (isWorkflow)
            sb.AppendLine($"{indent}\"{EscapeDot(from)}\" -> \"{EscapeDot(to)}\" [color=\"{LightGray}\"];");
        else
            sb.AppendLine($"{indent}\"{EscapeDot(from)}\" -> \"{EscapeDot(to)}\" [arrowhead=none, color=\"{LightGray}\"];");
    }

    private static string EscapeDot(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"");


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
