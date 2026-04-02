// ============================================================================
// Tools Sample — Auth, Bash, Transfer, Grounding
// ============================================================================

using GoogleAdk.Core.Abstractions.Auth;
using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core;
using GoogleAdk.Core.Tools;

AdkEnv.Load();

Console.WriteLine("=== Tools Sample ===\n");

var authTool = new AuthenticatedFunctionTool(
    "auth_tool",
    "Requires credential",
    new AuthConfig { CredentialKey = "sample" },
    (_, _) => Task.FromResult<object?>("ok"),
    new Dictionary<string, object?> { ["type"] = "object" });

var bashTool = new ExecuteBashTool(["echo"]);
var transferTool = new TransferToAgentTool(["planner"]);
var choiceTool = new GetUserChoiceTool();

var groundingTools = new BaseTool[]
{
    new DiscoveryEngineSearchTool(),
    new EnterpriseWebSearchTool(),
    new GoogleMapsGroundingTool(),
    new UrlContextTool()
};

Console.WriteLine($"AuthTool: {authTool.Name}");
Console.WriteLine($"BashTool: {bashTool.Name}");
Console.WriteLine($"TransferTool: {transferTool.Name}");
Console.WriteLine($"ChoiceTool (long running): {choiceTool.IsLongRunning}");
Console.WriteLine($"Grounding tools: {groundingTools.Length}");

Console.WriteLine("\n=== Tools Sample Complete ===");
