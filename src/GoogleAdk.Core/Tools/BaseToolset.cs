using GoogleAdk.Core.Agents;

namespace GoogleAdk.Core.Tools;

/// <summary>
/// Predicate to decide whether a tool should be exposed to the LLM.
/// </summary>
public delegate bool ToolPredicate(BaseTool tool, AgentContext context);

/// <summary>
/// Base class for a toolset — a managed collection of tools.
/// Toolsets can dynamically resolve which tools to expose and can process LLM requests.
/// </summary>
public abstract class BaseToolset : GoogleAdk.Core.Abstractions.Events.IBaseTool, IAsyncDisposable
{
    public string Name => GetType().Name;
    public string Description => "A dynamic toolset.";
    public bool IsLongRunning => false;
    public GoogleAdk.Core.Abstractions.Models.FunctionDeclaration? GetDeclaration() => null;
    /// <summary>Optional filter: either a predicate delegate or a list of tool names.</summary>
    public ToolPredicate? ToolFilterPredicate { get; }

    /// <summary>Optional filter: a list of tool names to include.</summary>
    public IReadOnlyList<string>? ToolFilterNames { get; }

    /// <summary>Optional prefix for tool names.</summary>
    public string? Prefix { get; }

    protected BaseToolset(ToolPredicate? toolFilter = null, string? prefix = null)
    {
        ToolFilterPredicate = toolFilter;
        Prefix = prefix;
    }

    protected BaseToolset(IReadOnlyList<string> toolFilterNames, string? prefix = null)
    {
        ToolFilterNames = toolFilterNames;
        Prefix = prefix;
    }

    /// <summary>
    /// Returns the tools that should be exposed to the LLM.
    /// </summary>
    /// <param name="context">Optional context used to filter tools available to the agent.</param>
    public abstract Task<IReadOnlyList<BaseTool>> GetToolsAsync(AgentContext? context = null);

    /// <summary>
    /// Processes the outgoing LLM request for this toolset.
    /// Called before each tool processes the llm request.
    /// </summary>
    public virtual Task ProcessLlmRequestAsync(AgentContext context, Abstractions.Events.LlmRequest llmRequest)
        => Task.CompletedTask;

    /// <summary>
    /// Releases any resources held by this toolset.
    /// </summary>
    public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// Returns whether a tool is selected by the current filter.
    /// </summary>
    protected bool IsToolSelected(BaseTool tool, AgentContext context)
    {
        if (ToolFilterPredicate != null)
            return ToolFilterPredicate(tool, context);

        if (ToolFilterNames != null)
            return ToolFilterNames.Contains(tool.Name);

        return true;
    }
}
