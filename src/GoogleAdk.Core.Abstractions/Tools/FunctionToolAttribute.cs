namespace GoogleAdk.Core.Abstractions.Tools;

/// <summary>
/// Marks a static method or a top-level/local function as an ADK FunctionTool. A source generator will
/// create the <c>FunctionTool</c> instance automatically, using XML doc
/// comments for the description and parameter metadata.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class FunctionToolAttribute : Attribute
{
    /// <summary>
    /// Optional override for the tool name. Defaults to the method name in snake_case.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Whether this is a long-running tool.
    /// </summary>
    public bool IsLongRunning { get; set; }

    /// <summary>
    /// Whether this tool requires user confirmation before execution.
    /// </summary>
    public bool RequireConfirmation { get; set; }
}
