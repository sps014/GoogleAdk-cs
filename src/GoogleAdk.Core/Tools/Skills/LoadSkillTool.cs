using GoogleAdk.Core.Agents;

namespace GoogleAdk.Core.Tools.Skills;

public class LoadSkillTool : BaseTool
{
    private readonly SkillToolset _toolset;

    public LoadSkillTool(SkillToolset toolset)
        : base("load_skill", "Loads the SKILL.md instructions for a given skill.")
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
                        ["description"] = "The name of the skill to load."
                    }
                },
                ["required"] = new List<string> { "skill_name" }
            }
        };
    }

    public override Task<object?> RunAsync(Dictionary<string, object?> args, AgentContext context)
    {
        if (!args.TryGetValue("skill_name", out var skillNameObj))
        {
            return Task.FromResult<object?>(new
            {
                error = "Argument 'skill_name' is required.",
                error_code = "INVALID_ARGUMENTS"
            });
        }

        string skillName = skillNameObj is System.Text.Json.JsonElement je 
            ? (je.GetString() ?? "") 
            : (skillNameObj?.ToString() ?? "");

        if (string.IsNullOrWhiteSpace(skillName))
        {
            return Task.FromResult<object?>(new
            {
                error = "Argument 'skill_name' is required.",
                error_code = "INVALID_ARGUMENTS"
            });
        }

        var skill = _toolset.GetSkill(skillName);
        if (skill == null)
        {
            return Task.FromResult<object?>(new
            {
                error = $"Skill '{skillName}' not found.",
                error_code = "SKILL_NOT_FOUND"
            });
        }

        // Track activated skills in state
        var stateKey = $"_adk_activated_skill_default"; // Fallback identifier
        if (context.State != null)
        {
            var activatedSkills = context.State.Get<List<string>>(stateKey) ?? new List<string>();
            
            if (!activatedSkills.Contains(skillName))
            {
                activatedSkills.Add(skillName);
                context.State.Set(stateKey, activatedSkills);
            }
        }

        return Task.FromResult<object?>(new
        {
            skill_name = skillName,
            instructions = skill.Instructions,
            frontmatter = skill.Frontmatter
        });
    }
}
