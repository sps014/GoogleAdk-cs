// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

namespace GoogleAdk.Core.Abstractions.Auth;

/// <summary>
/// The auth config sent by a tool asking the client to collect auth credentials.
/// ADK and the client work together to fill in the response.
/// </summary>
public class AuthConfig
{
    /// <summary>
    /// The auth scheme used to collect credentials.
    /// </summary>
    public AuthScheme AuthScheme { get; set; } = new();

    /// <summary>
    /// The raw auth credential. Used in auth schemes that need credential exchange
    /// (e.g. OAuth2, OIDC). For other schemes it may be null.
    /// </summary>
    public AuthCredential? RawAuthCredential { get; set; }

    /// <summary>
    /// The exchanged auth credential. ADK and the client work together to fill this.
    /// For schemes that don't need exchange (e.g. API key), the client fills it directly.
    /// For OAuth2/OIDC, ADK generates the auth URI and state, then the client completes the flow.
    /// </summary>
    public AuthCredential? ExchangedAuthCredential { get; set; }

    /// <summary>
    /// A user-specified key used to load and save this credential in a credential service.
    /// </summary>
    public string CredentialKey { get; set; } = string.Empty;
}

/// <summary>
/// Arguments for the special long running function tool used to request end user credentials.
/// </summary>
public class AuthToolArguments
{
    public string FunctionCallId { get; set; } = string.Empty;
    public AuthConfig AuthConfig { get; set; } = new();
}
