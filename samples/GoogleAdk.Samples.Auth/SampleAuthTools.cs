using GoogleAdk.Core.Abstractions.Auth;
using GoogleAdk.Core.Abstractions.Tools;
using GoogleAdk.Core.Agents;

namespace GoogleAdk.Samples.Auth;

public static partial class SampleAuthTools
{
    public static AuthConfig CalendarAuthConfig { get; } = new AuthConfig
    {
        CredentialKey = "calendar_oauth",
        AuthScheme = new AuthScheme
        {
            Type = AuthSchemeType.OAuth2,
            Description = "OAuth2 authorization code flow for calendar access",
            Flows = new OAuth2Flows
            {
                AuthorizationCode = new OAuth2Flow
                {
                    AuthorizationUrl = "https://accounts.example.com/o/oauth2/v2/auth",
                    TokenUrl = "https://oauth2.example.com/token",
                    Scopes = new Dictionary<string, string>
                    {
                        ["calendar.read"] = "Read calendar events"
                    }
                }
            }
        },
        RawAuthCredential = new AuthCredential
        {
            AuthType = AuthCredentialType.OAuth2,
            OAuth2Auth = new OAuth2Auth
            {
                ClientId = "client-id",
                ClientSecret = "client-secret",
                RedirectUri = "http://localhost:8080/callback"
            }
        }
    };

    /// <summary>Returns the user's next calendar event (OAuth required).</summary>
    /// <param name="timezone">IANA timezone like America/Los_Angeles.</param>
    /// <param name="ctx">Agent context used for auth state.</param>
    [FunctionTool(Name = "calendar_next_event")]
    public static object? CalendarNextEvent(string timezone, AgentContext ctx)
    {
        var credential = ctx.GetAuthResponse(CalendarAuthConfig);
        if (credential == null)
        {
            ctx.RequestCredential(CalendarAuthConfig);
            return new Dictionary<string, object?> { ["status"] = "auth_required" };
        }

        var tokenSuffix = credential.OAuth2Auth?.AccessToken?.Length >= 4
            ? credential.OAuth2Auth.AccessToken[^4..]
            : "none";

        return new Dictionary<string, object?>
        {
            ["title"] = "Project Sync",
            ["start"] = "2026-04-02T09:30:00-07:00",
            ["location"] = "Meet",
            ["timezone"] = timezone,
            ["token_suffix"] = tokenSuffix
        };
    }
}
