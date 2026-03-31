// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Events;
using System.Runtime.CompilerServices;

namespace GoogleAdk.Core.Agents;

/// <summary>
/// Configuration for a loop agent.
/// </summary>
public class LoopAgentConfig : BaseAgentConfig
{
    /// <summary>
    /// Maximum number of iterations. Defaults to int.MaxValue (run indefinitely).
    /// </summary>
    public int MaxIterations { get; set; } = int.MaxValue;
}

/// <summary>
/// A shell agent that runs its sub-agents in a loop.
/// Stops when a sub-agent generates an event with Escalate=true or max iterations reached.
/// </summary>
public class LoopAgent : BaseAgent
{
    public int MaxIterations { get; }

    public LoopAgent(LoopAgentConfig config) : base(config)
    {
        MaxIterations = config.MaxIterations;
    }

    protected override async IAsyncEnumerable<Event> RunAsyncImpl(
        InvocationContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        int iteration = 0;

        while (iteration < MaxIterations)
        {
            foreach (var subAgent in SubAgents)
            {
                bool shouldExit = false;
                await foreach (var evt in subAgent.RunAsync(context).WithCancellation(cancellationToken))
                {
                    yield return evt;
                    if (evt.Actions.Escalate == true)
                        shouldExit = true;
                }

                if (shouldExit)
                    yield break;
            }

            iteration++;
        }
    }
}
