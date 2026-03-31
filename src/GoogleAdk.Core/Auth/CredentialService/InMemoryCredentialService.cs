// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Auth;
using GoogleAdk.Core.Agents;

namespace GoogleAdk.Core.Auth.CredentialService;

/// <summary>
/// In-memory implementation of the credential service.
/// Stores credentials keyed by appName → userId → credentialKey.
/// </summary>
public class InMemoryCredentialService : IBaseCredentialService
{
    // appName → userId → credentialKey → credential
    private readonly Dictionary<string, Dictionary<string, Dictionary<string, AuthCredential>>> _credentials = new();

    public Task<AuthCredential?> LoadCredentialAsync(object authConfig, object toolContext)
    {
        if (authConfig is not AuthConfig config || toolContext is not AgentContext ctx)
            return Task.FromResult<AuthCredential?>(null);

        var bucket = GetBucket(ctx);
        bucket.TryGetValue(config.CredentialKey, out var credential);
        return Task.FromResult(credential);
    }

    public Task SaveCredentialAsync(object authConfig, object toolContext)
    {
        if (authConfig is not AuthConfig config || toolContext is not AgentContext ctx)
            return Task.CompletedTask;

        if (config.ExchangedAuthCredential != null)
        {
            var bucket = GetBucket(ctx);
            bucket[config.CredentialKey] = config.ExchangedAuthCredential;
        }

        return Task.CompletedTask;
    }

    private Dictionary<string, AuthCredential> GetBucket(AgentContext context)
    {
        var appName = context.AppName;
        var userId = context.UserId;

        if (!_credentials.ContainsKey(appName))
            _credentials[appName] = new();

        if (!_credentials[appName].ContainsKey(userId))
            _credentials[appName][userId] = new();

        return _credentials[appName][userId];
    }
}
