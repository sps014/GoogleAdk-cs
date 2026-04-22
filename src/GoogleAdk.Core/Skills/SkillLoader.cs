using System.Text.Json;
using System.Text.Json.Serialization;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace GoogleAdk.Core.Skills;

/// <summary>
/// Utility to load a Skill from a directory.
/// </summary>
public static class SkillLoader
{
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>
    /// Loads a skill from a specific directory path.
    /// The directory must contain a SKILL.md file.
    /// </summary>
    /// <param name="skillPath">The absolute or relative path to the skill directory.</param>
    /// <returns>A loaded Skill object.</returns>
    public static Skill LoadFromDirectory(string skillPath)
    {
        if (!Directory.Exists(skillPath))
        {
            throw new DirectoryNotFoundException($"Skill directory not found: {skillPath}");
        }

        var skillMdPath = Path.Combine(skillPath, "SKILL.md");
        if (!File.Exists(skillMdPath))
        {
            throw new FileNotFoundException($"SKILL.md not found in skill directory: {skillPath}");
        }

        var content = File.ReadAllText(skillMdPath);
        var (frontmatter, instructions) = ParseSkillMd(content);

        var skill = new Skill
        {
            Frontmatter = frontmatter,
            Instructions = instructions
        };

        // Load references
        var referencesPath = Path.Combine(skillPath, "references");
        if (Directory.Exists(referencesPath))
        {
            foreach (var file in Directory.GetFiles(referencesPath, "*.*", SearchOption.AllDirectories))
            {
                var relPath = GetRelativePath(referencesPath, file).Replace("\\", "/");
                skill.Resources.References[relPath] = LoadFileContent(file);
            }
        }

        // Load assets
        var assetsPath = Path.Combine(skillPath, "assets");
        if (Directory.Exists(assetsPath))
        {
            foreach (var file in Directory.GetFiles(assetsPath, "*.*", SearchOption.AllDirectories))
            {
                var relPath = GetRelativePath(assetsPath, file).Replace("\\", "/");
                skill.Resources.Assets[relPath] = LoadFileContent(file);
            }
        }

        // Load scripts
        var scriptsPath = Path.Combine(skillPath, "scripts");
        if (Directory.Exists(scriptsPath))
        {
            foreach (var file in Directory.GetFiles(scriptsPath, "*.*", SearchOption.AllDirectories))
            {
                var relPath = GetRelativePath(scriptsPath, file).Replace("\\", "/");
                skill.Resources.Scripts[relPath] = new Script { Src = File.ReadAllText(file) };
            }
        }

        return skill;
    }

    private static (Frontmatter, string) ParseSkillMd(string content)
    {
        // Simple frontmatter parsing
        var frontmatterEndIndex = -1;
        if (content.StartsWith("---\r\n") || content.StartsWith("---\n"))
        {
            var searchStart = content.IndexOf('\n') + 1;
            frontmatterEndIndex = content.IndexOf("---\r\n", searchStart);
            if (frontmatterEndIndex == -1)
            {
                frontmatterEndIndex = content.IndexOf("---\n", searchStart);
            }
        }

        if (frontmatterEndIndex == -1)
        {
            throw new FormatException("Invalid SKILL.md: No frontmatter found.");
        }

        var frontmatterText = content.Substring(3, frontmatterEndIndex - 3).Trim();
        var instructionsText = content.Substring(frontmatterEndIndex + 3).TrimStart('\r', '\n');

        var dict = YamlDeserializer.Deserialize<Dictionary<string, object>>(frontmatterText) ?? new Dictionary<string, object>();
        
        var frontmatter = new Frontmatter();
        if (dict.TryGetValue("name", out var n)) frontmatter.Name = n.ToString() ?? "";
        if (dict.TryGetValue("description", out var d)) frontmatter.Description = d.ToString() ?? "";
        if (dict.TryGetValue("license", out var l)) frontmatter.License = l.ToString();
        if (dict.TryGetValue("compatibility", out var c)) frontmatter.Compatibility = c.ToString();
        if (dict.TryGetValue("allowed-tools", out var at)) frontmatter.AllowedTools = at.ToString();
        
        if (dict.TryGetValue("metadata", out var metaObj) && metaObj is Dictionary<object, object> metaDict)
        {
            foreach (var kvp in metaDict)
            {
                frontmatter.Metadata[kvp.Key.ToString() ?? ""] = kvp.Value;
            }
        }

        return (frontmatter, instructionsText);
    }

    private static string GetRelativePath(string rootPath, string fullPath)
    {
        var rootUri = new Uri(rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar);
        var fullUri = new Uri(fullPath);
        return Uri.UnescapeDataString(rootUri.MakeRelativeUri(fullUri).ToString());
    }

    private static object LoadFileContent(string filePath)
    {
        // Return byte[] for binary files, string for text files
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var binaryExtensions = new HashSet<string> { ".png", ".jpg", ".jpeg", ".gif", ".pdf", ".zip", ".tar" };
        
        if (binaryExtensions.Contains(ext))
        {
            return File.ReadAllBytes(filePath);
        }
        
        return File.ReadAllText(filePath);
    }
}
