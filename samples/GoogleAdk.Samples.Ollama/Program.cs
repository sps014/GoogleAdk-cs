// ============================================================================
// Ollama Sample — Multi-Agent Orchestration with Thinking (gemma4)
// ============================================================================
// Demonstrates multi-agent orchestration with a local Ollama model.
// Includes model-native thinking and streaming.
//
// Architecture:
//   coordinator (root)
//   └── research_pipeline (SequentialAgent)
//       ├── researcher (uses GetWeatherDataTool)
//       └── analyst (analyzes researcher's output)
//
// When thinking is enabled:
//   • The ConsoleRunner renders a grey "Thinking (agent)" panel showing
//     the model's internal reasoning before the final response.
//   • ADK Web renders a "Thought" chip in the chat panel.
//   • Thought content is excluded from tool results and output keys.
//
// Run modes:
//   dotnet run          → ConsoleRunner (interactive terminal)
//   dotnet run -- --web → ADK Web (http://localhost:5000)
// ============================================================================

using GoogleAdk.Core;
using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Abstractions.Tools;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Planning;
using GoogleAdk.Core.Runner;
using GoogleAdk.Core.Tools;
using GoogleAdk.ApiServer;
using GoogleAdk.Models.Meai;
using GoogleAdk.Models.Ollama;
using Microsoft.Extensions.AI;

Console.WriteLine("=== Ollama Multi-Agent Thinking Sample (gemma4) ===\n");
Console.WriteLine("Ask: compare London and Tokyo weather and generate a report.");

AdkEnv.Load();

// ── Ollama client ─────────────────────────────────────────────────────────
// OllamaChatClient calls the Ollama HTTP API directly and automatically
// enables "think": true when the agent uses BuiltInPlanner with ThinkingConfig.
// Pull the model with: ollama pull gemma4:e4b
string modelName = "gemma4:e4b";
IChatClient ollamaClient = new OllamaChatClient(new Uri("http://localhost:11434"), modelName);
var llm = new MeaiLlm(modelName, ollamaClient);

// Helper to create an agent with thinking enabled
LlmAgent CreateThinkingAgent(string name, string description, string instruction, string? outputKey = null, params IBaseTool[] tools)
{
    return new LlmAgent(new LlmAgentConfig
    {
        Name = name,
        Description = description,
        Model = llm,
        Planner = new BuiltInPlanner(new ThinkingConfig
        {
            // IncludeThoughts = true surfaces the internal reasoning in the UI.
            IncludeThoughts = true,
            ThinkingBudget = null,
        }),
        Instruction = instruction,
        OutputKey = outputKey,
        Tools = tools.ToList()
    });
}

// ── 1. Researcher Agent ──────────────────────────────────────────────────
var researcherAgent = CreateThinkingAgent(
    "researcher",
    "Gathers raw data and information.",
    """
    You are a thorough researcher. Use the tools available to gather factual data 
    based on the user's request. Present your findings clearly and concisely.
    """,
    "research_data",
    GetWeatherDataTool
);

// ── 2. Analyst Agent ─────────────────────────────────────────────────────
var analystAgent = CreateThinkingAgent(
    "analyst",
    "Analyzes data and provides insights.",
    """
    You are an expert analyst. Review the research data provided:
    {research_data?}
    
    Think carefully about what this data means, draw insights, and synthesize 
    a final, well-structured report.
    """
);

// ── 3. Orchestration Pipeline ────────────────────────────────────────────
var pipeline = new SequentialAgent(new BaseAgentConfig
{
    Name = "research_pipeline",
    Description = "A pipeline that researches a topic and then analyzes the findings.",
    SubAgents = new List<BaseAgent> { researcherAgent, analystAgent }
});

// ── 4. Coordinator Agent ─────────────────────────────────────────────────
var coordinator = CreateThinkingAgent(
    "coordinator",
    "Main coordinator agent that routes requests.",
    """
    You are an intelligent coordinator. You have access to a 'research_pipeline' 
    tool that will research data and provide deep analysis.
    
    For comprehensive questions or analysis requests, delegate to the research_pipeline tool.
    For simple questions, you can answer directly.
    """,
    null,
    new AgentTool(pipeline)
);

// ── Run mode ──────────────────────────────────────────────────────────────
if (args.Contains("--web"))
{
    await AdkServer.RunAsync(coordinator);
    return;
}

await ConsoleRunner.RunAsync(coordinator, cfg =>
{
    cfg.FigletText = "Ollama";
    cfg.DebugMode = true;
    // Enable streaming to see thought chunks arrive in real time.
    cfg.EnableStreaming = true;
});

Console.WriteLine("\n=== Ollama Sample Complete ===");

// ── Tools ─────────────────────────────────────────────────────────────────

/// <summary>
/// Fetches the current weather data for a given location.
/// </summary>
/// <param name="location">The location to get the weather for (e.g., 'New York')</param>
/// <returns>A WeatherData object containing the location and forecast</returns>
[FunctionTool]
static WeatherData? GetWeatherData(string location)
{
    if (location == "London")
        return new WeatherData("London", "Cloudy with higher chances of rain");
    return new WeatherData(location, "Sunny with a chance of rainbows");
}

public record WeatherData(string Location, string Forecast);