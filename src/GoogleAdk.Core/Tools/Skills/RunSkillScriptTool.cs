using GoogleAdk.Core.Agents;
using GoogleAdk.Core.CodeExecutors;
using System.Text.Json;

namespace GoogleAdk.Core.Tools.Skills;

public class RunSkillScriptTool : BaseTool
{
    private readonly SkillToolset _toolset;

    public RunSkillScriptTool(SkillToolset toolset)
        : base("run_skill_script", "Executes a script from a skill's scripts/ directory.")
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
                        ["description"] = "The relative path to the script (e.g., 'scripts/setup.py')."
                    },
                    ["args"] = new Dictionary<string, object?>
                    {
                        ["type"] = "object", // Simplified for C#, accepting a dictionary of args
                        ["description"] = "Optional arguments to pass to the script as key-value pairs."
                    }
                },
                ["required"] = new List<string> { "skill_name", "file_path" }
            }
        };
    }

    public override async Task<object?> RunAsync(Dictionary<string, object?> args, AgentContext context)
    {
        if (!args.TryGetValue("skill_name", out var skillNameObj))
            return new { error = "Argument 'skill_name' is required.", error_code = "INVALID_ARGUMENTS" };
            
        string skillName = skillNameObj is System.Text.Json.JsonElement je1 ? (je1.GetString() ?? "") : (skillNameObj?.ToString() ?? "");

        if (!args.TryGetValue("file_path", out var filePathObj))
            return new { error = "Argument 'file_path' is required.", error_code = "INVALID_ARGUMENTS" };

        string filePath = filePathObj is System.Text.Json.JsonElement je2 ? (je2.GetString() ?? "") : (filePathObj?.ToString() ?? "");

        var skill = _toolset.GetSkill(skillName);
        if (skill == null)
        {
            return new { error = $"Skill '{skillName}' not found.", error_code = "SKILL_NOT_FOUND" };
        }

        var scriptName = filePath.StartsWith("scripts/") ? filePath.Substring("scripts/".Length) : filePath;
        var script = skill.Resources.GetScript(scriptName);

        if (script == null)
        {
            return new { error = $"Script '{filePath}' not found in skill '{skillName}'.", error_code = "SCRIPT_NOT_FOUND" };
        }

        var codeExecutor = _toolset.CodeExecutor;
        if (codeExecutor == null)
        {
            return new { error = "No code executor configured. A code executor is required to run scripts.", error_code = "NO_CODE_EXECUTOR" };
        }

        // Build execution input
        var executionInput = new CodeExecutionInput
        {
            Code = script.Src, // We just pass the script content to execute. The underlying executor (like Bash or Python) will handle it.
            InputFiles = new List<CodeFile>()
        };

        // Materialize resources into CodeFiles so the script can access them
        foreach (var refName in skill.Resources.ListReferences())
        {
            var content = skill.Resources.GetReference(refName);
            if (content != null)
            {
                executionInput.InputFiles.Add(new CodeFile
                {
                    Name = $"references/{refName}",
                    Content = content is byte[] bytes ? Convert.ToBase64String(bytes) : Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes((string)content))
                });
            }
        }

        foreach (var assetName in skill.Resources.ListAssets())
        {
            var content = skill.Resources.GetAsset(assetName);
            if (content != null)
            {
                executionInput.InputFiles.Add(new CodeFile
                {
                    Name = $"assets/{assetName}",
                    Content = content is byte[] bytes ? Convert.ToBase64String(bytes) : Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes((string)content))
                });
            }
        }

        try
        {
            // Execute using the configured code executor
            var result = await codeExecutor.ExecuteCodeAsync(context.InvocationContext, executionInput);
            
            var status = "success";
            if (!string.IsNullOrEmpty(result.Stderr) && string.IsNullOrEmpty(result.Stdout))
            {
                status = "error";
            }
            else if (!string.IsNullOrEmpty(result.Stderr))
            {
                status = "warning";
            }

            return new
            {
                skill_name = skillName,
                file_path = filePath,
                stdout = result.Stdout,
                stderr = result.Stderr,
                status = status
            };
        }
        catch (Exception ex)
        {
            return new
            {
                error = $"Failed to execute script '{filePath}':\n{ex.GetType().Name}: {ex.Message}",
                error_code = "EXECUTION_ERROR"
            };
        }
    }
}
