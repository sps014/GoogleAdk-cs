using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Agents;

namespace GoogleAdk.Core.Tools;

public sealed class UrlContextTool : BaseTool
{
    public UrlContextTool()
        : base("url_context", "URL context tool.")
    {
    }

    public override Task<object?> RunAsync(Dictionary<string, object?> args, AgentContext context)
        => Task.FromResult<object?>(null);

    public override Task ProcessLlmRequestAsync(AgentContext context, LlmRequest llmRequest)
    {
        llmRequest.Config ??= new GenerateContentConfig();
        llmRequest.Config.Tools ??= new List<ToolDeclaration>();
        llmRequest.Config.Tools.Add(new ToolDeclaration
        {
            UrlContext = new Dictionary<string, object?>()
        });
        return Task.CompletedTask;
    }
}
