using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;

namespace GoogleAdk.Core.Tests;

public class LlmRegistryTests
{
    private sealed class FakeLlm : BaseLlm
    {
        public FakeLlm(string model) : base(model) { }

        public override IAsyncEnumerable<LlmResponse> GenerateContentAsync(
            LlmRequest llmRequest,
            bool stream = false,
            CancellationToken cancellationToken = default)
        {
            return AsyncEnumerable.Empty<LlmResponse>();
        }

        public override Task<BaseLlmConnection> ConnectAsync(LlmRequest llmRequest)
        {
            throw new NotSupportedException();
        }
    }

    [Fact]
    public void LlmRegistry_ResolvesRegisteredModel()
    {
        LlmRegistry.Register(model => new FakeLlm(model), new[] { "fake-.*" });

        var instance = LlmRegistry.NewLlm("fake-1");
        Assert.IsType<FakeLlm>(instance);
    }
}
