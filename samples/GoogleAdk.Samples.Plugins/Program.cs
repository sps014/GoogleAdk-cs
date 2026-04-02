// ============================================================================
// Plugins Sample — Security & Logging Plugins with LLM
// ============================================================================
//
// Demonstrates:
//   1. LoggingPlugin — logs all callback events to console
//   2. SecurityPlugin — policy-based tool call gating (deny/confirm/allow)
//   3. Custom IBasePolicyEngine — configurable deny list
//   4. Runner wiring plugins into an LLM flow
// ============================================================================

using GoogleAdk.Core;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Abstractions.Sessions;
using GoogleAdk.Core.Abstractions.Tools;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Artifacts;
using GoogleAdk.Core.Memory;
using GoogleAdk.Core.Plugins;
using GoogleAdk.Core.Runner;
using GoogleAdk.Core.Sessions;
using GoogleAdk.Core.Tools;
using GoogleAdk.Samples.Plugins;

AdkEnv.Load();

Console.WriteLine("=== Plugins Sample ===\n");

// ── 1. LoggingPlugin + SecurityPlugin ──────────────────────────────────────
var logs = new List<string>();
var loggingPlugin = new LoggingPlugin(logAction: msg => logs.Add(msg));

var customPolicy = new DenyListPolicyEngine(
    denyList: new[] { "dangerous_tool" },
    confirmList: new[] { "sensitive_tool" });

var securityPlugin = new SecurityPlugin(customPolicy);

var agent = new LlmAgent(new LlmAgentConfig
{
    Name = "plugin-agent",
    Model = "gemini-2.5-flash",
    Instruction = "Call safe_tool, then dangerous_tool, then sensitive_tool. Explain what happened.",
    Tools =
    [
        SamplePluginTools.SafeToolTool,
        SamplePluginTools.DangerousToolTool,
        SamplePluginTools.SensitiveToolTool
    ]
});

var runner = new Runner(new RunnerConfig
{
    AppName = "plugins-sample",
    Agent = agent,
    SessionService = new InMemorySessionService(),
    ArtifactService = new InMemoryArtifactService(),
    MemoryService = new InMemoryMemoryService(),
    Plugins = new BasePlugin[] { loggingPlugin, securityPlugin }
});

var session = await runner.SessionService.CreateSessionAsync(new CreateSessionRequest
{
    AppName = "plugins-sample",
    UserId = "user-1"
});

var userMessage = new Content
{
    Role = "user",
    Parts = [new Part { Text = "Demonstrate safe_tool, dangerous_tool, and sensitive_tool." }]
};

await foreach (var evt in runner.RunAsync("user-1", session.Id, userMessage))
{
    foreach (var response in evt.GetFunctionResponses())
    {
        var result = response.Response != null
            ? string.Join(", ", response.Response.Select(kv => $"{kv.Key}={kv.Value}"))
            : "(null)";
        Console.WriteLine($"Tool response ({response.Name}): {result}");
    }

    if (evt.Actions.RequestedToolConfirmations.Count > 0)
    {
        Console.WriteLine("Tool confirmation requested:");
        foreach (var confirmation in evt.Actions.RequestedToolConfirmations.Values)
            Console.WriteLine($"  FunctionCallId={confirmation.FunctionCallId}");
    }

    if (evt.IsFinalResponse() && evt.Content?.Parts != null)
    {
        foreach (var part in evt.Content.Parts)
        {
            if (!string.IsNullOrWhiteSpace(part.Text))
                Console.WriteLine($"Agent: {part.Text}");
        }
    }
}

Console.WriteLine($"\nCaptured {logs.Count} log entries:");
foreach (var log in logs.Take(8))
    Console.WriteLine($"  {log}");
if (logs.Count > 8)
    Console.WriteLine($"  ... and {logs.Count - 8} more");

Console.WriteLine("\n=== Plugins Sample Complete ===");

// ── Custom Policy Engine ───────────────────────────────────────────────────

public class DenyListPolicyEngine : IBasePolicyEngine
{
    private readonly HashSet<string> _denyList;
    private readonly HashSet<string> _confirmList;

    public DenyListPolicyEngine(IEnumerable<string> denyList, IEnumerable<string> confirmList)
    {
        _denyList = new HashSet<string>(denyList);
        _confirmList = new HashSet<string>(confirmList);
    }

    public Task<PolicyCheckResult> EvaluateAsync(ToolCallPolicyContext context)
    {
        var toolName = context.Tool.Name;

        if (_denyList.Contains(toolName))
            return Task.FromResult(new PolicyCheckResult
            {
                Outcome = PolicyOutcome.Deny,
                Reason = $"Tool '{toolName}' is on the deny list."
            });

        if (_confirmList.Contains(toolName))
            return Task.FromResult(new PolicyCheckResult
            {
                Outcome = PolicyOutcome.Confirm,
                Reason = $"Tool '{toolName}' requires confirmation."
            });

        return Task.FromResult(new PolicyCheckResult
        {
            Outcome = PolicyOutcome.Allow,
            Reason = "Tool allowed by default."
        });
    }
}
