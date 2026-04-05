using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Abstractions.Sessions;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Tools;

namespace GoogleAdk.Core.Tests;

public class ToolTests
{
    private static AgentContext CreateToolContext()
    {
        var invCtx = new InvocationContext
        {
            Session = Session.Create("s1", "app", "user"),
            Agent = new TestAgent("root"),
        };
        return new AgentContext(invCtx);
    }

    [Fact]
    public async Task FunctionTool_ExecutesDelegate()
    {
        var tool = GeneratedTools.AddToolTool;

        var context = CreateToolContext();
        var result = await tool.RunAsync(new Dictionary<string, object?>
        {
            ["a"] = 3,
            ["b"] = 5
        }, context);

        Assert.Equal(8, result);
    }

    [Fact]
    public void FunctionTool_HasDeclaration()
    {
        var tool = GeneratedTools.TestToolTool;

        var decl = tool.GetDeclaration();
        Assert.NotNull(decl);
        Assert.Equal("test_tool", decl!.Name);
        Assert.Equal("Test tool.", decl.Description);
    }

    [Fact]
    public async Task ExitLoopTool_SetsEscalate()
    {
        var context = CreateToolContext();
        await ExitLoopTool.Instance.RunAsync(new Dictionary<string, object?>(), context);

        Assert.True(context.EventActions.Escalate);
        Assert.True(context.EventActions.SkipSummarization);
    }

    [Fact]
    public void GoogleSearchTool_HasCorrectName()
    {
        Assert.Equal("google_search", GoogleSearchTool.Instance.Name);
    }

    [Fact]
    public async Task GoogleSearchTool_ProcessesLlmRequest()
    {
        var context = CreateToolContext();
        var request = new LlmRequest();

        await GoogleSearchTool.Instance.ProcessLlmRequestAsync(context, request);

        Assert.NotNull(request.Config?.Tools);
        Assert.Single(request.Config!.Tools!);
        Assert.NotNull(request.Config.Tools[0].GoogleSearch);
    }

    [Fact]
    public void SyncFunctionTool_Works()
    {
        var tool = GeneratedTools.GreetTool;

        var decl = tool.GetDeclaration();
        Assert.Equal("greet", decl!.Name);
    }
}
