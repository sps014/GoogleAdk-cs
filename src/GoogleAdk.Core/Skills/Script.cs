using System.Text.Json.Serialization;

namespace GoogleAdk.Core.Skills;

/// <summary>
/// Wrapper for script content.
/// </summary>
public class Script
{
    /// <summary>
    /// The source code of the script.
    /// </summary>
    [JsonPropertyName("src")]
    public string Src { get; set; } = string.Empty;

    public override string ToString() => Src;
}
