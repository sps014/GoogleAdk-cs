// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Auth;

namespace GoogleAdk.Core.Auth.Exchanger;

/// <summary>
/// Exception for credential exchange errors.
/// </summary>
public class CredentialExchangeException : Exception
{
    public CredentialExchangeException(string message) : base(message) { }
    public CredentialExchangeException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Interface for credential exchangers.
/// Credential exchangers are responsible for exchanging credentials from one format or scheme to another.
/// </summary>
public interface IBaseCredentialExchanger
{
    /// <summary>
    /// Exchange credential if needed.
    /// </summary>
    /// <param name="authCredential">The credential to exchange.</param>
    /// <param name="authScheme">The authentication scheme (optional).</param>
    /// <returns>The exchanged credential.</returns>
    Task<AuthCredential> ExchangeAsync(AuthCredential authCredential, AuthScheme? authScheme = null);
}
