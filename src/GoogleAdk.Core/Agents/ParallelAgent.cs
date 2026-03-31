// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Events;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace GoogleAdk.Core.Agents;

/// <summary>
/// A shell agent that runs its sub-agents in parallel with isolated branches.
/// Useful for running different algorithms simultaneously or generating multiple responses.
/// </summary>
public class ParallelAgent : BaseAgent
{
    public ParallelAgent(BaseAgentConfig config) : base(config) { }

    protected override async IAsyncEnumerable<Event> RunAsyncImpl(
        InvocationContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var channel = Channel.CreateUnbounded<Event>();
        var tasks = SubAgents.Select(subAgent =>
            RunSubAgentAsync(subAgent, context, channel.Writer, cancellationToken)
        ).ToArray();

        // When all sub-agents complete, close the channel
        _ = Task.WhenAll(tasks).ContinueWith(_ => channel.Writer.Complete(), cancellationToken);

        await foreach (var evt in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return evt;
        }

        // Propagate any exceptions
        await Task.WhenAll(tasks);
    }

    private async Task RunSubAgentAsync(
        BaseAgent subAgent,
        InvocationContext originalContext,
        ChannelWriter<Event> writer,
        CancellationToken cancellationToken)
    {
        var branchCtx = CreateBranchContext(subAgent, originalContext);
        await foreach (var evt in subAgent.RunAsync(branchCtx).WithCancellation(cancellationToken))
        {
            await writer.WriteAsync(evt, cancellationToken);
        }
    }

    private InvocationContext CreateBranchContext(BaseAgent subAgent, InvocationContext originalContext)
    {
        var ctx = new InvocationContext(originalContext);
        var branchSuffix = $"{Name}.{subAgent.Name}";
        ctx.Branch = string.IsNullOrEmpty(ctx.Branch)
            ? branchSuffix
            : $"{ctx.Branch}.{branchSuffix}";
        return ctx;
    }
}
