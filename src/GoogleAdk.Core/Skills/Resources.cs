namespace GoogleAdk.Core.Skills;

/// <summary>
/// L3 skill content: additional instructions, assets, and scripts.
/// </summary>
public class Resources
{
    /// <summary>
    /// Additional markdown files with instructions, workflows, or guidance.
    /// </summary>
    public Dictionary<string, object> References { get; set; } = new();

    /// <summary>
    /// Resource materials like database schemas, API documentation, templates, or examples.
    /// </summary>
    public Dictionary<string, object> Assets { get; set; } = new();

    /// <summary>
    /// Executable scripts that can be run via bash or python.
    /// </summary>
    public Dictionary<string, Script> Scripts { get; set; } = new();

    /// <summary>
    /// Get content of a reference file.
    /// </summary>
    /// <param name="referenceId">Unique path or name of the reference file.</param>
    /// <returns>Reference content as string or byte array, or null if not found.</returns>
    public object? GetReference(string referenceId) => 
        References.TryGetValue(referenceId, out var content) ? content : null;

    /// <summary>
    /// Get content of an asset file.
    /// </summary>
    /// <param name="assetId">Unique path or name of the asset file.</param>
    /// <returns>Asset content as string or byte array, or null if not found.</returns>
    public object? GetAsset(string assetId) => 
        Assets.TryGetValue(assetId, out var content) ? content : null;

    /// <summary>
    /// Get content of a script file.
    /// </summary>
    /// <param name="scriptId">Unique path or name of the script file.</param>
    /// <returns>Script object, or null if not found.</returns>
    public Script? GetScript(string scriptId) => 
        Scripts.TryGetValue(scriptId, out var script) ? script : null;

    /// <summary>
    /// List all available reference paths.
    /// </summary>
    public IEnumerable<string> ListReferences() => References.Keys;

    /// <summary>
    /// List all available asset paths.
    /// </summary>
    public IEnumerable<string> ListAssets() => Assets.Keys;

    /// <summary>
    /// List all available script paths.
    /// </summary>
    public IEnumerable<string> ListScripts() => Scripts.Keys;
}
