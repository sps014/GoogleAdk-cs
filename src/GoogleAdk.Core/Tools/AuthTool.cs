using System.Text.Json;
using GoogleAdk.Core.Abstractions.Auth;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Agents;

namespace GoogleAdk.Core.Tools;

/// <summary>
/// Tool that requests end-user credentials and returns a partial response to trigger auth flow.
/// </summary>
public sealed class AuthTool : BaseTool
{
    public AuthTool() : base(FunctionCallHandler.RequestEucFunctionCallName, "Request end-user credentials.", isLongRunning: true)
    {
    }

    public override FunctionDeclaration? GetDeclaration()
    {
        return new FunctionDeclaration
        {
            Name = Name,
            Description = Description,
            Parameters = new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?>
                {
                    ["function_call_id"] = new Dictionary<string, object?> { ["type"] = "string" },
                    ["auth_config"] = new Dictionary<string, object?> { ["type"] = "object" }
                }
            }
        };
    }

    public override Task<object?> RunAsync(Dictionary<string, object?> args, AgentContext context)
    {
        var authConfig = TryParseAuthConfig(args);
        if (authConfig == null)
            throw new InvalidOperationException("auth_config is required for AuthTool.");

        context.RequestCredential(authConfig);
        return Task.FromResult<object?>(new Dictionary<string, object?>
        {
            ["partial"] = "auth_required"
        });
    }

    private static AuthConfig? TryParseAuthConfig(Dictionary<string, object?> args)
    {
        if (!args.TryGetValue("auth_config", out var value) || value == null)
            return null;

        if (value is AuthConfig config)
            return config;

        try
        {
            if (value is JsonElement element)
                return element.Deserialize<AuthConfig>();

            if (value is Dictionary<string, object?> dict)
            {
                var json = JsonSerializer.Serialize(dict);
                return JsonSerializer.Deserialize<AuthConfig>(json);
            }
        }
        catch
        {
            return null;
        }

        return null;
    }
}
