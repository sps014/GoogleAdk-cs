// ============================================================================
// Complex Multi-Agent Orchestration Sample
// ============================================================================
//
// This sample demonstrates a multi-agent orchestration pipeline where:
//
//   1. A "Researcher" agent uses Google Search and MCP tools to gather data
//   2. An "Analyst" agent analyzes the research results
//   3. A "Writer" agent produces the final synthesized report
//   4. A SequentialAgent orchestrates them in order
//   5. A root LLM agent coordinates the overall workflow
//
// Environment variables:
//   GOOGLE_GENAI_USE_VERTEXAI=True
//   GOOGLE_CLOUD_PROJECT=<your-project-id>
//   GOOGLE_CLOUD_LOCATION=us-central1
//
// Or for Google AI Studio:
//   GOOGLE_API_KEY=<your-api-key>
//
// For MCP features (optional):
//   MCP_SERVER_URL=http://localhost:8788/mcp
//
// Usage:
//   dotnet run --project samples/GoogleAdk.Samples.Orchestration
// ============================================================================

using GoogleAdk.Core;
using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Abstractions.Tools;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Runner;
using GoogleAdk.Core.Tools;
using GoogleAdk.Models.Gemini;
using GoogleAdk.Tools.Mcp;

AdkEnv.Load();

// ── Create a Gemini model from environment variables ───────────────────────
var model = "gemini-2.5-flash";

// Tools are auto-generated from [FunctionTool] methods in SampleTools.
// No manual schema or boilerplate needed — just use SampleTools.XxxTool.

// ── Define specialized sub-agents ──────────────────────────────────────────

var researchAgent = new LlmAgent(new LlmAgentConfig
{
    Name = "researcher",
    Description = "A research specialist that gathers information using search and data tools.",
    Model = model,
    Instruction = """
        You are a thorough researcher. Your job is to gather relevant data and information
        on the topic the user asks about. Use the available tools to collect weather data,
        news headlines, and any calculations needed. Present your findings as structured
        bullet points. Be factual and cite your sources (tool names).
        """,
    Tools = new List<IBaseTool> { GetWeatherTool, SearchNewsTool, CalculateTool },
});

var analystAgent = new LlmAgent(new LlmAgentConfig
{
    Name = "analyst",
    Description = "An analyst that interprets data and identifies patterns and insights.",
    Model = model,
    Instruction = """
        You are a data analyst. Review the research findings from the previous conversation
        and provide analytical insights. Identify trends, patterns, correlations, and notable 
        observations. Structure your analysis with clear sections: Key Findings, Trends, 
        and Recommendations. Be specific and data-driven.
        """,
});

var writerAgent = new LlmAgent(new LlmAgentConfig
{
    Name = "writer",
    Description = "A technical writer that produces polished reports from research and analysis.",
    Model = model,
    Instruction = """
        You are an expert technical writer. Based on the research and analysis provided 
        in the conversation, produce a concise, well-structured final report. Use markdown
        formatting with headers, bullet points, and emphasis where appropriate.
        The report should include: Executive Summary, Detailed Findings, Analysis, 
        and Conclusion. Keep it professional and actionable.
        """,
});

// ── Build the orchestration pipeline ───────────────────────────────────────

// Sequential pipeline: research → analyze → write
var pipeline = new SequentialAgent(new BaseAgentConfig
{
    Name = "research_pipeline",
    Description = "A sequential pipeline that researches, analyzes, and writes a report.",
    SubAgents = new List<BaseAgent>
    {
        researchAgent,
        analystAgent,
        writerAgent,
    }
});

// ── Optional: Add MCP tools if MCP server URL is configured ────────────────

var mcpServerUrl = Environment.GetEnvironmentVariable("MCP_SERVER_URL");
BaseToolset? mcpToolset = null;

if (!string.IsNullOrEmpty(mcpServerUrl))
{
    Console.WriteLine($"[INFO] MCP server configured: {mcpServerUrl}");
    mcpToolset = new McpToolset(
        new HttpConnectionParams { Url = mcpServerUrl },
        prefix: "mcp");
}

// ── Root coordinator agent ─────────────────────────────────────────────────

// The coordinator uses the pipeline as an AgentTool and can also use
// standalone tools for quick queries. It decides which approach to use.
var rootAgent = new LlmAgent(new LlmAgentConfig
{
    Name = "coordinator",
    Description = "The main coordinator agent that routes requests to the appropriate workflow.",
    Model = model,
    Instruction = """
        You are an intelligent coordinator. You have access to a full research pipeline
        (via the 'research_pipeline' tool) that will research, analyze, and write a report.
        
        For comprehensive questions, delegate to the research_pipeline tool.
        For simple questions, you can answer directly or use individual tools.

        Always present the final output clearly to the user.
        """,
    Tools = new List<IBaseTool>
    {
        new AgentTool(pipeline),
        GetWeatherTool,   // Also available directly for quick queries
        CalculateTool,    // Also available directly for quick queries
    },
});

// ── Create and run ─────────────────────────────────────────────────────────

var runner = new InMemoryRunner("orchestration-sample", rootAgent);

// Create a persistent session so conversation history is preserved across turns
var session = await runner.SessionService.CreateSessionAsync(
    new GoogleAdk.Core.Abstractions.Sessions.CreateSessionRequest
    {
        AppName = "orchestration-sample",
        UserId = "user-1",
    });

Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
Console.WriteLine("║  ADK C# — Multi-Agent Orchestration Sample              ║");
Console.WriteLine("║  Type a question or 'quit' to exit.                     ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
Console.WriteLine();

// Interactive loop
while (true)
{
    Console.Write("You: ");
    var input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input) || input.Equals("quit", StringComparison.OrdinalIgnoreCase))
        break;

    var userMessage = new Content
    {
        Role = "user",
        Parts = new List<Part> { new Part { Text = input } }
    };

    Console.WriteLine();
    var eventCount = 0;

    await foreach (var evt in runner.RunAsync("user-1", session.Id, userMessage))
    {
        eventCount++;

        // Show which agent is responding
        var author = evt.Author ?? "system";
        var text = evt.Content?.Parts?.FirstOrDefault()?.Text;

        if (text != null && evt.Partial != true)
        {
            Console.WriteLine($"[{author}]: {text}");
            Console.WriteLine();
        }

        // Show function calls for transparency
        var calls = evt.GetFunctionCalls();
        if (calls.Count > 0)
        {
            foreach (var call in calls)
            {
                Console.WriteLine($"  ⚡ {author} calling tool: {call.Name}");
            }
        }
    }

    Console.WriteLine($"  ({eventCount} events produced)");
    Console.WriteLine(new string('─', 60));
}

// Cleanup MCP if used
if (mcpToolset != null)
    await mcpToolset.DisposeAsync();

Console.WriteLine("Goodbye!");



/// <summary>Gets the current weather for a city. Returns temperature and conditions.</summary>
/// <param name="city">The city name</param>
[FunctionTool]
static object? GetWeather(string city)
{
    return new Dictionary<string, object?>
    {
        ["city"] = city,
        ["temperature_celsius"] = city.Contains("New York", StringComparison.OrdinalIgnoreCase) ? 22 : 18,
        ["condition"] = city.Contains("London", StringComparison.OrdinalIgnoreCase) ? "Rainy" : "Sunny",
        ["humidity_percent"] = 65,
        ["wind_kph"] = 12,
    };
}

/// <summary>Searches for recent news headlines about a topic.</summary>
/// <param name="topic">The topic to search news for</param>
[FunctionTool]
static object? SearchNews(string topic)
{
    var headlines = new[]
    {
            $"Breaking: New developments in {topic} sector show promising growth",
            $"Analysis: How {topic} is reshaping the global economy in 2025",
            $"Expert opinion: The future of {topic} according to industry leaders",
        };
    return new { topic, headlines, source = "simulated-news-api" };
}

/// <summary>Performs basic math calculations. Supports +, -, *, /.</summary>
/// <param name="expression">A math expression like '2 + 3 * 4'</param>
[FunctionTool]
static object? Calculate(string expression)
{
    try
    {
        var result = new System.Data.DataTable().Compute(expression, null);
        return new { expression, result = result?.ToString() };
    }
    catch
    {
        return new { expression, error = "Invalid expression" };
    }
}