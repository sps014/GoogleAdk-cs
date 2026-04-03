using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Abstractions.Sessions;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Context;
using GoogleAdk.Core.Planning;
using GoogleAdk.Core.Tools;
using GoogleAdk.Core.Apps;
using GoogleAdk.Core.Features;
using GoogleAdk.Core.Plugins;
using GoogleAdk.Core.Runner;
using GoogleAdk.Core.Agents.Processors;
using GoogleAdk.Core.Abstractions.Errors;
using GoogleAdk.Core.Abstractions.Artifacts;
using GoogleAdk.Core.Artifacts;
using System.Text;
using System.Threading.Channels;
using GoogleAdk.Evaluation;
using GoogleAdk.Evaluation.Models;
using GoogleAdk.Optimization;

namespace GoogleAdk.Core.Tests;

public class NewFeaturesTests
{
    private static InvocationContext CreateInvocationContext(BaseAgent agent)
    {
        return new InvocationContext
        {
            Agent = agent,
            Session = Session.Create("s-1", "app", "user"),
            RunConfig = new RunConfig()
        };
    }

    [Fact]
    public async Task Planner_AppendsInstructionsAndClearsThought()
    {
        var agent = new LlmAgent(new LlmAgentConfig
        {
            Name = "planner",
            Planner = new PlanReActPlanner()
        });
        var context = CreateInvocationContext(agent);
        var request = new LlmRequest
        {
            Contents =
            [
                new Content
                {
                    Role = "user",
                    Parts = [new Part { Text = "hi", Thought = true }]
                }
            ]
        };

        var processor = NlPlanningRequestProcessor.Instance;
        await foreach (var _ in processor.RunAsync(context, request)) { }

        Assert.NotNull(request.Config?.SystemInstruction);
        Assert.Null(request.Contents[0].Parts![0].Thought);
    }

    [Fact]
    public async Task BuiltInPlanner_SetsThinkingConfig()
    {
        var agent = new LlmAgent(new LlmAgentConfig
        {
            Name = "builtin",
            Planner = new BuiltInPlanner(new Dictionary<string, object?> { ["mode"] = "fast" })
        });
        var context = CreateInvocationContext(agent);
        var request = new LlmRequest();

        await foreach (var _ in NlPlanningRequestProcessor.Instance.RunAsync(context, request)) { }

        Assert.Equal("fast", request.Config?.ThinkingConfig?["mode"]);
    }

    [Fact]
    public void SetModelResponseTool_ExtractsResult()
    {
        var responseEvent = Event.Create(e =>
        {
            e.Content = new Content
            {
                Role = "user",
                Parts =
                [
                    new Part
                    {
                        FunctionResponse = new FunctionResponse
                        {
                            Name = SetModelResponseTool.ToolName,
                            Response = new Dictionary<string, object?> { ["result"] = new Dictionary<string, object?> { ["foo"] = "bar" } }
                        }
                    }
                ]
            };
        });

        var json = SetModelResponseTool.TryExtractStructuredResponse(responseEvent);
        Assert.NotNull(json);
        Assert.Contains("\"foo\":\"bar\"", json);
    }

    [Fact]
    public async Task LiveRequestQueue_SendsAndReads()
    {
        var queue = new LiveRequestQueue();
        var content = new Content { Role = "user", Parts = [new Part { Text = "ping" }] };
        await queue.SendContentAsync(content);
        queue.Close();

        var read = new List<LiveRequest>();
        await foreach (var item in queue.ReadAllAsync())
            read.Add(item);

        Assert.Contains(read, r => r.Content?.Parts?.Any(p => p.Text == "ping") == true);
    }

    [Fact]
    public void TransferToAgentTool_EnumTargets()
    {
        var tool = new TransferToAgentTool(["a", "b"]);
        var decl = tool.GetDeclaration();
        var props = (Dictionary<string, object?>)decl!.Parameters!["properties"]!;
        var agentName = (Dictionary<string, object?>)props["agentName"]!;
        var enumValues = (Array)agentName["enum"]!;
        Assert.Equal(2, enumValues.Length);
    }

    [Fact]
    public void Runner_UsesAppRootAgent()
    {
        var app = new AdkApp("app", new LlmAgent(new LlmAgentConfig { Name = "root" }));
        var runner = new GoogleAdk.Core.Runner.Runner(new GoogleAdk.Core.Runner.RunnerConfig
        {
            AppName = "app",
            App = app,
            SessionService = new Sessions.InMemorySessionService()
        });
        Assert.Equal("root", runner.Agent.Name);
    }

    [Fact]
    public void FeatureFlags_OverrideViaEnv()
    {
        Environment.SetEnvironmentVariable("ADK_ENABLE_LIVEBIDISTREAMING", "1");
        Assert.True(AdkFeatures.IsFeatureEnabled(FeatureName.LiveBidiStreaming));
        Environment.SetEnvironmentVariable("ADK_ENABLE_LIVEBIDISTREAMING", null);
    }

    [Fact]
    public async Task ContextFilterPlugin_TrimsContents()
    {
        var plugin = new ContextFilterPlugin(maxContents: 1);
        var ctx = new InvocationContext { Agent = new LlmAgent(new LlmAgentConfig { Name = "a" }), Session = Session.Create("s", "app", "u") };
        var request = new LlmRequest
        {
            Contents =
            [
                new Content { Role = "user", Parts = [new Part { Text = "1" }] },
                new Content { Role = "user", Parts = [new Part { Text = "2" }] }
            ]
        };

        await plugin.BeforeModelCallbackAsync(new AgentContext(ctx), request);
        Assert.Single(request.Contents);
    }

    [Fact]
    public void UiWidgets_MergeAndDeduplicate()
    {
        var ctx = new AgentContext(CreateInvocationContext(new LlmAgent(new LlmAgentConfig { Name = "u" })));
        ctx.RenderUiWidget(new UiWidget { Id = "w1", Provider = "mcp" });
        ctx.RenderUiWidget(new UiWidget { Id = "w1", Provider = "mcp" });
        Assert.Single(ctx.EventActions.RenderUiWidgets);
    }

    [Fact]
    public async Task ToolErrorCallback_Recovers()
    {
        var tool = GeneratedTools.FailToolTool;

        var agent = new LlmAgent(new LlmAgentConfig
        {
            Name = "tool-agent",
            Tools = [tool],
            OnToolErrorCallbacks =
            [
                (t, args, ctx, ex) => Task.FromResult<Dictionary<string, object?>?>(new Dictionary<string, object?> { ["result"] = "recovered" })
            ]
        });

        var context = CreateInvocationContext(agent);
        var evt = Event.Create(e =>
        {
            e.Content = new Content
            {
                Role = "model",
                Parts = [new Part { FunctionCall = new FunctionCall { Name = "fail", Args = new Dictionary<string, object?>(), Id = "1" } }]
            };
        });

        var response = await FunctionCallHandler.HandleFunctionCallsAsync(
            context,
            evt,
            new Dictionary<string, IBaseTool> { ["fail"] = tool },
            agent.BeforeToolCallbacks,
            agent.OnToolErrorCallbacks,
            agent.AfterToolCallbacks);

        Assert.Contains(response!.Content!.Parts!, p => p.FunctionResponse?.Response?["result"]?.ToString() == "recovered");
    }

    [Fact]
    public async Task OutputSchemaRequestProcessor_InjectsToolAndInstruction()
    {
        var agent = new LlmAgent(new LlmAgentConfig
        {
            Name = "schema",
            OutputSchema = typeof(TestSchemaOutput),
            Tools =
            [
                GeneratedTools.NoopTool
            ]
        });

        var request = new LlmRequest();
        var ctx = CreateInvocationContext(agent);
        await foreach (var _ in OutputSchemaRequestProcessor.Instance.RunAsync(ctx, request)) { }

        Assert.Contains("set_model_response", request.ToolsDict.Keys);
        Assert.Contains("set_model_response", request.Config!.SystemInstruction);
    }

    [Fact]
    public async Task TransferToAgentTool_SetsAction()
    {
        var tool = new TransferToAgentTool(["alpha"]);
        var ctx = new AgentContext(CreateInvocationContext(new LlmAgent(new LlmAgentConfig { Name = "a" })))
        {
            FunctionCallId = "fc-1"
        };
        await tool.RunAsync(new Dictionary<string, object?> { ["agentName"] = "alpha" }, ctx);
        Assert.Equal("alpha", ctx.EventActions.TransferToAgent);
    }

    [Fact]
    public async Task GlobalInstructionPlugin_AppendsInstruction()
    {
        var plugin = new GlobalInstructionPlugin("always");
        var ctx = new AgentContext(
            CreateInvocationContext(new LlmAgent(new LlmAgentConfig { Name = "a" })),
            functionCallId: "fc-1");
        var request = new LlmRequest();
        await plugin.BeforeModelCallbackAsync(ctx, request);
        Assert.Contains("always", request.Config!.SystemInstruction);
    }

    [Fact]
    public async Task SaveFilesAsArtifactsPlugin_SavesInlineData()
    {
        var plugin = new SaveFilesAsArtifactsPlugin();
        var artifactService = new InMemoryArtifactService();
        var ctx = new InvocationContext
        {
            Agent = new LlmAgent(new LlmAgentConfig { Name = "a" }),
            Session = Session.Create("s", "app", "user"),
            ArtifactService = artifactService
        };

        var content = new Content
        {
            Role = "user",
            Parts =
            [
                new Part
                {
                    InlineData = new InlineData { Data = Convert.ToBase64String(Encoding.UTF8.GetBytes("hi")), MimeType = "text/plain", DisplayName = "note.txt" }
                }
            ]
        };

        var updated = await plugin.OnUserMessageCallbackAsync(ctx, content);
        Assert.NotNull(updated);
        Assert.NotNull(updated!.Parts![0].FileData);
        var loaded = await artifactService.LoadArtifactAsync(new LoadArtifactRequest
        {
            AppName = "app",
            UserId = "user",
            SessionId = "s",
            Filename = "note.txt"
        });
        Assert.NotNull(loaded);
    }

    [Fact]
    public async Task DebugLoggingPlugin_WritesEvent()
    {
        var temp = Path.GetTempFileName();
        var plugin = new DebugLoggingPlugin(temp);
        var ctx = CreateInvocationContext(new LlmAgent(new LlmAgentConfig { Name = "a" }));
        var evt = Event.Create(e => e.Author = "model");
        await plugin.OnEventCallbackAsync(ctx, evt);
        var text = await File.ReadAllTextAsync(temp);
        Assert.Contains("\"Author\":\"model\"", text);
    }

    [Fact]
    public void FeatureFlags_TemporaryOverride()
    {
        using var _ = AdkFeatures.TemporaryOverride(FeatureName.OutputSchemaWithTools, true);
        Assert.True(AdkFeatures.IsFeatureEnabled(FeatureName.OutputSchemaWithTools));
    }

    [Fact]
    public async Task AuthenticatedFunctionTool_RequestsCredentialWhenMissing()
    {
        var authConfig = new GoogleAdk.Core.Abstractions.Auth.AuthConfig
        {
            CredentialKey = "test"
        };

        var tool = new AuthenticatedFunctionTool(
            "auth_tool",
            "auth",
            authConfig,
            (_, _) => Task.FromResult<object?>("ok"),
            new Dictionary<string, object?> { ["type"] = "object" });

        var ctx = new AgentContext(
            CreateInvocationContext(new LlmAgent(new LlmAgentConfig { Name = "a" })),
            functionCallId: "fc-1");
        var result = await tool.RunAsync(new Dictionary<string, object?>(), ctx);

        Assert.Equal("auth_required", ((Dictionary<string, object?>)result!)["status"]);
        Assert.Single(ctx.EventActions.RequestedAuthConfigs);
    }

    [Fact]
    public async Task GroundingTools_AddToolDeclarations()
    {
        var request = new LlmRequest();
        var ctx = new AgentContext(CreateInvocationContext(new LlmAgent(new LlmAgentConfig { Name = "a" })));

        await new DiscoveryEngineSearchTool().ProcessLlmRequestAsync(ctx, request);
        await new EnterpriseWebSearchTool().ProcessLlmRequestAsync(ctx, request);
        await new GoogleMapsGroundingTool().ProcessLlmRequestAsync(ctx, request);
        await new UrlContextTool().ProcessLlmRequestAsync(ctx, request);

        Assert.True(request.Config!.Tools!.Count >= 4);
    }

    [Fact]
    public async Task StreamingLlmConnection_EmitsResponse()
    {
        var llm = new EchoLlm("gemini-2.5-flash");
        var request = new LlmRequest();
        await using var connection = new StreamingLlmConnection(llm, request);

        await connection.SendContentAsync(new Content { Role = "user", Parts = [new Part { Text = "hi" }] });

        var responses = new List<LlmResponse>();
        await foreach (var resp in connection.ReceiveAsync())
        {
            responses.Add(resp);
            if (resp.TurnComplete == true) break;
        }

        Assert.Contains(responses, r => r.Content?.Parts?.Any(p => p.Text?.Contains("echo: hi") == true) == true);
    }

    [Fact]
    public async Task RunLiveAsync_YieldsResponseEvent()
    {
        var llm = new LiveEchoLlm("gemini-2.5-flash");
        var agent = new LlmAgent(new LlmAgentConfig { Name = "live", Model = llm });
        var runner = new InMemoryRunner("live-core", agent);
        var session = await runner.SessionService.CreateSessionAsync(new CreateSessionRequest
        {
            AppName = "live-core",
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

    private sealed class EchoLlm : BaseLlm
    {
        public EchoLlm(string model) : base(model) { }

        public override async IAsyncEnumerable<LlmResponse> GenerateContentAsync(
            LlmRequest llmRequest,
            bool stream = false,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var lastText = llmRequest.Contents.LastOrDefault()?.Parts?.FirstOrDefault()?.Text ?? string.Empty;
            yield return new LlmResponse
            {
                Content = new Content { Role = "model", Parts = [new Part { Text = $"echo: {lastText}" }] },
                TurnComplete = true
            };
            await Task.CompletedTask;
        }

        public override Task<BaseLlmConnection> ConnectAsync(LlmRequest llmRequest)
            => Task.FromResult<BaseLlmConnection>(new StreamingLlmConnection(this, llmRequest));
    }

    private sealed class LiveEchoLlm : BaseLlm
    {
        public LiveEchoLlm(string model) : base(model) { }

        public override IAsyncEnumerable<LlmResponse> GenerateContentAsync(
            LlmRequest llmRequest,
            bool stream = false,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public override Task<BaseLlmConnection> ConnectAsync(LlmRequest llmRequest)
            => Task.FromResult<BaseLlmConnection>(new LiveEchoConnection());

        private sealed class LiveEchoConnection : BaseLlmConnection
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

    [Fact]
    public void TypedErrors_Instantiate()
    {
        var err = new ToolExecutionError("oops", ToolErrorType.Execution);
        Assert.Equal(ToolErrorType.Execution, err.ErrorType);
    }

    [Fact]
    public async Task MissingTools_BasicBehavior()
    {
        var tool = new GetUserChoiceTool();
        Assert.True(tool.IsLongRunning);
        var bash = new ExecuteBashTool(["echo"]);
        var result = await bash.RunAsync(
            new Dictionary<string, object?> { ["command"] = "dir" },
            new AgentContext(CreateInvocationContext(new LlmAgent(new LlmAgentConfig { Name = "a" }))));
        Assert.NotNull(result);
    }

    [Fact]
    public void ContextCache_Configured()
    {
        var agent = new LlmAgent(new LlmAgentConfig { Name = "cache", ContextCacheConfig = new ContextCacheConfig() });
        Assert.Contains(agent.RequestProcessors, p => p == ContextCacheRequestProcessor.Instance);
    }

    [Fact]
    public async Task EvaluationAndOptimization_Smoke()
    {
        var eval = new EvalSet { EvalSetId = "set", EvalCases = [] };
        var service = new LocalEvalService();
        var results = await service.EvaluateAsync(eval, [], [], CancellationToken.None);
        Assert.Empty(results);

        var optimizer = new SimplePromptOptimizer();
        var opt = await optimizer.OptimizeAsync("prompt", new DummySampler());
        Assert.Equal("prompt", opt.Optimized);
    }

    private sealed class DummySampler : ISampler<string>
    {
        public Task<SamplingResult> SampleAndScoreAsync(string candidate, CancellationToken cancellationToken = default)
            => Task.FromResult(new SamplingResult { Score = 1.0 });
    }
}

public class TestSchemaOutput
{
    public string? Foo { get; set; }
}
