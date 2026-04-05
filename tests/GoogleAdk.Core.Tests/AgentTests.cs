using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Abstractions.Sessions;
using GoogleAdk.Core.Agents;
using System.Runtime.CompilerServices;

namespace GoogleAdk.Core.Tests;

/// <summary>
/// A test agent that yields configurable events.
/// </summary>
public class TestAgent : BaseAgent
{
    private readonly Func<InvocationContext, IAsyncEnumerable<Event>>? _runFunc;

    public TestAgent(string name, string? description = null, Func<InvocationContext, IAsyncEnumerable<Event>>? runFunc = null)
        : base(new BaseAgentConfig { Name = name, Description = description ?? name })
    {
        _runFunc = runFunc;
    }

    protected override IAsyncEnumerable<Event> RunAsyncImpl(InvocationContext context, CancellationToken cancellationToken = default)
    {
        if (_runFunc != null)
            return _runFunc(context);
        return EmptyEvents();
    }

    private static async IAsyncEnumerable<Event> EmptyEvents([EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.CompletedTask;
        yield break;
    }
}

/// <summary>
/// A test agent that yields a single text event.
/// </summary>
public class TextAgent : BaseAgent
{
    private readonly string _text;

    public TextAgent(string name, string text)
        : base(new BaseAgentConfig { Name = name, Description = name })
    {
        _text = text;
    }

    protected override async IAsyncEnumerable<Event> RunAsyncImpl(
        InvocationContext context, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield return Event.Create(e =>
        {
            e.InvocationId = context.InvocationId;
            e.Author = Name;
            e.Content = new Content
            {
                Role = "model",
                Parts = new List<Part> { new Part { Text = _text } }
            };
        });
    }
}

/// <summary>
/// A counter agent that tracks iteration count.
/// </summary>
public class CounterAgent : BaseAgent
{
    public int Count { get; set; }

    public CounterAgent(string name = "counter")
        : base(new BaseAgentConfig { Name = name }) { }

    protected override async IAsyncEnumerable<Event> RunAsyncImpl(
        InvocationContext context, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Count++;
        await Task.CompletedTask;
        yield return Event.Create(e =>
        {
            e.InvocationId = context.InvocationId;
            e.Author = Name;
            e.Content = new Content
            {
                Role = "model",
                Parts = new List<Part> { new Part { Text = $"iteration {Count}" } }
            };
        });
    }
}

public class AgentTests
{
    private static InvocationContext CreateTestContext()
    {
        return new InvocationContext
        {
            Session = Session.Create("s1", "app", "user"),
            Agent = new TestAgent("root"),
        };
    }

    [Fact]
    public async Task SequentialAgent_RunsInOrder()
    {
        var seq = new SequentialAgent(new BaseAgentConfig
        {
            Name = "seq",
            SubAgents = new List<BaseAgent>
            {
                new TextAgent("agent1", "first"),
                new TextAgent("agent2", "second"),
            }
        });

        var context = CreateTestContext();
        context.Agent = seq;

        var events = new List<Event>();
        await foreach (var evt in seq.RunAsync(context))
            events.Add(evt);

        Assert.Equal(2, events.Count);
        Assert.Equal("first", events[0].Content?.Parts?[0].Text);
        Assert.Equal("second", events[1].Content?.Parts?[0].Text);
    }

    [Fact]
    public async Task ParallelAgent_RunsAll()
    {
        var parallel = new ParallelAgent(new BaseAgentConfig
        {
            Name = "par",
            SubAgents = new List<BaseAgent>
            {
                new TextAgent("agent1", "a"),
                new TextAgent("agent2", "b"),
            }
        });

        var context = CreateTestContext();
        context.Agent = parallel;

        var events = new List<Event>();
        await foreach (var evt in parallel.RunAsync(context))
            events.Add(evt);

        Assert.Equal(2, events.Count);
        var texts = events.Select(e => e.Content?.Parts?[0].Text).OrderBy(t => t).ToList();
        Assert.Contains("a", texts);
        Assert.Contains("b", texts);
    }

    [Fact]
    public async Task LoopAgent_RespectsMaxIterations()
    {
        var counter = new CounterAgent();
        var loop = new LoopAgent(new LoopAgentConfig
        {
            Name = "loop",
            SubAgents = new List<BaseAgent> { counter },
            MaxIterations = 3,
        });

        var context = CreateTestContext();
        context.Agent = loop;

        var events = new List<Event>();
        await foreach (var evt in loop.RunAsync(context))
            events.Add(evt);

        Assert.Equal(3, events.Count);
        Assert.Equal(3, counter.Count);
    }

    [Fact]
    public void FindAgent_FindsNestedAgent()
    {
        var child = new TestAgent("child");
        var parent = new SequentialAgent(new BaseAgentConfig
        {
            Name = "parent",
            SubAgents = new List<BaseAgent> { child }
        });

        var found = parent.FindAgent("child");
        Assert.NotNull(found);
        Assert.Equal("child", found!.Name);
    }

    [Fact]
    public void RootAgent_NavigatesToRoot()
    {
        var grandchild = new TestAgent("grandchild");
        var child = new SequentialAgent(new BaseAgentConfig
        {
            Name = "child",
            SubAgents = new List<BaseAgent> { grandchild }
        });
        _ = new SequentialAgent(new BaseAgentConfig
        {
            Name = "root",
            SubAgents = new List<BaseAgent> { child }
        });

        Assert.Equal("root", grandchild.RootAgent.Name);
    }

    [Fact]
    public void SequentialAgentConfig_Instantiates()
    {
        var config = new SequentialAgentConfig { Name = "seq-test" };
        var agent = new SequentialAgent(config);
        Assert.Equal("seq-test", agent.Name);
    }

    [Fact]
    public void ParallelAgentConfig_Instantiates()
    {
        var config = new ParallelAgentConfig { Name = "par-test" };
        var agent = new ParallelAgent(config);
        Assert.Equal("par-test", agent.Name);
    }

    [Fact]
    public void LlmAgentConfig_StaticInstruction()
    {
        var content = new Content { Role = "system", Parts = new List<Part> { new Part { Text = "You are a helpful assistant." } } };
        var config = new LlmAgentConfig
        {
            Name = "llm-test",
            StaticInstruction = content
        };
        var agent = new LlmAgent(config);
        Assert.Equal(content, agent.StaticInstruction);
    }

    [Fact]
    public void RunConfig_Properties_Populate()
    {
        var config = new RunConfig
        {
            MaxLlmCalls = 10,
            SupportCfc = true,
            SaveLiveBlob = true,
            EnableAffectiveDialog = true,
            SpeechConfig = new SpeechConfig(),
            ResponseModalities = new List<string> { "AUDIO" }
        };

        Assert.Equal(10, config.MaxLlmCalls);
        Assert.True(config.SupportCfc);
        Assert.True(config.SaveLiveBlob);
        Assert.True(config.EnableAffectiveDialog);
        Assert.NotNull(config.SpeechConfig);
        Assert.Contains("AUDIO", config.ResponseModalities);
    }
}
