using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;

namespace GoogleAdk.Core.Agents.Processors;

/// <summary>
/// Request processor that enables context caching for LLM requests.
/// </summary>
public sealed class ContextCacheRequestProcessor : BaseLlmRequestProcessor
{
    public static readonly ContextCacheRequestProcessor Instance = new();

    public override async IAsyncEnumerable<Event> RunAsync(
        InvocationContext invocationContext,
        LlmRequest llmRequest)
    {
        if (invocationContext.Agent is not LlmAgent agent || agent.ContextCacheConfig == null)
            yield break;

        // Set cache config to request
        llmRequest.CacheConfig = agent.ContextCacheConfig;

        // Find latest cache metadata and previous token count from session events
        var (cacheMetadata, previousTokenCount) = FindCacheInfoFromEvents(
            invocationContext, agent.Name, invocationContext.InvocationId);

        if (cacheMetadata != null)
        {
            llmRequest.CacheMetadata = cacheMetadata;
        }

        if (previousTokenCount.HasValue)
        {
            llmRequest.CacheableContentsTokenCount = previousTokenCount.Value;
        }

        await Task.CompletedTask;
    }

    private (CacheMetadata? cacheMetadata, int? previousTokenCount) FindCacheInfoFromEvents(
        InvocationContext invocationContext,
        string agentName,
        string currentInvocationId)
    {
        if (invocationContext.Session?.Events == null)
            return (null, null);

        CacheMetadata? cacheMetadata = null;
        int? previousTokenCount = null;

        var events = invocationContext.Session.Events;
        for (int i = events.Count - 1; i >= 0; i--)
        {
            var evt = events[i];
            if (evt.Author != agentName)
                continue;

            if (cacheMetadata == null && evt.CacheMetadata != null)
            {
                if (!string.IsNullOrEmpty(evt.InvocationId) &&
                    evt.InvocationId != currentInvocationId &&
                    evt.CacheMetadata.CacheName != null)
                {
                    cacheMetadata = evt.CacheMetadata.Clone();
                    cacheMetadata.InvocationsUsed++;
                }
                else
                {
                    cacheMetadata = evt.CacheMetadata.Clone();
                }
            }

            if (previousTokenCount == null && evt.UsageMetadata?.PromptTokenCount != null)
            {
                previousTokenCount = evt.UsageMetadata.PromptTokenCount;
            }

            if (cacheMetadata != null && previousTokenCount != null)
            {
                break;
            }
        }

        return (cacheMetadata, previousTokenCount);
    }
}
