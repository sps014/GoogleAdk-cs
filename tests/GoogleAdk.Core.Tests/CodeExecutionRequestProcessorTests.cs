using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Sessions;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Agents.Processors;
using GoogleAdk.Core.CodeExecutors;

namespace GoogleAdk.Core.Tests;

public class CodeExecutionRequestProcessorTests
{
    [Fact]
    public async Task CodeExecutionRequestProcessor_EnablesBuiltInTool()
    {
        var agent = new LlmAgent(new LlmAgentConfig
        {
            Name = "agent",
            CodeExecutor = new BuiltInCodeExecutor()
        });

        var invocationContext = new InvocationContext
        {
            Agent = agent,
            Session = Session.Create("s1", "app", "user")
        };

        var llmRequest = new LlmRequest { Model = "gemini-2.5-flash" };
        var processor = CodeExecutionRequestProcessor.Instance;

        await foreach (var _ in processor.RunAsync(invocationContext, llmRequest)) { }

        Assert.NotNull(llmRequest.Config?.Tools);
        Assert.Contains(llmRequest.Config!.Tools!, t => t.CodeExecution != null);
    }
}
