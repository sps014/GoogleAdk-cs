// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

// ============================================================================
// Auth System Sample — OAuth2 Flow with Credential Service
// ============================================================================
//
// Demonstrates:
//   1. AuthScheme configuration (OAuth2)
//   2. AuthConfig creation with credential key
//   3. AuthHandler for generating auth requests
//   4. InMemoryCredentialService for storing/loading credentials
//   5. CredentialExchangerRegistry for credential exchange
// ============================================================================

using GoogleAdk.Core.Abstractions.Auth;
using GoogleAdk.Core.Abstractions.Sessions;
using GoogleAdk.Core.Auth;
using GoogleAdk.Core.Auth.CredentialService;
using GoogleAdk.Core.Auth.Exchanger;

Console.WriteLine("=== Auth System Sample ===\n");

// ── 1. Configure OAuth2 Auth Scheme ────────────────────────────────────────

var oauthScheme = new AuthScheme
{
    Type = AuthSchemeType.OAuth2,
    Description = "OAuth2 authorization code flow for API access",
    Flows = new OAuth2Flows
    {
        AuthorizationCode = new OAuth2Flow
        {
            AuthorizationUrl = "https://accounts.example.com/o/oauth2/v2/auth",
            TokenUrl = "https://oauth2.example.com/token",
            Scopes = new Dictionary<string, string>
            {
                ["read"] = "Read access",
                ["write"] = "Write access"
            }
        }
    }
};

Console.WriteLine($"Auth Scheme Type: {oauthScheme.Type}");
Console.WriteLine($"Authorization URL: {oauthScheme.Flows?.AuthorizationCode?.AuthorizationUrl}");

// ── 2. Create Auth Config ──────────────────────────────────────────────────

var authConfig = new AuthConfig
{
    CredentialKey = "my_api_oauth",
    AuthScheme = oauthScheme,
    RawAuthCredential = new AuthCredential
    {
        AuthType = AuthCredentialType.OAuth2,
        OAuth2Auth = new OAuth2Auth
        {
            ClientId = "my-client-id",
            ClientSecret = "my-client-secret",
            RedirectUri = "http://localhost:8080/callback"
        }
    }
};

Console.WriteLine($"\nCredential Key: {authConfig.CredentialKey}");
Console.WriteLine($"Client ID: {authConfig.RawAuthCredential?.OAuth2Auth?.ClientId}");

// ── 3. Use AuthHandler ─────────────────────────────────────────────────────

var handler = new AuthHandler(authConfig);
var authRequest = handler.GenerateAuthRequest();

Console.WriteLine($"\nGenerated Auth Request:");
Console.WriteLine($"  Credential Key: {authRequest.CredentialKey}");
Console.WriteLine($"  Has Exchanged Credential: {authRequest.ExchangedAuthCredential != null}");

// Check auth response from state (empty state, no response yet)
var state = new State();
var authResponse = handler.GetAuthResponse(state);
Console.WriteLine($"  Auth Response from State: {(authResponse != null ? "present" : "none")}");

// ── 4. Credential Service ──────────────────────────────────────────────────

var credentialService = new InMemoryCredentialService();
Console.WriteLine("\nInMemoryCredentialService created.");
Console.WriteLine("  (stores credentials keyed by appName → userId → credentialKey)");

// ── 5. Credential Exchanger Registry ───────────────────────────────────────

var registry = new CredentialExchangerRegistry();
Console.WriteLine("\nCredentialExchangerRegistry created.");

var exchanger = registry.GetExchanger(AuthCredentialType.OAuth2);
Console.WriteLine($"  OAuth2 Exchanger registered: {exchanger != null}");

// ── 6. API Key Auth (simpler flow) ────────────────────────────────────────

var apiKeyScheme = new AuthScheme
{
    Type = AuthSchemeType.ApiKey,
    Name = "X-API-Key",
    In = "header",
    Description = "Simple API key in header"
};

var apiKeyConfig = new AuthConfig
{
    CredentialKey = "my_api_key",
    AuthScheme = apiKeyScheme,
    RawAuthCredential = new AuthCredential
    {
        AuthType = AuthCredentialType.ApiKey,
        HttpAuth = new HttpAuth
        {
            Scheme = "apiKey",
            Credentials = new HttpCredentials { Token = "sk-sample-key-12345" }
        }
    }
};

var apiKeyHandler = new AuthHandler(apiKeyConfig);
var apiKeyRequest = apiKeyHandler.GenerateAuthRequest();
Console.WriteLine($"\nAPI Key Auth Request:");
Console.WriteLine($"  Scheme Type: {apiKeyRequest.AuthScheme.Type}");
Console.WriteLine($"  Header Name: {apiKeyRequest.AuthScheme.Name}");
Console.WriteLine($"  Location: {apiKeyRequest.AuthScheme.In}");

Console.WriteLine("\n=== Auth Sample Complete ===");
