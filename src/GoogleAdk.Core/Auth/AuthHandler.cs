using GoogleAdk.Core.Abstractions.Auth;
using GoogleAdk.Core.Abstractions.Sessions;

namespace GoogleAdk.Core.Auth;

/// <summary>
/// Handles the auth flow in Agent Development Kit to orchestrate the credential
/// request and response flow (e.g. OAuth flow).
/// </summary>
public class AuthHandler
{
    private readonly AuthConfig _authConfig;

    public AuthHandler(AuthConfig authConfig)
    {
        _authConfig = authConfig;
    }

    /// <summary>
    /// Gets the auth response from session state.
    /// </summary>
    public AuthCredential? GetAuthResponse(State state)
    {
        var credentialKey = "temp:" + _authConfig.CredentialKey;
        return state.Get<AuthCredential>(credentialKey);
    }

    /// <summary>
    /// Generates the auth request. For OAuth2/OIDC, generates the auth URI if needed.
    /// </summary>
    public AuthConfig GenerateAuthRequest()
    {
        var authSchemeType = _authConfig.AuthScheme.Type;

        if (authSchemeType != AuthSchemeType.OAuth2 && authSchemeType != AuthSchemeType.OpenIdConnect)
            return _authConfig;

        if (_authConfig.ExchangedAuthCredential?.OAuth2Auth?.AuthUri != null)
            return _authConfig;

        if (_authConfig.RawAuthCredential == null)
            throw new InvalidOperationException($"Auth Scheme {authSchemeType} requires authCredential.");

        if (_authConfig.RawAuthCredential.OAuth2Auth == null)
            throw new InvalidOperationException($"Auth Scheme {authSchemeType} requires oauth2 in authCredential.");

        if (_authConfig.RawAuthCredential.OAuth2Auth.AuthUri != null)
        {
            return new AuthConfig
            {
                CredentialKey = _authConfig.CredentialKey,
                AuthScheme = _authConfig.AuthScheme,
                RawAuthCredential = _authConfig.RawAuthCredential,
                ExchangedAuthCredential = _authConfig.RawAuthCredential,
            };
        }

        if (string.IsNullOrEmpty(_authConfig.RawAuthCredential.OAuth2Auth.ClientId) ||
            string.IsNullOrEmpty(_authConfig.RawAuthCredential.OAuth2Auth.ClientSecret))
        {
            throw new InvalidOperationException(
                $"Auth Scheme {authSchemeType} requires both clientId and clientSecret in authCredential.oauth2.");
        }

        return new AuthConfig
        {
            CredentialKey = _authConfig.CredentialKey,
            AuthScheme = _authConfig.AuthScheme,
            RawAuthCredential = _authConfig.RawAuthCredential,
            ExchangedAuthCredential = GenerateAuthUri(),
        };
    }

    /// <summary>
    /// Generates an AuthCredential containing the auth URI for user sign-in.
    /// </summary>
    public AuthCredential? GenerateAuthUri()
    {
        if (_authConfig.RawAuthCredential?.OAuth2Auth == null)
            return _authConfig.RawAuthCredential;

        var auth2Auth = _authConfig.RawAuthCredential.OAuth2Auth;
        var authUri = auth2Auth.AuthUri;

        if (string.IsNullOrEmpty(authUri) && !string.IsNullOrEmpty(auth2Auth.ClientId))
        {
            var state = Guid.NewGuid().ToString("N");
            
            var baseAuthUri = "https://accounts.google.com/o/oauth2/v2/auth";
            var scopes = Array.Empty<string>();

            if (_authConfig.AuthScheme.Type == AuthSchemeType.OAuth2 && _authConfig.AuthScheme.Flows?.AuthorizationCode != null)
            {
                var authCodeFlow = _authConfig.AuthScheme.Flows.AuthorizationCode;
                if (!string.IsNullOrEmpty(authCodeFlow.AuthorizationUrl))
                    baseAuthUri = authCodeFlow.AuthorizationUrl;
                
                if (authCodeFlow.Scopes != null)
                    scopes = authCodeFlow.Scopes.Keys.ToArray();
            }
            else if (_authConfig.AuthScheme.Type == AuthSchemeType.OpenIdConnect && _authConfig.AuthScheme.OpenIdConnect != null)
            {
                if (!string.IsNullOrEmpty(_authConfig.AuthScheme.OpenIdConnect.AuthorizationEndpoint))
                    baseAuthUri = _authConfig.AuthScheme.OpenIdConnect.AuthorizationEndpoint;
                
                if (_authConfig.AuthScheme.OpenIdConnect.Scopes != null)
                    scopes = _authConfig.AuthScheme.OpenIdConnect.Scopes.ToArray();
            }

            var queryParams = new List<string>
            {
                $"client_id={Uri.EscapeDataString(auth2Auth.ClientId)}",
                $"redirect_uri={Uri.EscapeDataString(auth2Auth.RedirectUri ?? "")}",
                $"response_type=code",
                $"state={Uri.EscapeDataString(state)}",
            };

            if (scopes.Length > 0)
            {
                queryParams.Add($"scope={Uri.EscapeDataString(string.Join(" ", scopes))}");
            }

            authUri = $"{baseAuthUri}?{string.Join("&", queryParams)}";
        }

        return new AuthCredential
        {
            OAuth2Auth = new OAuth2Auth
            {
                ClientId = auth2Auth.ClientId,
                ClientSecret = auth2Auth.ClientSecret,
                RedirectUri = auth2Auth.RedirectUri,
                AuthUri = authUri
            }
        };
    }
}
