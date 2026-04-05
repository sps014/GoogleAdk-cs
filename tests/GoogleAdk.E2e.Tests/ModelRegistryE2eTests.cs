using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Abstractions.Sessions;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Runner;

namespace GoogleAdk.E2e.Tests;

public class ModelRegistryE2eTests
{
    private sealed class RegistryLlm : BaseLlm
    {
        public bool WasCalled { get; private set; }

        public RegistryLlm(string model) : base(model) { }

        public override async IAsyncEnumerable<LlmResponse> GenerateContentAsync(
            LlmRequest llmRequest,
            bool stream = false,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            yield return new LlmResponse
            {
                Content = new Content
                {
                    Role = "model",
                    Parts = new List<Part> { new Part { Text = "ok" } }
                }
            };
            await Task.CompletedTask;
        }

        public override Task<BaseLlmConnection> ConnectAsync(LlmRequest llmRequest)
        {
            throw new NotSupportedException();
        }
    }

    [Fact]
    public async Task ModelRegistry_ResolvesModelNameInAgent()
    {
        var llm = new RegistryLlm("fake-1");
        LlmRegistry.Register(_ => llm, new[] { "fake-.*" });

        var agent = new LlmAgent(new LlmAgentConfig
        {
            Name = "registry-agent",
            Model = "fake-1",
            Instruction = "test"
        });

        var runner = new InMemoryRunner("registry-e2e", agent);
        var session = await runner.SessionService.CreateSessionAsync(new CreateSessionRequest
        {
            AppName = "registry-e2e",
            UserId = "user-1",
        });

        var userMessage = new Content
        {
            Role = "user",
            Parts = new List<Part> { new Part { Text = "Hello" } }
        };

        await foreach (var _ in runner.RunAsync("user-1", session.Id, userMessage)) { }

        Assert.True(llm.WasCalled);
    }
}
