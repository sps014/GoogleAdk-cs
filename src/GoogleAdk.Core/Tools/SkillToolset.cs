using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.CodeExecutors;
using GoogleAdk.Core.Skills;
using GoogleAdk.Core.Tools.Skills;

namespace GoogleAdk.Core.Tools;

/// <summary>
/// A toolset for managing and interacting with agent skills.
/// </summary>
public sealed class SkillToolset : BaseToolset
{
    private readonly Dictionary<string, Skill> _skills;
    private readonly List<BaseTool> _tools;

    public BaseCodeExecutor? CodeExecutor { get; }

    private const string DefaultSkillSystemInstruction = @"You can use specialized 'skills' to help you with complex tasks. You MUST use the skill tools to interact with these skills.

Skills are folders of instructions and resources that extend your capabilities for specialized tasks. Each skill folder contains:
- **SKILL.md** (required): The main instruction file with skill metadata and detailed markdown instructions.
- **references/** (Optional): Additional documentation or examples for skill usage.
- **assets/** (Optional): Templates, scripts or other resources used by the skill.
- **scripts/** (Optional): Executable scripts that can be run via bash.

This is very important:

1. If a skill seems relevant to the current user query, you MUST use the `load_skill` tool with `skill_name=""<SKILL_NAME>""` to read its full instructions before proceeding.
2. Once you have read the instructions, follow them exactly as documented before replying to the user. For example, If the instruction lists multiple steps, please make sure you complete all of them in order.
3. The `load_skill_resource` tool is for viewing files within a skill's directory (e.g., `references/*`, `assets/*`, `scripts/*`). Do NOT use other tools to access these files.
4. Use `run_skill_script` to run scripts from a skill's `scripts/` directory. Use `load_skill_resource` to view script content first if needed.
";

    public SkillToolset(IEnumerable<Skill> skills, BaseCodeExecutor? codeExecutor = null)
    {
        _skills = new Dictionary<string, Skill>();
        foreach (var skill in skills)
        {
            if (_skills.ContainsKey(skill.Name))
            {
                throw new ArgumentException($"Duplicate skill name '{skill.Name}'.");
            }
            _skills[skill.Name] = skill;
        }

        CodeExecutor = codeExecutor;

        _tools = new List<BaseTool>
        {
            new ListSkillsTool(this),
            new LoadSkillTool(this),
            new LoadSkillResourceTool(this),
            new RunSkillScriptTool(this)
        };
    }

    public Skill? GetSkill(string skillName)
    {
        return _skills.TryGetValue(skillName, out var skill) ? skill : null;
    }

    public IEnumerable<Skill> ListSkills()
    {
        return _skills.Values;
    }

    public override Task<IReadOnlyList<BaseTool>> GetToolsAsync(AgentContext? context = null)
    {
        return Task.FromResult<IReadOnlyList<BaseTool>>(_tools.AsReadOnly());
    }

    public override Task ProcessLlmRequestAsync(AgentContext context, Abstractions.Events.LlmRequest llmRequest)
    {
        var instructions = new List<string> { DefaultSkillSystemInstruction };
        
        var listTool = new ListSkillsTool(this);
        var skillsXml = listTool.RunAsync(new Dictionary<string, object?>(), context).Result as string;
        
        if (!string.IsNullOrEmpty(skillsXml))
        {
            instructions.Add(skillsXml);
        }

        llmRequest.AppendInstructions(instructions);

        // Add tool schemas to the request
        foreach (var tool in _tools)
        {
            tool.ProcessLlmRequestAsync(context, llmRequest);
        }

        return Task.CompletedTask;
    }
}
