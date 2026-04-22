using GoogleAdk.Core.Agents;
using System.Text.Json;
using System.Text;

namespace GoogleAdk.Core.Tools.Skills;

public class ListSkillsTool : BaseTool
{
    private readonly SkillToolset _toolset;

    public ListSkillsTool(SkillToolset toolset)
        : base("list_skills", "Lists all available skills with their names and descriptions.")
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
                ["properties"] = new Dictionary<string, object?>()
            }
        };
    }

    public override Task<object?> RunAsync(Dictionary<string, object?> args, AgentContext context)
    {
        var skills = _toolset.ListSkills();
        var sb = new StringBuilder();
        sb.AppendLine("<skills>");
        foreach (var skill in skills)
        {
            sb.AppendLine("  <skill>");
            sb.AppendLine($"    <name>{skill.Name}</name>");
            sb.AppendLine($"    <description>{skill.Description}</description>");
            sb.AppendLine("  </skill>");
        }
        sb.AppendLine("</skills>");
        return Task.FromResult<object?>(sb.ToString());
    }
}
