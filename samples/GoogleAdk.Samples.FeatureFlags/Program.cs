// ============================================================================
// Feature Flags + App Container Sample
// ============================================================================

using GoogleAdk.Core;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Abstractions.Sessions;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Apps;
using GoogleAdk.Core.Features;
using GoogleAdk.Core.Plugins;
using GoogleAdk.Core.Context;
using GoogleAdk.Models.Gemini;
using GoogleAdk.Samples.FeatureFlags;

AdkEnv.Load();

Console.WriteLine("=== Feature Flags + App Container Sample ===\n");

using var _ = AdkFeatures.TemporaryOverride(FeatureName.LiveBidiStreaming, true);
Console.WriteLine($"LiveBidi enabled: {AdkFeatures.IsFeatureEnabled(FeatureName.LiveBidiStreaming)}");

var agent = new LlmAgent(new LlmAgentConfig
{
    Name = "app-agent",
    Model = "gemini-2.5-flash",
    Instruction = "Call render_widget at least once and then respond briefly.",
    ContextCacheConfig = new ContextCacheConfig(),
    Tools =
    [
        SampleFeatureTools.RenderWidgetTool
    ]
});

var app = new AdkApp("feature-sample", agent)
{
    Plugins = [new GlobalInstructionPlugin("Always be concise.")]
};

var runner = new GoogleAdk.Core.Runner.Runner(new GoogleAdk.Core.Runner.RunnerConfig
{
    AppName = "feature-sample",
    App = app,
    SessionService = new GoogleAdk.Core.Sessions.InMemorySessionService()
});

var session = await runner.SessionService.CreateSessionAsync(new CreateSessionRequest
{
    AppName = "feature-sample",
    UserId = "user-1"
});

var userMessage = new Content { Role = "user", Parts = [new Part { Text = "Hi" }] };

await foreach (var evt in runner.RunAsync("user-1", session.Id, userMessage))
    {
        if (evt.Actions.RenderUiWidgets.Count > 0)
            Console.WriteLine($"UI Widgets: {evt.Actions.RenderUiWidgets.Count}");
    }


Console.WriteLine("\n=== Feature Flags Sample Complete ===");


