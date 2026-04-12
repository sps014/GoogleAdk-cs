using System.Runtime.CompilerServices;
using GoogleAdk.Core.Abstractions.Events;

namespace GoogleAdk.Core.Agents.Processors;

/// <summary>
/// Injects agent identity into the system instructions:
/// "You are an agent. Your internal name is {name}."
/// </summary>
public class IdentityLlmRequestProcessor : BaseLlmRequestProcessor
{
    public static readonly IdentityLlmRequestProcessor Instance = new();

    public override async IAsyncEnumerable<Event> RunAsync(
        InvocationContext invocationContext,
        LlmRequest llmRequest)
    {
        await Task.CompletedTask;

        if (invocationContext.Agent is LlmAgent llmAgent && llmAgent.DisableIdentity)
            yield break;

        var agent = invocationContext.Agent;
        var instructions = new List<string>
        {
            $"You are an agent. Your internal name is \"{agent.Name}\"."
        };

        if (!string.IsNullOrEmpty(agent.Description))
            instructions.Add($"The description about you is \"{agent.Description}\"");

        llmRequest.AppendInstructions(instructions);
        yield break;
    }
}
