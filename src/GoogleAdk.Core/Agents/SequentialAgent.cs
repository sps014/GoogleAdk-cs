// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Events;
using System.Runtime.CompilerServices;

namespace GoogleAdk.Core.Agents;

/// <summary>
/// A shell agent that runs its sub-agents in sequential order.
/// </summary>
public class SequentialAgent : BaseAgent
{
    public SequentialAgent(BaseAgentConfig config) : base(config) { }

    protected override async IAsyncEnumerable<Event> RunAsyncImpl(
        InvocationContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var subAgent in SubAgents)
        {
            await foreach (var evt in subAgent.RunAsync(context).WithCancellation(cancellationToken))
            {
                yield return evt;
            }
        }
    }
}
