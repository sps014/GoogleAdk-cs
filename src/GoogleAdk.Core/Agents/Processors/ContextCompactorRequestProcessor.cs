// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Context;

namespace GoogleAdk.Core.Agents.Processors;

/// <summary>
/// Evaluates a set of context compactors to optionally compact
/// the conversation history before generating an LLM request.
/// The first compactor that indicates it should compact performs the compaction.
/// </summary>
public class ContextCompactorRequestProcessor : BaseLlmRequestProcessor
{
    private readonly IReadOnlyList<IContextCompactor> _compactors;

    public ContextCompactorRequestProcessor(IReadOnlyList<IContextCompactor> compactors)
    {
        _compactors = compactors;
    }

    public override async IAsyncEnumerable<Event> RunAsync(
        InvocationContext invocationContext,
        LlmRequest llmRequest)
    {
        foreach (var compactor in _compactors)
        {
            if (await compactor.ShouldCompactAsync(invocationContext))
            {
                var oldEvents = new HashSet<Event>(invocationContext.Session.Events);
                await compactor.CompactAsync(invocationContext);

                // Yield any new events added by the compaction
                foreach (var evt in invocationContext.Session.Events)
                {
                    if (!oldEvents.Contains(evt))
                        yield return evt;
                }
                yield break; // Stop after one compactor has compacted
            }
        }
    }
}
