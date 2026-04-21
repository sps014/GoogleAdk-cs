// ============================================================================
// Thinking Sample — BuiltInPlanner with ThinkingConfig (Gemini 2.5 Pro)
// ============================================================================
// Demonstrates model-native thinking using BuiltInPlanner.
//
// When thinking is enabled:
//   • The ConsoleRunner renders a grey "Thinking (agent)" panel showing
//     the model's internal reasoning before the final response.
//   • ADK Web renders a "Thought" chip in the chat panel.
//   • Thought content is excluded from tool results and output keys.
//
// You can also enable thinking directly (without a planner) via:
//
//   GenerateContentConfig = new GenerateContentConfig
//   {
//       ThinkingConfig = new ThinkingConfig { ThinkingBudget = 8192, IncludeThoughts = true }
//   }
//
// ThinkingBudget controls the maximum number of reasoning tokens.
// Set to -1 for dynamic budget (model decides), or 0 to disable thinking.
// ============================================================================

using GoogleAdk.Core;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Planning;
using GoogleAdk.Core.Runner;
using GoogleAdk.Core.Abstractions.Tools;
using GoogleAdk.Models.Gemini;
using GoogleAdk.ApiServer;

Console.WriteLine("=== Thinking Sample (Gemini 2.5 Pro) ===\n");

AdkEnv.Load();

// ── Agent with BuiltInPlanner (thinking enabled) ──────────────────────────

var agent = new LlmAgent(new LlmAgentConfig
{
    Name = "thinking_agent",
    // Gemini 2.5 Pro supports model-native thinking.
    // Use "gemini-2.5-flash" for a faster/cheaper alternative.
    Model = "gemini-2.5-pro",
    Planner = new BuiltInPlanner(new ThinkingConfig
    {
        // Maximum tokens the model may spend reasoning.
        // Increase for harder multi-step problems.
        ThinkingBudget = 8192,
        // Include thought content in the response so it surfaces in the UI.
        IncludeThoughts = true,
    }),
    Instruction = """
        You are a careful, methodical assistant.
        Think step-by-step before answering — especially for reasoning-heavy
        questions like math problems, logic puzzles, code reviews, or planning tasks.
        """,
    Tools = [GetPrimeFactorsTool],
});


if(args.Contains("--web"))
{
    await AdkServer.RunAsync(agent);
}

// ── ConsoleRunner renders a grey "Thinking" panel before each response ─────

await ConsoleRunner.RunAsync(agent, cfg =>
{
    cfg.FigletText = "Thinking";
    cfg.DebugMode = true;
    // Enable streaming to see thought chunks arrive in real time.
    cfg.EnableStreaming = true;
});

Console.WriteLine("\n=== Thinking Sample Complete ===");

// ── Tools ──────────────────────────────────────────────────────────────────

/// <summary>
/// Computes the prime factors of a positive integer.
/// Use this tool when asked to factorise or find prime factors of a number.
/// </summary>
/// <param name="n">The positive integer to factorise (must be >= 2).</param>
/// <returns>The list of prime factors in ascending order.</returns>
[FunctionTool]
static List<int> GetPrimeFactors(int n)
{
    if (n < 2) return [];
    var factors = new List<int>();
    for (int d = 2; (long)d * d <= n; d++)
    {
        while (n % d == 0)
        {
            factors.Add(d);
            n /= d;
        }
    }
    if (n > 1) factors.Add(n);
    return factors;
}
