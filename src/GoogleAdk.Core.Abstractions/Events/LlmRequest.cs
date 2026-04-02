using GoogleAdk.Core.Abstractions.Models;

namespace GoogleAdk.Core.Abstractions.Events;

/// <summary>
/// LLM request class that allows passing in tools, output schema and system
/// instructions to the model.
/// </summary>
public class LlmRequest
{
    /// <summary>The model name.</summary>
    public string? Model { get; set; }

    /// <summary>The contents to send to the model.</summary>
    public List<Content> Contents { get; set; } = new();

    /// <summary>
    /// Additional config for the generate content request.
    /// Tools in config should not be set directly; use AppendTools.
    /// </summary>
    public GenerateContentConfig? Config { get; set; }

    /// <summary>
    /// The tools dictionary, keyed by tool name. Excluded from JSON serialization.
    /// </summary>
    public Dictionary<string, IBaseTool> ToolsDict { get; set; } = new();

    /// <summary>
    /// Appends a single instruction to the system instruction.
    /// </summary>
    public void AppendInstructions(string instruction) => AppendInstructions(new[] { instruction });

    /// <summary>
    /// Appends instructions to the system instruction.
    /// </summary>
    public void AppendInstructions(IEnumerable<string> instructions)
    {
        Config ??= new GenerateContentConfig();
        var newInstructions = string.Join("\n\n", instructions);
        if (!string.IsNullOrEmpty(Config.SystemInstruction))
        {
            Config.SystemInstruction += "\n\n" + newInstructions;
        }
        else
        {
            Config.SystemInstruction = newInstructions;
        }
    }

    /// <summary>
    /// Appends tools to the request.
    /// </summary>
    public void AppendTools(IEnumerable<IBaseTool> tools)
    {
        var functionDeclarations = new List<FunctionDeclaration>();
        foreach (var tool in tools)
        {
            var declaration = tool.GetDeclaration();
            if (declaration != null)
            {
                functionDeclarations.Add(declaration);
                ToolsDict[tool.Name] = tool;
            }
        }

        if (functionDeclarations.Count > 0)
        {
            Config ??= new GenerateContentConfig();
            Config.Tools ??= new List<ToolDeclaration>();
            Config.Tools.Add(new ToolDeclaration { FunctionDeclarations = functionDeclarations });
        }
    }

    /// <summary>
    /// Sets the output schema for the request.
    /// </summary>
    public void SetOutputSchema(Dictionary<string, object?> schema)
    {
        Config ??= new GenerateContentConfig();
        Config.ResponseSchema = schema;
        Config.ResponseMimeType = "application/json";
    }

    /// <summary>
    /// Ensures there is a user content so the model can continue to output.
    /// </summary>
    public void MaybeAppendUserContent()
    {
        if (Contents.Count == 0)
        {
            Contents.Add(new Content
            {
                Role = "user",
                Parts = new List<Part>
                {
                    new() { Text = "Handle the requests as specified in the System Instruction." }
                }
            });
            return;
        }

        if (Contents[^1].Role != "user")
        {
            Contents.Add(new Content
            {
                Role = "user",
                Parts = new List<Part>
                {
                    new() { Text = "Continue processing previous requests as instructed. Exit or provide a summary if no more outputs are needed." }
                }
            });
        }
    }

    /// <summary>
    /// Context cache configuration for this request. (Defined as object to avoid circular references if necessary, or passed via downcast)
    /// </summary>
    public object? CacheConfig { get; set; }

    /// <summary>
    /// The cache metadata attached to the request.
    /// </summary>
    public CacheMetadata? CacheMetadata { get; set; }

    /// <summary>
    /// Token count for cacheable contents.
    /// </summary>
    public int? CacheableContentsTokenCount { get; set; }
}

/// <summary>
/// Marker interface for tools that can be referenced from LlmRequest.
/// </summary>
public interface IBaseTool
{
    string Name { get; }
    string Description { get; }
    bool IsLongRunning { get; }
    FunctionDeclaration? GetDeclaration();
}
