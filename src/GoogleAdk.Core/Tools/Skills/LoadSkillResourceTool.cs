using GoogleAdk.Core.Agents;

namespace GoogleAdk.Core.Tools.Skills;

public class LoadSkillResourceTool : BaseTool
{
    private readonly SkillToolset _toolset;
    private const string BinaryFileDetectedMsg = "Binary file detected. The content has been injected into the conversation history for you to analyze.";

    public LoadSkillResourceTool(SkillToolset toolset)
        : base("load_skill_resource", "Loads a resource file (from references/, assets/, or scripts/) from within a skill.")
    {
        _toolset = toolset;
    }

    public override Abstractions.Models.FunctionDeclaration? GetDeclaration()
    {
        return new Abstractions.Models.FunctionDeclaration
        {
            Name = Name,
            Description = Description,
            Parameters = new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?>
                {
                    ["skill_name"] = new Dictionary<string, object?>
                    {
                        ["type"] = "string",
                        ["description"] = "The name of the skill."
                    },
                    ["file_path"] = new Dictionary<string, object?>
                    {
                        ["type"] = "string",
                        ["description"] = "The relative path to the resource (e.g., 'references/my_doc.md', 'assets/template.txt', or 'scripts/setup.sh')."
                    }
                },
                ["required"] = new List<string> { "skill_name", "file_path" }
            }
        };
    }

    public override Task<object?> RunAsync(Dictionary<string, object?> args, AgentContext context)
    {
        if (!args.TryGetValue("skill_name", out var skillNameObj))
            return Task.FromResult<object?>(new { error = "Argument 'skill_name' is required.", error_code = "INVALID_ARGUMENTS" });
            
        string skillName = skillNameObj is System.Text.Json.JsonElement je1 ? (je1.GetString() ?? "") : (skillNameObj?.ToString() ?? "");

        if (!args.TryGetValue("file_path", out var filePathObj))
            return Task.FromResult<object?>(new { error = "Argument 'file_path' is required.", error_code = "INVALID_ARGUMENTS" });

        string filePath = filePathObj is System.Text.Json.JsonElement je2 ? (je2.GetString() ?? "") : (filePathObj?.ToString() ?? "");

        var skill = _toolset.GetSkill(skillName);
        if (skill == null)
        {
            return Task.FromResult<object?>(new { error = $"Skill '{skillName}' not found.", error_code = "SKILL_NOT_FOUND" });
        }

        object? content = null;
        if (filePath.StartsWith("references/"))
        {
            content = skill.Resources.GetReference(filePath.Substring("references/".Length));
        }
        else if (filePath.StartsWith("assets/"))
        {
            content = skill.Resources.GetAsset(filePath.Substring("assets/".Length));
        }
        else if (filePath.StartsWith("scripts/"))
        {
            var script = skill.Resources.GetScript(filePath.Substring("scripts/".Length));
            content = script?.Src;
        }
        else
        {
            return Task.FromResult<object?>(new { error = "Path must start with 'references/', 'assets/', or 'scripts/'.", error_code = "INVALID_RESOURCE_PATH" });
        }

        if (content == null)
        {
            return Task.FromResult<object?>(new { error = $"Resource '{filePath}' not found in skill '{skillName}'.", error_code = "RESOURCE_NOT_FOUND" });
        }

        if (content is byte[])
        {
            // Binary content injection handled implicitly or via tool response
            return Task.FromResult<object?>(new
            {
                skill_name = skillName,
                file_path = filePath,
                status = BinaryFileDetectedMsg
            });
        }

        return Task.FromResult<object?>(new
        {
            skill_name = skillName,
            file_path = filePath,
            content = content
        });
    }
}
