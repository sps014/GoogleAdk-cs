// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Tools;

namespace GoogleAdk.Core.Abstractions.Events;

/// <summary>
/// Represents the actions attached to an event.
/// </summary>
public class EventActions
{
    /// <summary>
    /// If true, it won't call model to summarize function response.
    /// Only used for function_response event.
    /// </summary>
    public bool? SkipSummarization { get; set; }

    /// <summary>
    /// Indicates that the event is updating the state with the given delta.
    /// </summary>
    public Dictionary<string, object?> StateDelta { get; set; } = new();

    /// <summary>
    /// Indicates that the event is updating an artifact. Key is the filename, value is the version.
    /// </summary>
    public Dictionary<string, int> ArtifactDelta { get; set; } = new();

    /// <summary>
    /// If set, the event transfers to the specified agent.
    /// </summary>
    public string? TransferToAgent { get; set; }

    /// <summary>
    /// The agent is escalating to a higher level agent.
    /// </summary>
    public bool? Escalate { get; set; }

    /// <summary>
    /// Authentication configurations requested by tool responses.
    /// Keys: The function call id. Values: The requested auth config.
    /// </summary>
    public Dictionary<string, object?> RequestedAuthConfigs { get; set; } = new();

    /// <summary>
    /// A dict of tool confirmation requested by this event, keyed by the function call id.
    /// </summary>
    public Dictionary<string, ToolConfirmation> RequestedToolConfirmations { get; set; } = new();

    /// <summary>
    /// Custom metadata for the event actions.
    /// </summary>
    public Dictionary<string, object?>? CustomMetadata { get; set; }

    /// <summary>
    /// Creates a new EventActions with default values.
    /// </summary>
    public static EventActions Create(Action<EventActions>? configure = null)
    {
        var actions = new EventActions();
        configure?.Invoke(actions);
        return actions;
    }

    /// <summary>
    /// Merges a list of EventActions into a single EventActions.
    /// Dictionaries are merged by adding all properties. For scalar properties, last one wins.
    /// </summary>
    public static EventActions Merge(IEnumerable<EventActions?> sources, EventActions? target = null)
    {
        var result = new EventActions();

        if (target != null)
        {
            CopyScalars(target, result);
            MergeDictionaries(target, result);
        }

        foreach (var source in sources)
        {
            if (source == null) continue;

            foreach (var kv in source.StateDelta)
                result.StateDelta[kv.Key] = kv.Value;

            foreach (var kv in source.ArtifactDelta)
                result.ArtifactDelta[kv.Key] = kv.Value;

            foreach (var kv in source.RequestedAuthConfigs)
                result.RequestedAuthConfigs[kv.Key] = kv.Value;

            foreach (var kv in source.RequestedToolConfirmations)
                result.RequestedToolConfirmations[kv.Key] = kv.Value;

            CopyScalars(source, result);
        }

        return result;
    }

    private static void CopyScalars(EventActions source, EventActions target)
    {
        if (source.SkipSummarization.HasValue)
            target.SkipSummarization = source.SkipSummarization;
        if (source.TransferToAgent != null)
            target.TransferToAgent = source.TransferToAgent;
        if (source.Escalate.HasValue)
            target.Escalate = source.Escalate;
    }

    private static void MergeDictionaries(EventActions source, EventActions target)
    {
        foreach (var kv in source.StateDelta)
            target.StateDelta[kv.Key] = kv.Value;
        foreach (var kv in source.ArtifactDelta)
            target.ArtifactDelta[kv.Key] = kv.Value;
        foreach (var kv in source.RequestedAuthConfigs)
            target.RequestedAuthConfigs[kv.Key] = kv.Value;
        foreach (var kv in source.RequestedToolConfirmations)
            target.RequestedToolConfirmations[kv.Key] = kv.Value;
    }
}
