// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

namespace GoogleAdk.Core.Abstractions.Auth;

/// <summary>
/// Represents the OAuth2 flow (or grant type).
/// </summary>
public enum OAuthGrantType
{
    ClientCredentials,
    AuthorizationCode,
    Implicit,
    Password
}

/// <summary>
/// Represents the type of an authentication scheme.
/// </summary>
public enum AuthSchemeType
{
    ApiKey,
    Http,
    OAuth2,
    OpenIdConnect
}

/// <summary>
/// OAuth2 flow configuration.
/// </summary>
public class OAuth2Flow
{
    public string? AuthorizationUrl { get; set; }
    public string? TokenUrl { get; set; }
    public string? RefreshUrl { get; set; }
    public Dictionary<string, string>? Scopes { get; set; }
}

/// <summary>
/// OAuth2 flows configuration.
/// </summary>
public class OAuth2Flows
{
    public OAuth2Flow? ClientCredentials { get; set; }
    public OAuth2Flow? AuthorizationCode { get; set; }
    public OAuth2Flow? Implicit { get; set; }
    public OAuth2Flow? Password { get; set; }
}

/// <summary>
/// OpenID Connect configuration with explicit endpoints.
/// </summary>
public class OpenIdConnectConfig
{
    public string OpenIdConnectUrl { get; set; } = string.Empty;
    public string? AuthorizationEndpoint { get; set; }
    public string? TokenEndpoint { get; set; }
    public string? UserinfoEndpoint { get; set; }
    public string? RevocationEndpoint { get; set; }
    public List<string>? TokenEndpointAuthMethodsSupported { get; set; }
    public List<string>? GrantTypesSupported { get; set; }
    public List<string>? Scopes { get; set; }
}

/// <summary>
/// Represents an authentication scheme (OpenAPI 3.0 security scheme).
/// </summary>
public class AuthScheme
{
    /// <summary>
    /// The type of the security scheme: apiKey, http, oauth2, openIdConnect.
    /// </summary>
    public AuthSchemeType Type { get; set; }

    /// <summary>
    /// A short description for the security scheme.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The name of the header, query or cookie parameter to be used (for apiKey).
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// The location of the API key: query, header, or cookie (for apiKey).
    /// </summary>
    public string? In { get; set; }

    /// <summary>
    /// The name of the HTTP Authorization scheme (for http).
    /// </summary>
    public string? Scheme { get; set; }

    /// <summary>
    /// A hint for the format of the bearer token (for http bearer).
    /// </summary>
    public string? BearerFormat { get; set; }

    /// <summary>
    /// OAuth2 flows (for oauth2).
    /// </summary>
    public OAuth2Flows? Flows { get; set; }

    /// <summary>
    /// OpenID Connect configuration (for openIdConnect).
    /// </summary>
    public OpenIdConnectConfig? OpenIdConnect { get; set; }
}

/// <summary>
/// Helper methods for OAuth2 flows.
/// </summary>
public static class AuthSchemeExtensions
{
    /// <summary>
    /// Gets the OAuth grant type from the flows configuration.
    /// </summary>
    public static OAuthGrantType? GetOAuthGrantType(this OAuth2Flows flows)
    {
        if (flows.ClientCredentials != null) return OAuthGrantType.ClientCredentials;
        if (flows.AuthorizationCode != null) return OAuthGrantType.AuthorizationCode;
        if (flows.Implicit != null) return OAuthGrantType.Implicit;
        if (flows.Password != null) return OAuthGrantType.Password;
        return null;
    }
}
