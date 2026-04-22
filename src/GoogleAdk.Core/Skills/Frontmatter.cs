using System.Text.Json.Serialization;

namespace GoogleAdk.Core.Skills;

/// <summary>
/// L1 skill content: metadata parsed from SKILL.md for skill discovery.
/// </summary>
public class Frontmatter
{
    /// <summary>
    /// Skill name in kebab-case or snake_case.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// What the skill does and when the model should use it.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// License for the skill (optional).
    /// </summary>
    [JsonPropertyName("license")]
    public string? License { get; set; }

    /// <summary>
    /// Compatibility information for the skill (optional).
    /// </summary>
    [JsonPropertyName("compatibility")]
    public string? Compatibility { get; set; }

    /// <summary>
    /// A space-delimited list of tools that are pre-approved to run.
    /// </summary>
    [JsonPropertyName("allowed-tools")]
    public string? AllowedTools { get; set; }

    /// <summary>
    /// Key-value pairs for client-specific properties.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();
}
