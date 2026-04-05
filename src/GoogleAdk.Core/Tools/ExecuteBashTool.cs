using System.Diagnostics;
using System.Runtime.InteropServices;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Abstractions.Tools;
using GoogleAdk.Core.Agents;

namespace GoogleAdk.Core.Tools;

/// <summary>
/// Executes a shell command with a prefix allowlist.
/// </summary>
public sealed class ExecuteBashTool : BaseTool
{
    private readonly IReadOnlyList<string> _allowedPrefixes;

    public ExecuteBashTool(IEnumerable<string>? allowedPrefixes = null)
        : base("bash", "Executes a shell command.")
    {
        _allowedPrefixes = (allowedPrefixes ?? new[] { "*" }).ToList();
    }

    public override async Task<object?> RunAsync(Dictionary<string, object?> args, AgentContext context)
    {
        var command = args.GetValueOrDefault("command")?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(command))
        {
            return new Dictionary<string, object?> { ["error"] = "Command is required." };
        }

        var isAllowed = _allowedPrefixes.Contains("*") || 
                        _allowedPrefixes.Any(p => command.StartsWith(p, StringComparison.OrdinalIgnoreCase));

        if (!isAllowed)
        {
            return new Dictionary<string, object?>
            {
                ["error"] = $"Command not allowed by policy. Permitted prefixes are: {string.Join(", ", _allowedPrefixes)}"
            };
        }

        var functionCallId = context.FunctionCallId ?? string.Empty;
        if (!context.EventActions.RequestedToolConfirmations.TryGetValue(functionCallId, out var confirmation))
        {
            context.EventActions.RequestedToolConfirmations[functionCallId] = new ToolConfirmation
            {
                FunctionCallId = functionCallId,
            };

            return new Dictionary<string, object?>
            {
                ["error"] = "This tool call requires confirmation, please approve or reject."
            };
        }

        if (confirmation.Accepted != true)
        {
            return new Dictionary<string, object?> { ["error"] = "This tool call is rejected." };
        }

        var (fileName, arguments) = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? ("cmd.exe", $"/c {command}")
            : ("bash", $"-c \"{command.Replace("\"", "\\\"")}\"");

        var psi = new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Environment.CurrentDirectory
        };

        using var proc = Process.Start(psi);
        if (proc == null)
            return new Dictionary<string, object?> { ["error"] = "Failed to start process." };

        var outputTask = proc.StandardOutput.ReadToEndAsync();
        var errorTask = proc.StandardError.ReadToEndAsync();

        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
        var processExitTask = proc.WaitForExitAsync();

        var completedTask = await Task.WhenAny(processExitTask, timeoutTask);
        
        if (completedTask == timeoutTask)
        {
            try
            {
                proc.Kill();
            }
            catch (InvalidOperationException)
            {
                // Process already exited
            }
            return new Dictionary<string, object?> { ["error"] = "Command timed out after 30 seconds." };
        }

        var output = await outputTask;
        var error = await errorTask;

        return new Dictionary<string, object?>
        {
            ["stdout"] = string.IsNullOrEmpty(output) ? "<No stdout captured>" : output,
            ["stderr"] = string.IsNullOrEmpty(error) ? "<No stderr captured>" : error,
            ["returncode"] = proc.ExitCode
        };
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
                    ["command"] = new Dictionary<string, object?>
                    {
                        ["type"] = "string",
                        ["description"] = "The bash command to execute."
                    }
                },
                ["required"] = new[] { "command" }
            }
        };
    }
}
