// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

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
        // TODO: Implement full auth URI generation with state parameter
        return _authConfig.RawAuthCredential;
    }
}
