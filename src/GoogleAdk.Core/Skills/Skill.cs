namespace GoogleAdk.Core.Skills;

/// <summary>
/// Complete skill representation including frontmatter, instructions, and resources.
/// </summary>
public class Skill
{
    /// <summary>
    /// Parsed skill frontmatter from SKILL.md.
    /// </summary>
    public Frontmatter Frontmatter { get; set; } = new();

    /// <summary>
    /// L2 skill content: markdown instruction from SKILL.md body.
    /// </summary>
    public string Instructions { get; set; } = string.Empty;

    /// <summary>
    /// L3 skill content: additional instructions, assets, and scripts.
    /// </summary>
    public Resources Resources { get; set; } = new();

    /// <summary>
    /// Convenience property to access skill name.
    /// </summary>
    public string Name => Frontmatter.Name;

    /// <summary>
    /// Convenience property to access skill description.
    /// </summary>
    public string Description => Frontmatter.Description;
}
