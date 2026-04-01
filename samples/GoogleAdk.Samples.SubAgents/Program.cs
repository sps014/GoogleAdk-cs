// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

// ============================================================================
// Sub-Agent Transfer Sample
// ============================================================================
//
// Demonstrates LLM-driven agent routing via transfer_to_agent.
// A root "receptionist" agent uses the processor pipeline to automatically
// discover its sub-agents and transfer conversations to the right specialist.
//
// Architecture:
//   receptionist (root)
//   ├── billing_agent    — handles billing / payment questions
//   ├── tech_support     — handles technical issues
//   └── sales_agent      — handles product / pricing questions
//
// The AgentTransferLlmRequestProcessor automatically adds a transfer_to_agent
// tool and instructions listing the available targets.
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

var billingAgent = new LlmAgent(new LlmAgentConfig
{
    Name = "billing_agent",
    Description = "Handles billing questions: invoices, payments, refunds, subscription changes.",
    Model = model,
    Instruction = """
        You are a billing specialist. Help users with invoices, payment methods,
        refunds, and subscription changes. Be precise with amounts and dates.
        If the user's question is unrelated to billing, transfer to an appropriate agent.
        """,
});

var techSupport = new LlmAgent(new LlmAgentConfig
{
    Name = "tech_support",
    Description = "Handles technical issues: bugs, errors, setup problems, troubleshooting.",
    Model = model,
    Instruction = """
        You are a technical support engineer. Help users debug errors, fix configuration
        issues, and troubleshoot problems. Ask clarifying questions about their environment.
        If the user's question is unrelated to tech support, transfer to an appropriate agent.
        """,
    Tools = new List<IBaseTool> { GoogleAdk.Samples.SubAgents.SupportTools.CheckStatusTool },
});

var salesAgent = new LlmAgent(new LlmAgentConfig
{
    Name = "sales_agent",
    Description = "Handles sales inquiries: pricing, product features, demos, upgrades.",
    Model = model,
    Instruction = """
        You are a friendly sales representative. Answer questions about product features,
        pricing tiers, and help users choose the right plan. Be enthusiastic but honest.
        If the user's question is unrelated to sales, transfer to an appropriate agent.
        """,
});

// Root agent with sub-agents — the processor pipeline auto-adds transfer_to_agent
var receptionist = new LlmAgent(new LlmAgentConfig
{
    Name = "receptionist",
    Description = "Routes user requests to the appropriate specialist agent.",
    Model = model,
    Instruction = """
        You are a helpful receptionist. Greet the user and determine which specialist
        can best help them:
        - billing_agent: for invoices, payments, refunds, subscriptions
        - tech_support: for bugs, errors, technical problems
        - sales_agent: for pricing, features, demos, upgrades
        
        Transfer to the right agent immediately. Don't try to answer domain-specific
        questions yourself.
        """,
    SubAgents = new List<BaseAgent> { billingAgent, techSupport, salesAgent },
});

var runner = new InMemoryRunner("sub-agent-transfer-sample", receptionist);


var runWeb = args.Contains("--web");
var enableA2a = args.Contains("--a2a");
if (runWeb)
{
    AdkWeb.Root = receptionist;
    await AdkWeb.RunAsync(enableA2a: enableA2a);
    return;
}

// Create a persistent session so conversation history is preserved across turns
var session = await runner.SessionService.CreateSessionAsync(
    new GoogleAdk.Core.Abstractions.Sessions.CreateSessionRequest
    {
        AppName = "sub-agent-transfer-sample",
        UserId = "user-1",
    });

Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
Console.WriteLine("║  ADK C# — Sub-Agent Transfer Sample                     ║");
Console.WriteLine("║  Chat with the receptionist — it routes to specialists. ║");
Console.WriteLine("║  Try: billing, tech, or sales questions.                ║");
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
    await foreach (var evt in runner.RunAsync("user-1", session.Id, userMessage))
    {
        var text = evt.Content?.Parts?.FirstOrDefault()?.Text;
        if (text != null && evt.Partial != true)
        {
            Console.WriteLine($"[{evt.Author}]: {text}");
            Console.WriteLine();
        }

        var calls = evt.GetFunctionCalls();
        foreach (var call in calls)
        {
            if (call.Name == "transfer_to_agent")
                Console.WriteLine($"  → Transferring to: {call.Args?.GetValueOrDefault("agentName")}");
            else
                Console.WriteLine($"  ⚡ {evt.Author} calling: {call.Name}");
        }
    }
    Console.WriteLine(new string('─', 60));
}
