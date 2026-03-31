// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

namespace GoogleAdk.Core.Abstractions.Auth;

/// <summary>
/// Represents the secret token value for HTTP authentication.
/// </summary>
public class HttpCredentials
{
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? Token { get; set; }
}

/// <summary>
/// The credentials and metadata for HTTP authentication.
/// </summary>
public class HttpAuth
{
    /// <summary>
    /// The name of the HTTP Authorization scheme (RFC7235).
    /// Examples: 'basic', 'bearer'
    /// </summary>
    public string Scheme { get; set; } = string.Empty;

    public HttpCredentials Credentials { get; set; } = new();
}

/// <summary>
/// Represents credential value and its metadata for an OAuth2 credential.
/// </summary>
public class OAuth2Auth
{
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? AuthUri { get; set; }
    public string? State { get; set; }
    public string? RedirectUri { get; set; }
    public string? AuthResponseUri { get; set; }
    public string? AuthCode { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public long? ExpiresAt { get; set; }
    public long? ExpiresIn { get; set; }
}

/// <summary>
/// Represents Google Service Account configuration.
/// </summary>
public class ServiceAccountCredential
{
    public string Type { get; set; } = "service_account";
    public string ProjectId { get; set; } = string.Empty;
    public string PrivateKeyId { get; set; } = string.Empty;
    public string PrivateKey { get; set; } = string.Empty;
    public string ClientEmail { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string AuthUri { get; set; } = string.Empty;
    public string TokenUri { get; set; } = string.Empty;
    public string AuthProviderX509CertUrl { get; set; } = string.Empty;
    public string ClientX509CertUrl { get; set; } = string.Empty;
    public string UniverseDomain { get; set; } = string.Empty;
}

/// <summary>
/// Represents Google Service Account configuration.
/// </summary>
public class ServiceAccount
{
    public ServiceAccountCredential? ServiceAccountCredential { get; set; }
    public List<string>? Scopes { get; set; }
    public bool? UseDefaultCredential { get; set; }
}

/// <summary>
/// Represents the type of authentication credential.
/// </summary>
public enum AuthCredentialType
{
    ApiKey,
    Http,
    OAuth2,
    ServiceAccount
}

/// <summary>
/// Represents an authentication credential.
/// </summary>
public class AuthCredential
{
    public AuthCredentialType AuthType { get; set; }
    public string? ResourceRef { get; set; }
    public HttpAuth? HttpAuth { get; set; }
    public OAuth2Auth? OAuth2Auth { get; set; }
    public ServiceAccount? ServiceAccount { get; set; }
}

/// <summary>
/// Base interface for credential services.
/// </summary>
public interface IBaseCredentialService
{
    Task<AuthCredential?> LoadCredentialAsync(object authConfig, object toolContext);
    Task SaveCredentialAsync(object authConfig, object toolContext);
}
