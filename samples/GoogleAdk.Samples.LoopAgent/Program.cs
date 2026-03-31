// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

// ============================================================================
// Loop Agent Sample — Iterative Refinement
// ============================================================================
//
// Demonstrates a LoopAgent that iteratively refines content:
//   1. A "drafter" agent writes or improves content
//   2. A "critic" agent reviews and scores it
//   3. The critic escalates (breaks the loop) when quality threshold is met
//
// The loop runs up to 5 iterations or until the critic is satisfied.
//
// Environment variables:
//   GOOGLE_GENAI_USE_VERTEXAI=True
//   GOOGLE_CLOUD_PROJECT=<your-project-id>
//   GOOGLE_CLOUD_LOCATION=us-central1
// ============================================================================

using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Runner;
using GoogleAdk.Dev;
using GoogleAdk.Models.Gemini;

var model = GeminiModelFactory.Create("gemini-2.5-flash");

var drafter = new LlmAgent(new LlmAgentConfig
{
    Name = "drafter",
    Description = "Writes and iteratively improves content based on feedback.",
    Model = model,
    Instruction = """
        You are a skilled writer. On the first turn, write a short piece (3-4 sentences)
        based on the user's topic. On subsequent turns, the critic will have provided
        feedback — use it to improve your draft. Show only your latest improved version,
        not the history.
        """,
});

var critic = new LlmAgent(new LlmAgentConfig
{
    Name = "critic",
    Description = "Reviews writing quality and provides improvement feedback.",
    Model = model,
    Instruction = """
        You are a writing critic. Review the drafter's latest piece and:
        1. Score it 1-10 on clarity, engagement, and conciseness 
        2. If the average score is >= 8, respond ONLY with: "APPROVED — this is excellent."
        3. If the average score is < 8, give 2-3 specific, actionable improvement suggestions

        IMPORTANT: When you approve (score >= 8), you MUST call the escalate tool to 
        end the review loop.
        """,
    Tools = new List<IBaseTool> { GoogleAdk.Samples.LoopAgent.LoopTools.EscalateTool },
});

var refinementLoop = new LoopAgent(new LoopAgentConfig
{
    Name = "refinement_loop",
    Description = "Iteratively refines content through drafting and critique.",
    MaxIterations = 5,
    SubAgents = new List<BaseAgent> { drafter, critic },
});


if(args.Contains("--web"))
{
    AdkWeb.Root = refinementLoop;
    await AdkWeb.RunAsync();
    return;
}

var runner = new InMemoryRunner("loop-agent-sample", refinementLoop);

// Create a persistent session so conversation history is preserved across turns
var session = await runner.SessionService.CreateSessionAsync(
    new GoogleAdk.Core.Abstractions.Sessions.CreateSessionRequest
    {
        AppName = "loop-agent-sample",
        UserId = "user-1",
    });

Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
Console.WriteLine("║  ADK C# — Loop Agent Sample (Iterative Refinement)      ║");
Console.WriteLine("║  Give a topic; drafter + critic iterate until polished. ║");
Console.WriteLine("║  Type 'quit' to exit.                                   ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
Console.WriteLine();

while (true)
{
    Console.Write("Topic: ");
    var input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input) || input.Equals("quit", StringComparison.OrdinalIgnoreCase))
        break;

    var userMessage = new Content
    {
        Role = "user",
        Parts = new List<Part> { new() { Text = input } }
    };

    Console.WriteLine();
    int iteration = 0;
    string? lastAuthor = null;

    await foreach (var evt in runner.RunAsync("user-1", session.Id, userMessage))
    {
        var text = evt.Content?.Parts?.FirstOrDefault()?.Text;
        if (text == null || evt.Partial == true)
            continue;

        // Track iteration changes
        if (evt.Author == "drafter" && lastAuthor != "drafter")
        {
            iteration++;
            Console.WriteLine($"── Iteration {iteration} ─────────────────────────────────────");
        }

        var emoji = evt.Author == "drafter" ? "✏️" : "🔍";
        Console.WriteLine($"  {emoji} [{evt.Author}]:");
        Console.WriteLine($"  {text}");
        Console.WriteLine();
        lastAuthor = evt.Author;

        if (evt.Actions.Escalate == true)
            Console.WriteLine("  ✅ Critic approved — loop complete!");
    }
    Console.WriteLine(new string('─', 60));
}
