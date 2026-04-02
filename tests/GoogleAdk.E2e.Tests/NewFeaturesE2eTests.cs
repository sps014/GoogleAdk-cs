using System.Threading.Channels;
using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Abstractions.Sessions;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Runner;

namespace GoogleAdk.E2e.Tests;

public class NewFeaturesE2eTests
{
    [Fact]
    public async Task PlannerE2e_AppendsPlanningInstruction()
    {
        var llm = new CapturingLlm("gemini-2.5-flash");
        var agent = new LlmAgent(new LlmAgentConfig
        {
            Name = "planner",
            Model = llm,
            Planner = new GoogleAdk.Core.Planning.PlanReActPlanner()
        });

        var runner = new InMemoryRunner("planner-e2e", agent);
        var session = await runner.SessionService.CreateSessionAsync(new CreateSessionRequest
        {
            AppName = "planner-e2e",
            UserId = "user-1"
        });

        var userMessage = new Content { Role = "user", Parts = [new Part { Text = "Hi" }] };
        await foreach (var _ in runner.RunAsync("user-1", session.Id, userMessage)) { }

        Assert.Contains("/*PLANNING*/", llm.LastRequest!.Config!.SystemInstruction);
    }

    [Fact]
    public async Task OutputSchemaE2e_ProducesFinalResponse()
    {
        var llm = new ToolCallLlm("gemini-2.5-flash");
        var agent = new LlmAgent(new LlmAgentConfig
        {
            Name = "schema",
            Model = llm,
            Tools =
            [
                GeneratedTools.NoopTool
            ],
            OutputSchema = typeof(TestSchemaOutput)
        });

        var runner = new InMemoryRunner("schema-e2e", agent);
        var session = await runner.SessionService.CreateSessionAsync(new CreateSessionRequest
        {
            AppName = "schema-e2e",
            UserId = "user-1"
        });

        var userMessage = new Content { Role = "user", Parts = [new Part { Text = "Hi" }] };
        Event? last = null;
        await foreach (var evt in runner.RunAsync("user-1", session.Id, userMessage))
            last = evt;

        Assert.NotNull(last?.Content?.Parts?.FirstOrDefault()?.Text);
        Assert.Contains("\"foo\":\"bar\"", last!.Content!.Parts![0].Text);
    }

    [Fact]
    public async Task LiveBidiE2e_EchoesResponses()
    {
        var llm = new FakeLiveLlm("gemini-2.5-flash");
        var agent = new LlmAgent(new LlmAgentConfig { Name = "live", Model = llm });
        var runner = new InMemoryRunner("live-e2e", agent);
        var session = await runner.SessionService.CreateSessionAsync(new CreateSessionRequest
        {
            AppName = "live-e2e",
            UserId = "user-1"
        });

        var queue = new LiveRequestQueue();
        var events = new List<Event>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var runTask = Task.Run(async () =>
        {
            await foreach (var evt in runner.RunLiveAsync("user-1", session.Id, queue, cancellationToken: cts.Token))
            {
                events.Add(evt);
                if (evt.Content?.Parts?.Any(p => p.Text?.Contains("live: ping") == true) == true)
                {
                    cts.Cancel();
                    break;
                }
            }
        });

        await queue.SendContentAsync(new Content { Role = "user", Parts = [new Part { Text = "ping" }] });
        queue.Close();
        await runTask;

        Assert.Contains(events, e => e.Content?.Parts?.Any(p => p.Text?.Contains("live: ping") == true) == true);
    }

    [Fact]
    public async Task TransferToolE2e_RoutesToSubAgent()
    {
        var subLlm = new CapturingLlm("gemini-2.5-flash");
        var sub = new LlmAgent(new LlmAgentConfig { Name = "sub", Model = subLlm });
        var root = new LlmAgent(new LlmAgentConfig
        {
            Name = "root",
            Model = new TransferLlm("gemini-2.5-flash"),
            SubAgents = [sub]
        });

        var runner = new InMemoryRunner("transfer-e2e", root);
        var session = await runner.SessionService.CreateSessionAsync(new CreateSessionRequest
        {
            AppName = "transfer-e2e",
            UserId = "user-1"
        });

        var userMessage = new Content { Role = "user", Parts = [new Part { Text = "Hi" }] };
        Event? last = null;
        await foreach (var evt in runner.RunAsync("user-1", session.Id, userMessage))
            last = evt;

        Assert.Equal("sub", last?.Author);
    }

    private sealed class CapturingLlm : BaseLlm
    {
        public LlmRequest? LastRequest { get; private set; }

        public CapturingLlm(string model) : base(model) { }

        public override async IAsyncEnumerable<LlmResponse> GenerateContentAsync(
            LlmRequest llmRequest,
            bool stream = false,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            LastRequest = llmRequest;
            yield return new LlmResponse
            {
                Content = new Content { Role = "model", Parts = [new Part { Text = "ok" }] }
            };
            await Task.CompletedTask;
        }

        public override Task<BaseLlmConnection> ConnectAsync(LlmRequest llmRequest)
            => Task.FromResult<BaseLlmConnection>(new StreamingLlmConnection(this, llmRequest));
    }

    private sealed class ToolCallLlm : BaseLlm
    {
        public ToolCallLlm(string model) : base(model) { }

        public override async IAsyncEnumerable<LlmResponse> GenerateContentAsync(
            LlmRequest llmRequest,
            bool stream = false,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return new LlmResponse
            {
                Content = new Content
                {
                    Role = "model",
                    Parts =
                    [
                        new Part
                        {
                            FunctionCall = new FunctionCall
                            {
                                Name = "set_model_response",
                                Args = new Dictionary<string, object?>
                                {
                                    ["result"] = new Dictionary<string, object?> { ["foo"] = "bar" }
                                },
                                Id = "fc-1"
                            }
                        }
                    ]
                }
            };
            await Task.CompletedTask;
        }

        public override Task<BaseLlmConnection> ConnectAsync(LlmRequest llmRequest)
            => Task.FromResult<BaseLlmConnection>(new StreamingLlmConnection(this, llmRequest));
    }

    private sealed class TransferLlm : BaseLlm
    {
        public TransferLlm(string model) : base(model) { }

        public override async IAsyncEnumerable<LlmResponse> GenerateContentAsync(
            LlmRequest llmRequest,
            bool stream = false,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return new LlmResponse
            {
                Content = new Content
                {
                    Role = "model",
                    Parts =
                    [
                        new Part
                        {
                            FunctionCall = new FunctionCall
                            {
                                Name = "transfer_to_agent",
                                Args = new Dictionary<string, object?> { ["agentName"] = "sub" },
                                Id = "fc-1"
                            }
                        }
                    ]
                }
            };
            await Task.CompletedTask;
        }

        public override Task<BaseLlmConnection> ConnectAsync(LlmRequest llmRequest)
            => Task.FromResult<BaseLlmConnection>(new StreamingLlmConnection(this, llmRequest));
    }

    private sealed class FakeLiveLlm : BaseLlm
    {
        public FakeLiveLlm(string model) : base(model) { }

        public override IAsyncEnumerable<LlmResponse> GenerateContentAsync(
            LlmRequest llmRequest,
            bool stream = false,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public override Task<BaseLlmConnection> ConnectAsync(LlmRequest llmRequest)
            => Task.FromResult<BaseLlmConnection>(new FakeConnection());

        private sealed class FakeConnection : BaseLlmConnection
        {
            private readonly Channel<LlmResponse> _channel = Channel.CreateUnbounded<LlmResponse>();

            public override Task SendHistoryAsync(IEnumerable<Content> history, CancellationToken cancellationToken = default)
                => Task.CompletedTask;

            public override Task SendContentAsync(Content content, CancellationToken cancellationToken = default)
            {
                var text = content.Parts?.FirstOrDefault()?.Text ?? string.Empty;
                _channel.Writer.TryWrite(new LlmResponse
                {
                    Content = new Content { Role = "model", Parts = [new Part { Text = $"live: {text}" }] },
                    TurnComplete = true
                });
                _channel.Writer.TryComplete();
                return Task.CompletedTask;
            }

            public override Task SendRealtimeAsync(Part part, CancellationToken cancellationToken = default)
                => Task.CompletedTask;

            public override async IAsyncEnumerable<LlmResponse> ReceiveAsync(
                [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                while (await _channel.Reader.WaitToReadAsync(cancellationToken))
                {
                    while (_channel.Reader.TryRead(out var item))
                        yield return item;
                }
            }

            public override ValueTask DisposeAsync()
            {
                _channel.Writer.TryComplete();
                return ValueTask.CompletedTask;
            }
        }
    }
}

public class TestSchemaOutput
{
    public string? Foo { get; set; }
}
