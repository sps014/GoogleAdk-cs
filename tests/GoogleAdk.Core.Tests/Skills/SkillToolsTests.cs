using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Skills;
using GoogleAdk.Core.Tools;
using GoogleAdk.Core.Tools.Skills;
using GoogleAdk.Core.Abstractions.Sessions;

namespace GoogleAdk.Core.Tests.Skills;

public class SkillToolsTests
{
    private SkillToolset CreateToolset()
    {
        var skill1 = new Skill
        {
            Frontmatter = new Frontmatter { Name = "skill-1", Description = "First skill" },
            Instructions = "Do things."
        };
        skill1.Resources.References["ref1.md"] = "ref 1 content";

        var skill2 = new Skill
        {
            Frontmatter = new Frontmatter { Name = "skill-2", Description = "Second skill" },
            Instructions = "Do other things."
        };

        return new SkillToolset(new[] { skill1, skill2 });
    }

    [Fact]
    public async Task ListSkillsTool_ShouldReturnXml()
    {
        var toolset = CreateToolset();
        var listTool = new ListSkillsTool(toolset);
        
        var invocationContext = new InvocationContext
        {
            Session = new Session { AppName = "test", UserId = "user1", Id = "session1" }
        };
        var context = new AgentContext(invocationContext);
        var result = await listTool.RunAsync(new Dictionary<string, object?>(), context) as string;

        Assert.NotNull(result);
        Assert.Contains("<name>skill-1</name>", result);
        Assert.Contains("<name>skill-2</name>", result);
        Assert.Contains("<description>First skill</description>", result);
    }

    [Fact]
    public async Task LoadSkillTool_ShouldReturnInstructions()
    {
        var toolset = CreateToolset();
        var loadTool = new LoadSkillTool(toolset);
        
        var args = new Dictionary<string, object?> { ["skill_name"] = "skill-1" };
        var invocationContext = new InvocationContext
        {
            Session = new Session { AppName = "test", UserId = "user1", Id = "session1" }
        };
        var context = new AgentContext(invocationContext);
        
        var rawResult = await loadTool.RunAsync(args, context);
        var json = System.Text.Json.JsonSerializer.Serialize(rawResult);
        var result = System.Text.Json.JsonDocument.Parse(json).RootElement;

        Assert.Equal("skill-1", result.GetProperty("skill_name").GetString());
        Assert.Equal("Do things.", result.GetProperty("instructions").GetString());
        Assert.True(result.TryGetProperty("frontmatter", out _));

        // Verify state was updated
        var activated = context.State.Get<List<string>>("_adk_activated_skill_default");
        Assert.NotNull(activated);
        Assert.Contains("skill-1", activated);
    }

    [Fact]
    public async Task LoadSkillResourceTool_ShouldReturnContent()
    {
        var toolset = CreateToolset();
        var resourceTool = new LoadSkillResourceTool(toolset);
        
        var args = new Dictionary<string, object?> 
        { 
            ["skill_name"] = "skill-1",
            ["file_path"] = "references/ref1.md"
        };
        
        var invocationContext = new InvocationContext
        {
            Session = new Session { AppName = "test", UserId = "user1", Id = "session1" }
        };
        var rawResult = await resourceTool.RunAsync(args, new AgentContext(invocationContext));
        var json = System.Text.Json.JsonSerializer.Serialize(rawResult);
        var result = System.Text.Json.JsonDocument.Parse(json).RootElement;

        Assert.Equal("skill-1", result.GetProperty("skill_name").GetString());
        Assert.Equal("references/ref1.md", result.GetProperty("file_path").GetString());
        Assert.Equal("ref 1 content", result.GetProperty("content").GetString());
    }

    [Fact]
    public async Task RunSkillScriptTool_WithoutExecutor_ReturnsError()
    {
        var toolset = CreateToolset();
        var runTool = new RunSkillScriptTool(toolset);
        
        var args = new Dictionary<string, object?> 
        { 
            ["skill_name"] = "skill-1",
            ["file_path"] = "scripts/script.sh"
        };
        
        var invocationContext = new InvocationContext
        {
            Session = new Session { AppName = "test", UserId = "user1", Id = "session1" }
        };
        var rawResult = await runTool.RunAsync(args, new AgentContext(invocationContext));
        var json = System.Text.Json.JsonSerializer.Serialize(rawResult);
        var result = System.Text.Json.JsonDocument.Parse(json).RootElement;

        // Because skill doesn't have script yet, it will fail at SCRIPT_NOT_FOUND first
        Assert.Equal("SCRIPT_NOT_FOUND", result.GetProperty("error_code").GetString());

        // Let's add script
        toolset.GetSkill("skill-1")!.Resources.Scripts["script.sh"] = new Script { Src = "echo hello" };
        
        var rawResult2 = await runTool.RunAsync(args, new AgentContext(invocationContext));
        var json2 = System.Text.Json.JsonSerializer.Serialize(rawResult2);
        var result2 = System.Text.Json.JsonDocument.Parse(json2).RootElement;
        
        Assert.Equal("NO_CODE_EXECUTOR", result2.GetProperty("error_code").GetString());
    }
}