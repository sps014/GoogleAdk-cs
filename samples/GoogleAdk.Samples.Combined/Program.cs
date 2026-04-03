// ============================================================================
// News Aggregator Sample — Sequential + Parallel Agent Pattern
// ============================================================================
//
// Mirrors the adk-js sequential_agent.ts pattern using C# ADK:
//
//   1. ParallelAgent — searches tech news AND sports news concurrently
//   2. SequentialAgent — runs parallel search first, then word frequency analysis
//   3. GoogleSearch — grounded research with live web data
//   4. FunctionTool (C#) — counts word frequency and renders markdown table
//   5. OutputKey — passes structured data between agents via session state
//
// Because SequentialAgent and ParallelAgent are NOT LlmAgents, their children
// never get transfer_to_agent tools — no DisallowTransfer flags needed.
// This matches the Python/JS ADK behavior exactly.
//
// Environment variables:
//   GOOGLE_GENAI_USE_VERTEXAI=True
//   GOOGLE_CLOUD_PROJECT=<your-project-id>
//   GOOGLE_CLOUD_LOCATION=us-central1
// ============================================================================

using GoogleAdk.Core;
using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Runner;
using GoogleAdk.Core.Tools;
using GoogleAdk.ApiServer;
using GoogleAdk.Models.Gemini;
using GoogleAdk.Samples.Combined;

AdkEnv.Load();

var model = GeminiModelFactory.Create("gemini-2.5-flash");

// ── Parallel: Tech News + Sports News ──────────────────────────────────────

var techNewsAgent = new LlmAgent(new LlmAgentConfig
{
    Name = "tech_news",
    Description = "Searches for the latest technology news.",
    Model = model,
    Instruction = """
        Search for the latest technology news from today. Cover: AI, startups,
        major product launches, and tech industry trends.
        Return a summary of 5-7 news items with headlines and brief descriptions.
        """,
    OutputKey = "tech_news_data",
    Tools = new List<IBaseTool> { GoogleSearchTool.Instance },
});

var sportsNewsAgent = new LlmAgent(new LlmAgentConfig
{
    Name = "sports_news",
    Description = "Searches for the latest sports news.",
    Model = model,
    Instruction = """
        Search for the latest sports news from today. Cover: major leagues (NFL, NBA,
        Premier League, etc.), tournaments, transfers, and notable results.
        Return a summary of 5-7 news items with headlines and brief descriptions.
        """,
    OutputKey = "sports_news_data",
    Tools = new List<IBaseTool> { GoogleSearchTool.Instance },
});

var parallelNews = new ParallelAgent(new BaseAgentConfig
{
    Name = "parallel_news_search",
    Description = "Searches tech and sports news concurrently.",
    SubAgents = new List<BaseAgent> { techNewsAgent, sportsNewsAgent },
});

// ── Sequential: Search → Analyze ───────────────────────────────────────────

var wordAnalyzer = new LlmAgent(new LlmAgentConfig
{
    Name = "word_analyzer",
    Description = "Analyzes word frequency across news articles.",
    Model = model,
    Instruction = """
        You have news data from two sources:
        - Tech news: {tech_news_data?}
        - Sports news: {sports_news_data?}

        Combine ALL of the text from both news summaries into a single string and
        call the count_word_frequency tool to find the top 5 most frequent words.
        
        Then present the results with:
        1. The markdown table from the tool
        2. A brief insight about what these top words tell us about today's news cycle
        """,
    Tools = [CombinedTools.CountWordFrequencyTool]
});

var rootAgent = new SequentialAgent(new BaseAgentConfig
{
    Name = "news_aggregator",
    Description = "Searches news in parallel, then analyzes word frequency.",
    SubAgents = new List<BaseAgent> { parallelNews, wordAnalyzer },
});

// ── Web mode: "dotnet run -- --web" launches the ADK dev UI ────────────────
if (args.Contains("--web"))
{
    await AdkServer.RunAsync(rootAgent);
    return;
}

// ── Console mode ───────────────────────────────────────────────────────────

var runner = new InMemoryRunner("news-aggregator", rootAgent);

var session = await runner.SessionService.CreateSessionAsync(
    new GoogleAdk.Core.Abstractions.Sessions.CreateSessionRequest
    {
        AppName = "news-aggregator",
        UserId = "user-1",
    });

Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
Console.WriteLine("║  ADK C# — News Aggregator: Sequential + Parallel        ║");
Console.WriteLine("║                                                          ║");
Console.WriteLine("║  Patterns: ParallelAgent + SequentialAgent + GoogleSearch║");
Console.WriteLine("║  + C# FunctionTool (word frequency)                     ║");
Console.WriteLine("║                                                          ║");
Console.WriteLine("║  Try: 'Get me the latest news'                          ║");
Console.WriteLine("║  Type 'quit' to exit.                                   ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
Console.WriteLine();

while (true)
{
    Console.Write("You: ");
    var input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input) || input.Equals("quit", StringComparison.OrdinalIgnoreCase))
        break;

    var userMessage = new Content
    {
        Role = "user",
        Parts = new List<Part> { new() { Text = input } }
    };

    Console.WriteLine();
    var sw = System.Diagnostics.Stopwatch.StartNew();

    // Use SSE streaming mode for real-time partial responses
    var runConfig = new GoogleAdk.Core.Agents.RunConfig
    {
        StreamingMode = GoogleAdk.Core.Agents.StreamingMode.Sse,
    };

    await foreach (var evt in runner.RunAsync("user-1", session.Id, userMessage, runConfig: runConfig))
    {
        var text = evt.Content?.Parts?.FirstOrDefault()?.Text;

        // Show function calls
        var calls = evt.GetFunctionCalls();
        foreach (var call in calls)
        {
            var partialTag = evt.Partial == true ? " (partial)" : "";
            Console.WriteLine($"  ⚡ [{evt.Author}] tool: {call.Name}{partialTag}");
        }

        // Show agent responses
        if (text != null)
        {
            if (evt.Partial == true)
            {
                // Streaming partial: show inline progress
                Console.Write($"\r  [{evt.Author}] streaming...");
            }
            else if (evt.Author == "word_analyzer")
            {
                Console.WriteLine($"\n[{evt.Author}]:");
                Console.WriteLine(text);
                Console.WriteLine();
            }
            else
            {
                Console.WriteLine($"\n  [{evt.Author}] ✓ completed");
            }
        }
    }

    sw.Stop();
    Console.WriteLine($"  ⏱ {sw.Elapsed.TotalSeconds:F1}s total");
    Console.WriteLine(new string('─', 60));
}
