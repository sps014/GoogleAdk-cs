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
    Tools = new List<IBaseTool> { new GoogleSearchTool() },
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
    Tools = new List<IBaseTool> { new GoogleSearchTool() },
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

await ConsoleRunner.RunAsync(rootAgent, config=>
{
    config.InitialMessage = "hi, get me news";
    config.CloseOnFinish = true;
});
