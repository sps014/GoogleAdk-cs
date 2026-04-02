using System.CommandLine;
using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Abstractions.Sessions;
using GoogleAdk.Core.Runner;
using GoogleAdk.Core.Sessions;
using GoogleAdk.ApiServer.Server;

namespace GoogleAdk.ApiServer.Cli;

/// <summary>
/// The "run" command — interactive CLI agent conversation.
/// Usage: adk run [agents_dir] [--agent agent_name]
/// </summary>
public static class RunCommand
{
    public static Command Create()
    {
        var agentsDirArg = new Argument<string>("agents_dir")
        {
            Description = "Directory containing agent assemblies or projects.",
            DefaultValueFactory = _ => ".",
        };

        var agentOption = new Option<string?>("--agent", "-a")
        {
            Description = "Name of the agent to run. If omitted, uses the first available.",
        };

        var command = new Command("run", "Run an agent interactively in the terminal.");
        command.Arguments.Add(agentsDirArg);
        command.Options.Add(agentOption);

        command.SetAction(async parseResult =>
        {
            var agentsDir = parseResult.GetValue(agentsDirArg);
            var agentName = parseResult.GetValue(agentOption);
            await RunInteractive(agentsDir!, agentName);
        });

        return command;
    }

    private static async Task RunInteractive(string agentsDir, string? agentName)
    {
        var loader = new AgentLoader(agentsDir);
        var agents = loader.ListAgents();

        if (agents.Count == 0)
        {
            Console.Error.WriteLine("No agents found in: " + agentsDir);
            return;
        }

        var selectedAgent = agentName ?? agents[0];
        if (!agents.Contains(selectedAgent, StringComparer.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"Agent '{selectedAgent}' not found. Available: {string.Join(", ", agents)}");
            return;
        }

        var agent = loader.GetAgent(selectedAgent);
        var runner = new InMemoryRunner(selectedAgent, agent);

        Console.WriteLine($"ADK Interactive — Agent: {selectedAgent}");
        Console.WriteLine("Type 'quit' to exit.");
        Console.WriteLine();

        while (true)
        {
            Console.Write("You: ");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input) || input.Equals("quit", StringComparison.OrdinalIgnoreCase))
                break;

            var message = new Content
            {
                Role = "user",
                Parts = new List<Part> { new() { Text = input } }
            };

            await foreach (var evt in runner.RunEphemeralAsync("user-1", message))
            {
                var text = evt.Content?.Parts?.FirstOrDefault()?.Text;
                if (text != null && evt.Partial != true)
                {
                    Console.WriteLine($"[{evt.Author}]: {text}");
                    Console.WriteLine();
                }
            }
        }
    }
}
