// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Auth;
using GoogleAdk.Core.Abstractions.Sessions;
using GoogleAdk.Core.Auth;
using GoogleAdk.Core.Auth.CredentialService;
using GoogleAdk.Core.Auth.Exchanger;

namespace GoogleAdk.Core.Tests;

public class AuthTests
{
    [Fact]
    public void AuthHandler_GenerateAuthRequest_ApiKey_ReturnsConfig()
    {
        var config = new AuthConfig
        {
            CredentialKey = "my_key",
            AuthScheme = new AuthScheme { Type = AuthSchemeType.ApiKey },
            RawAuthCredential = new AuthCredential
            {
                AuthType = AuthCredentialType.ApiKey,
                HttpAuth = new HttpAuth { Scheme = "apiKey", Credentials = new HttpCredentials { Token = "abc" } }
            }
        };

        var handler = new AuthHandler(config);
        var result = handler.GenerateAuthRequest();

        Assert.Equal("my_key", result.CredentialKey);
        Assert.Equal(AuthSchemeType.ApiKey, result.AuthScheme.Type);
    }

    [Fact]
    public void AuthHandler_GenerateAuthRequest_OAuth2_WithAuthUri()
    {
        var config = new AuthConfig
        {
            CredentialKey = "oauth_key",
            AuthScheme = new AuthScheme { Type = AuthSchemeType.OAuth2 },
            RawAuthCredential = new AuthCredential
            {
                AuthType = AuthCredentialType.OAuth2,
                OAuth2Auth = new OAuth2Auth
                {
                    ClientId = "client-id",
                    ClientSecret = "client-secret",
                    AuthUri = "https://auth.example.com/authorize"
                }
            }
        };

        var handler = new AuthHandler(config);
        var result = handler.GenerateAuthRequest();

        Assert.NotNull(result.ExchangedAuthCredential);
    }

    [Fact]
    public void AuthHandler_GenerateAuthRequest_OAuth2_WithoutCredentials_Throws()
    {
        var config = new AuthConfig
        {
            CredentialKey = "oauth_key",
            AuthScheme = new AuthScheme { Type = AuthSchemeType.OAuth2 },
            RawAuthCredential = null
        };

        var handler = new AuthHandler(config);
        Assert.Throws<InvalidOperationException>(() => handler.GenerateAuthRequest());
    }

    [Fact]
    public void AuthHandler_GenerateAuthRequest_OAuth2_WithoutOAuth2_Throws()
    {
        var config = new AuthConfig
        {
            CredentialKey = "oauth_key",
            AuthScheme = new AuthScheme { Type = AuthSchemeType.OAuth2 },
            RawAuthCredential = new AuthCredential { AuthType = AuthCredentialType.OAuth2 }
        };

        var handler = new AuthHandler(config);
        Assert.Throws<InvalidOperationException>(() => handler.GenerateAuthRequest());
    }

    [Fact]
    public void AuthHandler_GetAuthResponse_ReturnsNull_WhenNotInState()
    {
        var config = new AuthConfig { CredentialKey = "test_key", AuthScheme = new AuthScheme() };
        var handler = new AuthHandler(config);
        var state = new State();

        var result = handler.GetAuthResponse(state);
        Assert.Null(result);
    }

    [Fact]
    public async Task InMemoryCredentialService_SaveAndLoad()
    {
        var service = new InMemoryCredentialService();
        var invCtx = new Agents.InvocationContext
        {
            Session = Session.Create("s1", "app", "user"),
            Agent = new TestAgent("root"),
        };
        var agentCtx = new Agents.AgentContext(invCtx);

        var authConfig = new AuthConfig
        {
            CredentialKey = "cred1",
            AuthScheme = new AuthScheme { Type = AuthSchemeType.ApiKey },
            ExchangedAuthCredential = new AuthCredential
            {
                AuthType = AuthCredentialType.ApiKey,
                HttpAuth = new HttpAuth { Credentials = new HttpCredentials { Token = "token123" } }
            }
        };

        await service.SaveCredentialAsync(authConfig, agentCtx);
        var loaded = await service.LoadCredentialAsync(authConfig, agentCtx);

        Assert.NotNull(loaded);
        Assert.Equal("token123", loaded!.HttpAuth?.Credentials.Token);
    }

    [Fact]
    public async Task InMemoryCredentialService_LoadReturnsNull_WhenNotSaved()
    {
        var service = new InMemoryCredentialService();
        var invCtx = new Agents.InvocationContext
        {
            Session = Session.Create("s1", "app", "user"),
            Agent = new TestAgent("root"),
        };
        var agentCtx = new Agents.AgentContext(invCtx);

        var authConfig = new AuthConfig { CredentialKey = "missing", AuthScheme = new AuthScheme() };
        var loaded = await service.LoadCredentialAsync(authConfig, agentCtx);

        Assert.Null(loaded);
    }

    [Fact]
    public void CredentialExchangerRegistry_RegisterAndGet()
    {
        var registry = new CredentialExchangerRegistry();
        var mockExchanger = new MockCredentialExchanger();

        registry.Register(AuthCredentialType.OAuth2, mockExchanger);
        var retrieved = registry.GetExchanger(AuthCredentialType.OAuth2);

        Assert.Same(mockExchanger, retrieved);
    }

    [Fact]
    public void CredentialExchangerRegistry_GetReturnsNull_WhenNotRegistered()
    {
        var registry = new CredentialExchangerRegistry();
        var retrieved = registry.GetExchanger(AuthCredentialType.ServiceAccount);

        Assert.Null(retrieved);
    }

    [Fact]
    public void AuthScheme_OAuth2Flows_AreNullable()
    {
        var scheme = new AuthScheme { Type = AuthSchemeType.OAuth2 };
        Assert.Null(scheme.Flows);

        scheme.Flows = new OAuth2Flows
        {
            AuthorizationCode = new OAuth2Flow
            {
                AuthorizationUrl = "https://example.com/auth",
                TokenUrl = "https://example.com/token",
                Scopes = new Dictionary<string, string> { ["read"] = "Read" }
            }
        };

        Assert.NotNull(scheme.Flows.AuthorizationCode);
        Assert.Equal("https://example.com/auth", scheme.Flows.AuthorizationCode!.AuthorizationUrl);
    }

    private class MockCredentialExchanger : IBaseCredentialExchanger
    {
        public Task<AuthCredential> ExchangeAsync(AuthCredential authCredential, AuthScheme? authScheme = null)
        {
            return Task.FromResult(authCredential);
        }
    }
}
