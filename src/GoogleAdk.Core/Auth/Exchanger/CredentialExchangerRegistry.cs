// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Auth;

namespace GoogleAdk.Core.Auth.Exchanger;

/// <summary>
/// Registry for credential exchanger instances, keyed by credential type.
/// </summary>
public class CredentialExchangerRegistry
{
    private readonly Dictionary<AuthCredentialType, IBaseCredentialExchanger> _exchangers = new();

    /// <summary>
    /// Register an exchanger instance for a credential type.
    /// </summary>
    public void Register(AuthCredentialType credentialType, IBaseCredentialExchanger exchanger)
    {
        _exchangers[credentialType] = exchanger;
    }

    /// <summary>
    /// Get the exchanger instance for a credential type.
    /// </summary>
    public IBaseCredentialExchanger? GetExchanger(AuthCredentialType credentialType)
    {
        _exchangers.TryGetValue(credentialType, out var exchanger);
        return exchanger;
    }
}
