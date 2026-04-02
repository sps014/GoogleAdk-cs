using GoogleAdk.Core.Abstractions.Auth;
using GoogleAdk.Core.Abstractions.Tools;
using GoogleAdk.Core.Agents;

namespace GoogleAdk.Samples.Tools;

public static partial class SampleToolFunctions
{
    public static AuthConfig WeatherAuthConfig { get; } = new AuthConfig
    {
        CredentialKey = "weather_api_key",
        AuthScheme = new AuthScheme
        {
            Type = AuthSchemeType.ApiKey,
            Name = "X-API-Key",
            In = "header",
            Description = "Weather API key"
        },
        RawAuthCredential = new AuthCredential
        {
            AuthType = AuthCredentialType.ApiKey,
            HttpAuth = new HttpAuth
            {
                Scheme = "apiKey",
                Credentials = new HttpCredentials { Token = "sk-weather-sample" }
            }
        }
    };

    /// <summary>Returns the current temperature for a city (API key required).</summary>
    /// <param name="city">City name like Seattle.</param>
    /// <param name="ctx">Agent context used for auth state.</param>
    [FunctionTool(Name = "weather_lookup")]
    public static object? WeatherLookup(string city, AgentContext ctx)
    {
        var credential = ctx.GetAuthResponse(WeatherAuthConfig);
        if (credential == null)
        {
            ctx.RequestCredential(WeatherAuthConfig);
            return new Dictionary<string, object?> { ["status"] = "auth_required" };
        }

        var tokenSuffix = credential.HttpAuth?.Credentials?.Token?.Length >= 4
            ? credential.HttpAuth.Credentials.Token[^4..]
            : "none";

        return new Dictionary<string, object?>
        {
            ["city"] = city,
            ["temp_f"] = 72,
            ["token_suffix"] = tokenSuffix
        };
    }
}
