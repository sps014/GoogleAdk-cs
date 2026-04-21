// ============================================================================
// Ollama Sample — MeaiLlm with Thinking (gemma4 / deepseek-r1)
// ============================================================================
// Demonstrates model-native thinking with a local Ollama model.
//
// Models that support thinking via TextReasoningContent:
//   • gemma4:e4b  (Google Gemma 4 — recommended)
//   • deepseek-r1 (DeepSeek R1 — explicit <think> blocks)
//   • qwq         (Qwen thinking model)
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
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Abstractions.Tools;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Planning;
using GoogleAdk.Core.Runner;
using GoogleAdk.ApiServer;
using GoogleAdk.Models.Meai;
using GoogleAdk.Models.Ollama;
using Microsoft.Extensions.AI;

Console.WriteLine("=== Ollama Thinking Sample (gemma4) ===\n");

AdkEnv.Load();

// ── Ollama client ─────────────────────────────────────────────────────────
// OllamaChatClient calls the Ollama HTTP API directly and automatically
// enables "think": true when the agent uses BuiltInPlanner with ThinkingConfig.
// Pull the model with: ollama pull gemma4:e4b
string modelName = "gemma4:e4b";
IChatClient ollamaClient = new OllamaChatClient(new Uri("http://localhost:11434"), modelName);
var llm = new MeaiLlm(modelName, ollamaClient);

// ── Agent with BuiltInPlanner (thinking enabled) ──────────────────────────
// BuiltInPlanner with ThinkingConfig activates the model's reasoning mode.
// MeaiLlm surfaces thought content via MEAI's TextReasoningContent.
var agent = new LlmAgent(new LlmAgentConfig
{
    Name = "ollama_agent",
    Model = llm,
    Planner = new BuiltInPlanner(new ThinkingConfig
    {
        // IncludeThoughts = true surfaces the internal reasoning in the UI.
        IncludeThoughts = true,
        // ThinkingBudget controls max reasoning tokens (provider-dependent).
        // Set to null to let the model decide, or 0 to disable thinking.
        ThinkingBudget = null,
    }),
    Instruction = """
        You are a careful, methodical assistant.
        Think step-by-step before answering — especially for reasoning-heavy
        questions like math problems, logic puzzles, code reviews, or planning tasks.
        """,
    Tools = [GetWeatherDataTool],
});

// ── Run mode ──────────────────────────────────────────────────────────────
if (args.Contains("--web"))
{
    await AdkServer.RunAsync(agent);
}

await ConsoleRunner.RunAsync(agent, cfg =>
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
    return new WeatherData(location, "Sunny with a chance of rainbows");
}

public record WeatherData(string Location, string Forecast);