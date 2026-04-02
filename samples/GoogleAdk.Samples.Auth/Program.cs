// ============================================================================
// Auth Sample — LLM + Authenticated Tool
// ============================================================================
//
// Demonstrates:
//   1. OAuth2 AuthConfig wired to a tool
//   2. LlmAgent invoking an authenticated tool
//   3. Auth request event + simulated OAuth completion
// ============================================================================

using GoogleAdk.Core;
using GoogleAdk.Core.Abstractions.Auth;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Abstractions.Sessions;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Runner;
using GoogleAdk.Samples.Auth;

AdkEnv.Load();

Console.WriteLine("=== Auth Sample (LLM) ===\n");

var authConfig = SampleAuthTools.CalendarAuthConfig;
var calendarTool = SampleAuthTools.CalendarNextEventTool;

var agent = new LlmAgent(new LlmAgentConfig
{
    Name = "auth",
    Model = "gemini-2.5-flash",
    Instruction = "Answer calendar questions by calling calendar_next_event.",
    Tools = [calendarTool]
});

var runner = new InMemoryRunner("auth-sample", agent);
var session = await runner.SessionService.CreateSessionAsync(new CreateSessionRequest
{
    AppName = "auth-sample",
    UserId = "user-1"
});

var userMessage = new Content
{
    Role = "user",
    Parts = [new Part { Text = "What's my next meeting in PST?" }]
};

Console.WriteLine("User: What's my next meeting in PST?\n");

var needsAuth = await RunOnceAsync(runner, session.Id, userMessage);
if (needsAuth)
{
    Console.WriteLine("\nSimulating OAuth completion...\n");
    var authState = new Dictionary<string, object?>
    {
        ["temp:" + authConfig.CredentialKey] = new AuthCredential
        {
            AuthType = AuthCredentialType.OAuth2,
            OAuth2Auth = new OAuth2Auth { AccessToken = "ya29.sample-token" }
        }
    };

    var followup = new Content
    {
        Role = "user",
        Parts = [new Part { Text = "Continue." }]
    };

    await RunOnceAsync(runner, session.Id, followup, authState);
}

Console.WriteLine("\n=== Auth Sample Complete ===");

static async Task<bool> RunOnceAsync(
    Runner runner,
    string sessionId,
    Content userMessage,
    Dictionary<string, object?>? stateDelta = null)
{
    var needsAuth = false;

    await foreach (var evt in runner.RunAsync("user-1", sessionId, userMessage, stateDelta))
    {
        if (evt.Actions.RequestedAuthConfigs.Count > 0)
        {
            needsAuth = true;
            foreach (var (_, authRequest) in evt.Actions.RequestedAuthConfigs)
            {
                if (authRequest is AuthConfig authConfig)
                {
                    var authUrl = authConfig.ExchangedAuthCredential?.OAuth2Auth?.AuthUri
                                  ?? authConfig.RawAuthCredential?.OAuth2Auth?.AuthUri
                                  ?? authConfig.AuthScheme.Flows?.AuthorizationCode?.AuthorizationUrl;

                    Console.WriteLine("Auth required:");
                    Console.WriteLine($"  Credential Key: {authConfig.CredentialKey}");
                    Console.WriteLine($"  Auth URL: {authUrl}");
                }
            }
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

    return needsAuth;
}
