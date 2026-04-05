using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Abstractions.Sessions;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Runner;
using GoogleAdk.Core.Tools;

namespace GoogleAdk.E2e.Tests;

/// <summary>
/// Tests that track the E2E streaming event flow, verifying correct ordering that mirrors the
/// Python ADK implementation. Covers both mock-based (fast/deterministic) and real Gemini LLM tests.
///
/// KEY BUG FIXED: Partial function call events must NOT trigger tool execution.
/// Python (base_llm_flow.py:936): `if model_response_event.partial: return`
/// C# fix: added `if (mergedEvent.Partial == true) yield break;` in PostprocessAsync.
/// </summary>
public class StreamingEventOrderTests
{
    // --- Helpers ---

    private static Content UserMessage(string text) => new()
    {
        Role = "user",
        Parts = [new Part { Text = text }]
    };

    private static LlmResponse TextResponse(string text, bool partial = false) => new()
    {
        Content = new Content { Role = "model", Parts = [new Part { Text = text }] },
        Partial = partial
    };

    private static LlmResponse FunctionCallResponse(string name, Dictionary<string, object?> args) => new()
    {
        Content = new Content
        {
            Role = "model",
            Parts = [new Part { FunctionCall = new FunctionCall { Name = name, Args = args } }]
        }
    };

    private static async Task<List<Event>> CollectEventsAsync(
        Runner runner, string sessionId, Content userMessage, RunConfig? runConfig = null)
    {
        var events = new List<Event>();
        await foreach (var evt in runner.RunAsync("user-1", sessionId, userMessage, runConfig: runConfig))
            events.Add(evt);
        return events;
    }

    private static async Task<(InMemoryRunner runner, string sessionId)> SetupAsync(
        BaseAgent agent, string appName = "streaming-test")
    {
        var runner = new InMemoryRunner(appName, agent);
        var session = await runner.SessionService.CreateSessionAsync(new CreateSessionRequest
        {
            AppName = appName,
            UserId = "user-1",
        });
        return (runner, session.Id);
    }

    // --- Mock-based unit tests (fast, deterministic) ---

    [Fact]
    public async Task Mock_Sse_PartialChunksThenFinal_CorrectOrder()
    {
        var llm = MockLlm.FromGroups(new List<LlmResponse>
        {
            TextResponse("Hello ", partial: true),
            TextResponse("Hello world", partial: true),
            TextResponse("Hello world!", partial: false),
        });
        var agent = new LlmAgent(new LlmAgentConfig { Name = "test-agent", Model = llm });
        var (runner, sessionId) = await SetupAsync(agent);
        var events = await CollectEventsAsync(runner, sessionId, UserMessage("Hi"),
            new RunConfig { StreamingMode = StreamingMode.Sse });

        Assert.Equal(3, events.Count);
        Assert.True(events[0].Partial);
        Assert.True(events[1].Partial);
        Assert.NotEqual(true, events[2].Partial);
        Assert.True(events[2].IsFinalResponse());
        Assert.All(events, e => Assert.Equal("test-agent", e.Author));
    }

    [Fact]
    public async Task Mock_Unary_SingleResponse_NoPartials()
    {
        var llm = MockLlm.FromResponses(TextResponse("Complete answer."));
        var agent = new LlmAgent(new LlmAgentConfig { Name = "test-agent", Model = llm });
        var (runner, sessionId) = await SetupAsync(agent);
        var events = await CollectEventsAsync(runner, sessionId, UserMessage("Hi"),
            new RunConfig { StreamingMode = StreamingMode.None });

        Assert.Single(events);
        Assert.NotEqual(true, events[0].Partial);
        Assert.True(events[0].IsFinalResponse());
        Assert.Equal("Complete answer.", events[0].Content?.Parts?.First().Text);
    }

    [Fact]
    public async Task Mock_FunctionCall_CorrectEventSequence()
    {
        var llm = MockLlm.FromGroups(
            new List<LlmResponse> { FunctionCallResponse("get_weather", new() { ["city"] = "London" }) },
            new List<LlmResponse> { TextResponse("London is Cloudy, 14 degrees.") });
        var agent = new LlmAgent(new LlmAgentConfig
        {
            Name = "weather-agent",
            Model = llm,
            Tools = [StreamingTestTools.GetWeatherTool],
        });
        var (runner, sessionId) = await SetupAsync(agent);
        var events = await CollectEventsAsync(runner, sessionId, UserMessage("Weather in London?"));

        Assert.True(events.Count >= 3, $"Expected >= 3 events, got {events.Count}");
        Assert.Single(events[0].GetFunctionCalls());
        Assert.Equal("get_weather", events[0].GetFunctionCalls()[0].Name);
        Assert.Single(events[1].GetFunctionResponses());
        Assert.Equal("get_weather", events[1].GetFunctionResponses()[0].Name);
        Assert.True(events.Last().IsFinalResponse());
    }

    /// <summary>
    /// BUG REPRO + FIX VERIFICATION: Partial FC events must NOT execute tools.
    /// Before fix: toolCallCount == 2 (executed on partial AND final FC).
    /// After fix:  toolCallCount == 1 (only executed on final non-partial FC).
    /// </summary>
    [Fact]
    public async Task Mock_Sse_PartialFunctionCall_DoesNotExecuteTool_BugFix()
    {
        var toolCallCount = 0;
        var llm = MockLlm.FromGroups(
            new List<LlmResponse>
            {
                new LlmResponse
                {
                    Content = new Content { Role = "model", Parts = [new Part
                    {
                        FunctionCall = new FunctionCall { Name = "get_weather", Args = new() { ["city"] = "Tokyo" } }
                    }]},
                    Partial = true  // partial FC: must NOT execute
                },
                FunctionCallResponse("get_weather", new() { ["city"] = "Tokyo" }) // final FC: MUST execute
            },
            new List<LlmResponse> { TextResponse("Tokyo is Sunny, 22 degrees.") });

        var countingTool = new FunctionTool(
            "get_weather", "Gets weather for a city",
            (args, _) =>
            {
                Interlocked.Increment(ref toolCallCount);
                return Task.FromResult<object?>(new Dictionary<string, object?> { ["result"] = "Sunny, 22C" });
            },
            new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?> { ["city"] = new Dictionary<string, object?> { ["type"] = "string" } }
            });

        var agent = new LlmAgent(new LlmAgentConfig { Name = "test-agent", Model = llm, Tools = [countingTool] });
        var (runner, sessionId) = await SetupAsync(agent);
        var events = await CollectEventsAsync(runner, sessionId, UserMessage("Weather?"),
            new RunConfig { StreamingMode = StreamingMode.Sse });

        Assert.Equal(1, toolCallCount); // KEY: must be 1 after fix, was 2 before fix
        Assert.Single(events.Where(e => e.Partial == true && e.GetFunctionCalls().Count > 0).ToList());
        Assert.Single(events.Where(e => e.Partial != true && e.GetFunctionCalls().Count > 0).ToList());
    }

    [Fact]
    public async Task Mock_MultiStepToolLoop_CorrectEventSequence()
    {
        var llm = MockLlm.FromGroups(
            new List<LlmResponse> { FunctionCallResponse("get_weather", new() { ["city"] = "Sydney" }) },
            new List<LlmResponse> { FunctionCallResponse("get_current_time", new() { ["timezone"] = "Australia/Sydney" }) },
            new List<LlmResponse> { TextResponse("Sydney: Rainy, 18 degrees, time retrieved.") });
        var agent = new LlmAgent(new LlmAgentConfig
        {
            Name = "multi-tool-agent",
            Model = llm,
            Tools = [StreamingTestTools.GetWeatherTool, StreamingTestTools.GetCurrentTimeTool],
        });
        var (runner, sessionId) = await SetupAsync(agent);
        var events = await CollectEventsAsync(runner, sessionId, UserMessage("Sydney weather and time?"));

        Assert.True(events.Count >= 5, $"Expected >= 5 events, got {events.Count}");
        Assert.Equal("get_weather", events[0].GetFunctionCalls()[0].Name);
        Assert.Equal("get_weather", events[1].GetFunctionResponses()[0].Name);
        Assert.Equal("get_current_time", events[2].GetFunctionCalls()[0].Name);
        Assert.Equal("get_current_time", events[3].GetFunctionResponses()[0].Name);
        Assert.True(events[4].IsFinalResponse());
    }

    [Fact]
    public async Task Mock_PartialEventsNotSavedToSession()
    {
        var llm = MockLlm.FromGroups(new List<LlmResponse>
        {
            TextResponse("Hel", partial: true),
            TextResponse("Hello", partial: true),
            TextResponse("Hello World!", partial: false),
        });
        var agent = new LlmAgent(new LlmAgentConfig { Name = "test-agent", Model = llm });
        var (runner, sessionId) = await SetupAsync(agent);
        await CollectEventsAsync(runner, sessionId, UserMessage("Hi"),
            new RunConfig { StreamingMode = StreamingMode.Sse });

        var session = await runner.SessionService.GetSessionAsync(new GetSessionRequest
        {
            AppName = "streaming-test", UserId = "user-1", SessionId = sessionId,
        });

        Assert.NotNull(session);
        Assert.Empty(session!.Events.Where(e => e.Partial == true));
        var modelEvents = session.Events.Where(e => e.Author == "test-agent").ToList();
        Assert.Single(modelEvents);
        Assert.Equal("Hello World!", modelEvents[0].Content?.Parts?.First().Text);
    }

    [Fact]
    public async Task Mock_AllEvents_HaveProperMetadata()
    {
        var llm = MockLlm.FromGroups(new List<LlmResponse>
        {
            TextResponse("chunk1", partial: true),
            TextResponse("chunk1 chunk2", partial: false),
        });
        var agent = new LlmAgent(new LlmAgentConfig { Name = "test-agent", Model = llm });
        var (runner, sessionId) = await SetupAsync(agent);
        var events = await CollectEventsAsync(runner, sessionId, UserMessage("Hi"),
            new RunConfig { StreamingMode = StreamingMode.Sse });

        foreach (var evt in events)
        {
            Assert.False(string.IsNullOrEmpty(evt.Id));
            Assert.False(string.IsNullOrEmpty(evt.InvocationId));
            Assert.True(evt.Timestamp > 0);
            Assert.Equal("test-agent", evt.Author);
        }
        Assert.Single(events.Select(e => e.InvocationId).Distinct());
    }

    [Fact]
    public async Task Mock_PauseOnToolCalls_SuspendsAfterFunctionCall()
    {
        var toolCalled = false;
        var llm = MockLlm.FromResponses(FunctionCallResponse("add", new() { ["a"] = 3.0, ["b"] = 4.0 }));
        var countingTool = new FunctionTool("add", "Adds two numbers",
            (_, _) => { toolCalled = true; return Task.FromResult<object?>(7.0); },
            new Dictionary<string, object?> { ["type"] = "object", ["properties"] = new Dictionary<string, object?> { ["a"] = new Dictionary<string, object?> { ["type"] = "number" }, ["b"] = new Dictionary<string, object?> { ["type"] = "number" } } });
        var agent = new LlmAgent(new LlmAgentConfig { Name = "test-agent", Model = llm, Tools = [countingTool] });
        var (runner, sessionId) = await SetupAsync(agent);
        var events = await CollectEventsAsync(runner, sessionId, UserMessage("Add 3+4"),
            new RunConfig { PauseOnToolCalls = true });

        Assert.False(toolCalled, "Tool must not execute when PauseOnToolCalls=true");
        Assert.Single(events);
        Assert.Single(events[0].GetFunctionCalls());
    }

    [Fact]
    public async Task Mock_AgentTransfer_EventsFromBothAgents()
    {
        var subLlm = MockLlm.FromResponses(TextResponse("I am the specialist. Here is your answer."));
        var subAgent = new LlmAgent(new LlmAgentConfig { Name = "specialist", Model = subLlm });
        var rootLlm = MockLlm.FromResponses(new LlmResponse
        {
            Content = new Content
            {
                Role = "model",
                Parts = [new Part { FunctionCall = new FunctionCall
                {
                    Name = "transfer_to_agent",
                    Args = new() { ["agentName"] = "specialist" }
                }}]
            }
        });
        var rootAgent = new LlmAgent(new LlmAgentConfig
        {
            Name = "root", Model = rootLlm, SubAgents = [subAgent],
        });
        var (runner, sessionId) = await SetupAsync(rootAgent);
        var events = await CollectEventsAsync(runner, sessionId, UserMessage("Help me"));

        Assert.True(events.Any(e => e.Author == "root"));
        Assert.True(events.Any(e => e.Author == "specialist"));
        Assert.True(events.Last().IsFinalResponse());
    }

    // --- Real Gemini LLM tests (integration) ---

    // --- SequentialAgent + ParallelAgent streaming tests ---

    [Fact]
    public async Task Mock_Sse_SequentialAgent_AllSubAgentsExecute()
    {
        // Two LlmAgents inside a SequentialAgent, both with streaming responses.
        // Verify BOTH sub-agents produce events when StreamingMode=Sse.
        var llm1 = MockLlm.FromGroups(new List<LlmResponse>
        {
            TextResponse("agent1 partial", partial: true),
            TextResponse("agent1 final"),
        });
        var llm2 = MockLlm.FromGroups(new List<LlmResponse>
        {
            TextResponse("agent2 partial", partial: true),
            TextResponse("agent2 final"),
        });
        var agent1 = new LlmAgent(new LlmAgentConfig { Name = "agent1", Model = llm1 });
        var agent2 = new LlmAgent(new LlmAgentConfig { Name = "agent2", Model = llm2 });
        var seq = new SequentialAgent(new BaseAgentConfig
        {
            Name = "seq",
            SubAgents = new List<BaseAgent> { agent1, agent2 },
        });
        var (runner, sessionId) = await SetupAsync(seq);
        var events = await CollectEventsAsync(runner, sessionId, UserMessage("Go"),
            new RunConfig { StreamingMode = StreamingMode.Sse });

        var agent1Events = events.Where(e => e.Author == "agent1").ToList();
        var agent2Events = events.Where(e => e.Author == "agent2").ToList();
        Assert.True(agent1Events.Count >= 1, $"agent1 should produce events, got {agent1Events.Count}");
        Assert.True(agent2Events.Count >= 1, $"agent2 should produce events, got {agent2Events.Count}");
        Assert.True(agent1Events.Any(e => e.IsFinalResponse()), "agent1 should have a final response");
        Assert.True(agent2Events.Any(e => e.IsFinalResponse()), "agent2 should have a final response");

        // Ordering: all agent1 events before any agent2 events
        var lastAgent1Idx = events.IndexOf(agent1Events.Last());
        var firstAgent2Idx = events.IndexOf(agent2Events.First());
        Assert.True(lastAgent1Idx < firstAgent2Idx, "agent1 events must precede agent2 events");
    }

    [Fact]
    public async Task Mock_Sse_ParallelAgent_AllSubAgentsExecute()
    {
        var llm1 = MockLlm.FromGroups(new List<LlmResponse>
        {
            TextResponse("branch1 partial", partial: true),
            TextResponse("branch1 final"),
        });
        var llm2 = MockLlm.FromGroups(new List<LlmResponse>
        {
            TextResponse("branch2 partial", partial: true),
            TextResponse("branch2 final"),
        });
        var agent1 = new LlmAgent(new LlmAgentConfig { Name = "branch1", Model = llm1 });
        var agent2 = new LlmAgent(new LlmAgentConfig { Name = "branch2", Model = llm2 });
        var par = new ParallelAgent(new BaseAgentConfig
        {
            Name = "par",
            SubAgents = new List<BaseAgent> { agent1, agent2 },
        });
        var (runner, sessionId) = await SetupAsync(par);
        var events = await CollectEventsAsync(runner, sessionId, UserMessage("Go"),
            new RunConfig { StreamingMode = StreamingMode.Sse });

        var b1Events = events.Where(e => e.Author == "branch1").ToList();
        var b2Events = events.Where(e => e.Author == "branch2").ToList();
        Assert.True(b1Events.Count >= 1, $"branch1 should produce events, got {b1Events.Count}");
        Assert.True(b2Events.Count >= 1, $"branch2 should produce events, got {b2Events.Count}");
        Assert.True(b1Events.Any(e => e.IsFinalResponse()), "branch1 should have a final response");
        Assert.True(b2Events.Any(e => e.IsFinalResponse()), "branch2 should have a final response");
    }

    [Fact]
    public async Task Mock_Sse_SequentialPlusParallel_MirrorsCombinedPattern()
    {
        // Mirrors the Combined sample: SequentialAgent → ParallelAgent → LlmAgents → LlmAgent
        var techLlm = MockLlm.FromGroups(new List<LlmResponse>
        {
            TextResponse("Tech news partial", partial: true),
            TextResponse("Tech news: AI breakthrough today"),
        });
        var sportsLlm = MockLlm.FromGroups(new List<LlmResponse>
        {
            TextResponse("Sports news partial", partial: true),
            TextResponse("Sports news: Championship finals"),
        });
        var analyzerLlm = MockLlm.FromGroups(new List<LlmResponse>
        {
            TextResponse("Analysis partial", partial: true),
            TextResponse("Word frequency: news(3), today(1)"),
        });

        var tech = new LlmAgent(new LlmAgentConfig { Name = "tech_news", Model = techLlm, OutputKey = "tech_data" });
        var sports = new LlmAgent(new LlmAgentConfig { Name = "sports_news", Model = sportsLlm, OutputKey = "sports_data" });
        var parallel = new ParallelAgent(new BaseAgentConfig
        {
            Name = "parallel_search",
            SubAgents = new List<BaseAgent> { tech, sports },
        });
        var analyzer = new LlmAgent(new LlmAgentConfig
        {
            Name = "word_analyzer",
            Model = analyzerLlm,
            Instruction = "Analyze: {tech_data?} {sports_data?}",
        });
        var root = new SequentialAgent(new BaseAgentConfig
        {
            Name = "news_aggregator",
            SubAgents = new List<BaseAgent> { parallel, analyzer },
        });

        var (runner, sessionId) = await SetupAsync(root);
        var events = await CollectEventsAsync(runner, sessionId, UserMessage("Get news"),
            new RunConfig { StreamingMode = StreamingMode.Sse });

        var techEvents = events.Where(e => e.Author == "tech_news").ToList();
        var sportsEvents = events.Where(e => e.Author == "sports_news").ToList();
        var analyzerEvents = events.Where(e => e.Author == "word_analyzer").ToList();

        Assert.True(techEvents.Count >= 1, $"tech_news should produce events, got {techEvents.Count}");
        Assert.True(sportsEvents.Count >= 1, $"sports_news should produce events, got {sportsEvents.Count}");
        Assert.True(analyzerEvents.Count >= 1, $"word_analyzer should produce events, got {analyzerEvents.Count}");
        Assert.True(analyzerEvents.Any(e => e.IsFinalResponse()), "word_analyzer should have a final response");

        // Parallel events should come before analyzer events
        var lastParallelIdx = Math.Max(
            events.IndexOf(techEvents.Last()),
            events.IndexOf(sportsEvents.Last()));
        var firstAnalyzerIdx = events.IndexOf(analyzerEvents.First());
        Assert.True(lastParallelIdx < firstAnalyzerIdx, "Parallel events must precede analyzer events");
    }

    [Fact]
    public async Task Mock_Sse_SequentialAgent_WithToolCalls_BothAgentsExecute()
    {
        // First agent calls a tool, second agent produces text.
        // Ensures FC handling in first agent doesn't prevent second agent from running.
        var callCount = 0;
        var llm1 = MockLlm.FromGroups(
            new List<LlmResponse> { FunctionCallResponse("get_weather", new() { ["city"] = "NY" }) },
            new List<LlmResponse> { TextResponse("NY is sunny") });
        var llm2 = MockLlm.FromGroups(new List<LlmResponse>
        {
            TextResponse("Summary partial", partial: true),
            TextResponse("Summary: The weather is great."),
        });

        var weatherTool = new FunctionTool("get_weather", "Gets weather",
            (args, _) =>
            {
                Interlocked.Increment(ref callCount);
                return Task.FromResult<object?>(new Dictionary<string, object?> { ["result"] = "Sunny, 25C" });
            },
            new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?> { ["city"] = new Dictionary<string, object?> { ["type"] = "string" } }
            });

        var agent1 = new LlmAgent(new LlmAgentConfig { Name = "weather_agent", Model = llm1, Tools = [weatherTool] });
        var agent2 = new LlmAgent(new LlmAgentConfig { Name = "summary_agent", Model = llm2 });
        var seq = new SequentialAgent(new BaseAgentConfig
        {
            Name = "seq",
            SubAgents = new List<BaseAgent> { agent1, agent2 },
        });
        var (runner, sessionId) = await SetupAsync(seq);
        var events = await CollectEventsAsync(runner, sessionId, UserMessage("Weather and summary"),
            new RunConfig { StreamingMode = StreamingMode.Sse });

        Assert.Equal(1, callCount);
        Assert.True(events.Any(e => e.Author == "weather_agent" && e.IsFinalResponse()));
        Assert.True(events.Any(e => e.Author == "summary_agent" && e.IsFinalResponse()));
    }

    // --- Real Gemini LLM tests (integration) ---

    [Fact]
    public async Task RealLlm_SimpleText_FinalResponseYielded()
    {
        var agent = new LlmAgent(new LlmAgentConfig
        {
            Name = "test-agent",
            Model = "gemini-2.0-flash",
            Instruction = "Reply with exactly one short sentence.",
        });
        var (runner, sessionId) = await SetupAsync(agent, "real-llm-test");
        var events = await CollectEventsAsync(runner, sessionId, UserMessage("Say hello in one sentence."));

        Assert.True(events.Count > 0);
        var finalEvents = events.Where(e => e.IsFinalResponse()).ToList();
        Assert.Single(finalEvents);
        Assert.False(string.IsNullOrWhiteSpace(finalEvents[0].Content?.Parts?.First().Text));
    }

    [Fact]
    public async Task RealLlm_Sse_StreamsPartialChunks()
    {
        var agent = new LlmAgent(new LlmAgentConfig
        {
            Name = "test-agent",
            Model = "gemini-2.0-flash",
            Instruction = "Write exactly two sentences.",
        });
        var (runner, sessionId) = await SetupAsync(agent, "real-sse-test");
        var events = await CollectEventsAsync(runner, sessionId,
            UserMessage("Describe the sky in two sentences."),
            new RunConfig { StreamingMode = StreamingMode.Sse });

        Assert.True(events.Count > 0);
        var final = events.Where(e => e.IsFinalResponse()).ToList();
        Assert.Single(final);

        var partials = events.Where(e => e.Partial == true).ToList();
        if (partials.Count > 0)
        {
            var lastPartialIndex = events.IndexOf(partials.Last());
            var finalIndex = events.IndexOf(final[0]);
            Assert.True(lastPartialIndex < finalIndex, "All partial events must precede the final event");
        }
    }

    [Fact]
    public async Task RealLlm_WithWeatherTool_CallsToolAndRespondsFinal()
    {
        var agent = new LlmAgent(new LlmAgentConfig
        {
            Name = "weather-agent",
            Model = "gemini-2.0-flash",
            Instruction = "You are a weather assistant. Always use get_weather to answer weather questions.",
            Tools = [StreamingTestTools.GetWeatherTool],
        });
        var (runner, sessionId) = await SetupAsync(agent, "real-tool-test");
        var events = await CollectEventsAsync(runner, sessionId, UserMessage("What is the weather in London?"));

        var fcEvents = events.Where(e => e.GetFunctionCalls().Count > 0).ToList();
        Assert.True(fcEvents.Count > 0, "Agent should call get_weather");
        Assert.Equal("GetWeather", fcEvents[0].GetFunctionCalls()[0].Name);

        var frEvents = events.Where(e => e.GetFunctionResponses().Count > 0).ToList();
        Assert.True(frEvents.Count > 0, "Should have tool response events");

        var finalEvent = events.Last(e => e.IsFinalResponse());
        Assert.False(string.IsNullOrWhiteSpace(finalEvent.Content?.Parts?.First().Text));

        // Ordering: FC < FR < final text
        var firstFcIdx = events.IndexOf(fcEvents[0]);
        var firstFrIdx = events.IndexOf(frEvents[0]);
        var finalIdx = events.IndexOf(finalEvent);
        Assert.True(firstFcIdx < firstFrIdx, "FC must precede FR");
        Assert.True(firstFrIdx < finalIdx, "FR must precede final text");
    }

    [Fact]
    public async Task RealLlm_Sse_WithTool_PartialFcExecutesToolExactlyOnce_BugFix()
    {
        // In SSE mode the model may stream a FC as partial chunks before the final non-partial FC.
        // The invariant: only non-partial FC events trigger tool execution.
        // callCount must equal nonPartialFcCount (no extra calls from partial events).
        var callCount = 0;
        var countingWeatherTool = new FunctionTool(
            "get_weather",
            "Returns current weather for a city as a short string",
            (args, _) =>
            {
                Interlocked.Increment(ref callCount);
                var city = args.GetValueOrDefault("city")?.ToString() ?? "unknown";
                return Task.FromResult<object?>($"Sunny, 20 degrees in {city}");
            },
            new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?> { ["city"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "City name" } },
                ["required"] = new[] { "city" }
            });

        var agent = new LlmAgent(new LlmAgentConfig
        {
            Name = "weather-agent",
            Model = "gemini-2.0-flash",
            Instruction = "Use get_weather to answer weather questions.",
            Tools = [countingWeatherTool],
        });
        var (runner, sessionId) = await SetupAsync(agent, "real-sse-bug-test");
        var events = await CollectEventsAsync(runner, sessionId, UserMessage("Weather in Tokyo?"),
            new RunConfig { StreamingMode = StreamingMode.Sse });

        // The invariant: tool executions == non-partial FC events (no extra calls from partial events).
        var nonPartialFcCount = events.Count(e => e.Partial != true && e.GetFunctionCalls().Count > 0);
        Assert.True(callCount >= 1, "Tool should be called at least once");
        Assert.Equal(nonPartialFcCount, callCount);
        Assert.True(events.Any(e => e.IsFinalResponse()));
    }

    [Fact]
    public async Task RealLlm_WithAddTool_ComputesCorrectAnswer()
    {
        var agent = new LlmAgent(new LlmAgentConfig
        {
            Name = "math-agent",
            Model = "gemini-2.0-flash",
            Instruction = "You are a math assistant. Use the add tool for arithmetic and report the result.",
            Tools = [StreamingTestTools.AddTool],
        });
        var (runner, sessionId) = await SetupAsync(agent, "real-add-test");
        var events = await CollectEventsAsync(runner, sessionId, UserMessage("What is 15 + 27?"));

        var fcEvents = events.Where(e => e.GetFunctionCalls().Any(c => string.Equals(c.Name, "Add", StringComparison.OrdinalIgnoreCase))).ToList();
        Assert.True(fcEvents.Count > 0, "Agent should call the add tool");

        var finalText = events.Last(e => e.IsFinalResponse()).Content?.Parts?.First().Text ?? "";
        Assert.Contains("42", finalText);
    }

    [Fact]
    public async Task RealLlm_SubAgent_TransfersAndResponds()
    {
        var specialistAgent = new LlmAgent(new LlmAgentConfig
        {
            Name = "weather_specialist",
            Description = "Specializes in weather information",
            Model = "gemini-2.0-flash",
            Instruction = "You are a weather specialist. Always use get_weather to answer.",
            Tools = [StreamingTestTools.GetWeatherTool],
        });
        var routerAgent = new LlmAgent(new LlmAgentConfig
        {
            Name = "router",
            Model = "gemini-2.0-flash",
            Instruction = "Route weather questions to the weather_specialist sub-agent.",
            SubAgents = [specialistAgent],
        });
        var (runner, sessionId) = await SetupAsync(routerAgent, "real-subagent-test");
        var events = await CollectEventsAsync(runner, sessionId, UserMessage("What is the weather in Sydney?"));

        Assert.True(events.Count > 0);
        Assert.True(events.Any(e => e.IsFinalResponse()));

        for (int i = 0; i < events.Count - 1; i++)
        {
            if (events[i].IsFinalResponse())
            {
                var sameAuthorAfter = events.Skip(i + 1)
                    .Where(e => e.Author == events[i].Author && e.Content != null)
                    .ToList();
                Assert.Empty(sameAuthorAfter);
            }
        }
    }

    [Fact]
    public async Task RealLlm_Sse_SequentialAgent_AllSubAgentsExecute()
    {
        // Real Gemini test: SequentialAgent with 2 LlmAgent sub-agents in SSE mode.
        // Verifies that BOTH sub-agents produce events when streaming is enabled.
        var agent1 = new LlmAgent(new LlmAgentConfig
        {
            Name = "greeter",
            Model = "gemini-2.0-flash",
            Instruction = "Reply with a single greeting sentence.",
            OutputKey = "greeting",
        });
        var agent2 = new LlmAgent(new LlmAgentConfig
        {
            Name = "translator",
            Model = "gemini-2.0-flash",
            Instruction = "Translate the following greeting to French: {greeting?}",
        });
        var seq = new SequentialAgent(new BaseAgentConfig
        {
            Name = "seq",
            SubAgents = new List<BaseAgent> { agent1, agent2 },
        });
        var (runner, sessionId) = await SetupAsync(seq, "real-seq-sse-test");
        var events = await CollectEventsAsync(runner, sessionId, UserMessage("Say hello"),
            new RunConfig { StreamingMode = StreamingMode.Sse });

        var greeterEvents = events.Where(e => e.Author == "greeter").ToList();
        var translatorEvents = events.Where(e => e.Author == "translator").ToList();
        Assert.True(greeterEvents.Count >= 1, $"greeter should produce events, got {greeterEvents.Count}");
        Assert.True(translatorEvents.Count >= 1, $"translator should produce events, got {translatorEvents.Count}");
        Assert.True(greeterEvents.Any(e => e.IsFinalResponse()), "greeter should have final response");
        Assert.True(translatorEvents.Any(e => e.IsFinalResponse()), "translator should have final response");
    }

    [Fact]
    public async Task RealLlm_Sse_SequentialAgent_WithTool_AllAgentsExecute()
    {
        // Real Gemini test mirroring the Combined sample pattern:
        // SequentialAgent → [weather_agent (uses tool)] → [summarizer]
        var weatherAgent = new LlmAgent(new LlmAgentConfig
        {
            Name = "weather_fetcher",
            Model = "gemini-2.0-flash",
            Instruction = "Use the get_weather tool to get weather for the requested city, then report the result.",
            Tools = [StreamingTestTools.GetWeatherTool],
            OutputKey = "weather_data",
        });
        var summaryAgent = new LlmAgent(new LlmAgentConfig
        {
            Name = "summarizer",
            Model = "gemini-2.0-flash",
            Instruction = "Summarize in one sentence: {weather_data?}",
        });
        var seq = new SequentialAgent(new BaseAgentConfig
        {
            Name = "pipeline",
            SubAgents = new List<BaseAgent> { weatherAgent, summaryAgent },
        });
        var (runner, sessionId) = await SetupAsync(seq, "real-seq-tool-sse-test");
        var events = await CollectEventsAsync(runner, sessionId, UserMessage("Weather in London"),
            new RunConfig { StreamingMode = StreamingMode.Sse });

        var fetcherEvents = events.Where(e => e.Author == "weather_fetcher").ToList();
        var summaryEvents = events.Where(e => e.Author == "summarizer").ToList();
        Assert.True(fetcherEvents.Count >= 1, $"weather_fetcher should produce events, got {fetcherEvents.Count}");
        Assert.True(summaryEvents.Count >= 1, $"summarizer should produce events, got {summaryEvents.Count}");
        Assert.True(fetcherEvents.Any(e => e.GetFunctionCalls().Count > 0), "weather_fetcher should call tool");
        Assert.True(summaryEvents.Any(e => e.IsFinalResponse()), "summarizer should have final response");
    }
}
