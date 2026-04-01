// Copyright 2026 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Agents;

namespace GoogleAdk.Core.A2a;

internal sealed class A2aRemoteAgentRunProcessor
{
    private sealed class ArtifactAggregation
    {
        public string AggregatedText { get; set; } = string.Empty;
        public string AggregatedThoughts { get; set; } = string.Empty;
        public List<Part> Parts { get; } = new();
        public CitationMetadata? Citations { get; set; }
        public GroundingMetadata? Grounding { get; set; }
        public Dictionary<string, object?>? CustomMeta { get; set; }
        public UsageMetadata? Usage { get; set; }
    }

    private readonly Dictionary<string, ArtifactAggregation> _aggregations = new();
    private readonly List<string> _aggregationOrder = new();
    private readonly MessageSendParams? _request;

    public A2aRemoteAgentRunProcessor(MessageSendParams? request)
    {
        _request = request;
    }

    public List<Event> AggregatePartial(
        InvocationContext context,
        IA2aEvent a2aEvent,
        Event adkEvent)
    {
        var metadata = A2aEventHelpers.GetEventMetadata(a2aEvent);
        if (metadata.TryGetValue(A2aMetadataKeys.Partial, out var partial) && partial is bool b && b)
            return new List<Event> { adkEvent };

        if (a2aEvent is TaskStatusUpdateEvent status && status.Final)
        {
            var events = new List<Event>();
            foreach (var id in _aggregationOrder.ToList())
            {
                if (_aggregations.TryGetValue(id, out var agg))
                    events.Add(BuildNonPartialAggregation(context, agg));
            }
            _aggregations.Clear();
            _aggregationOrder.Clear();
            events.Add(adkEvent);
            return events;
        }

        if (a2aEvent is Task)
        {
            _aggregations.Clear();
            _aggregationOrder.Clear();
            return new List<Event> { adkEvent };
        }

        if (a2aEvent is not TaskArtifactUpdateEvent artifactUpdate)
            return new List<Event> { adkEvent };

        var artifactId = artifactUpdate.Artifact.ArtifactId;

        if (artifactUpdate.Append != true)
        {
            RemoveAggregation(artifactId);
            if (artifactUpdate.LastChunk == true)
            {
                adkEvent.Partial = false;
                return new List<Event> { adkEvent };
            }
        }

        if (!_aggregations.TryGetValue(artifactId, out var aggregation))
        {
            aggregation = new ArtifactAggregation();
            _aggregations[artifactId] = aggregation;
            _aggregationOrder.Add(artifactId);
        }
        else
        {
            _aggregationOrder.Remove(artifactId);
            _aggregationOrder.Add(artifactId);
        }

        UpdateAggregation(aggregation, adkEvent);

        if (artifactUpdate.LastChunk != true)
            return new List<Event> { adkEvent };

        RemoveAggregation(artifactId);
        return new List<Event> { adkEvent, BuildNonPartialAggregation(context, aggregation) };
    }

    private void RemoveAggregation(string artifactId)
    {
        _aggregations.Remove(artifactId);
        _aggregationOrder.Remove(artifactId);
    }

    private static void UpdateAggregation(ArtifactAggregation agg, Event evt)
    {
        var parts = evt.Content?.Parts ?? new List<Part>();
        foreach (var part in parts)
        {
            if (!string.IsNullOrEmpty(part.Text))
            {
                if (part.Thought == true) agg.AggregatedThoughts += part.Text;
                else agg.AggregatedText += part.Text;
            }
            else
            {
                PromoteTextBlocksToParts(agg);
                agg.Parts.Add(part);
            }
        }

        if (evt.CitationMetadata != null)
        {
            agg.Citations ??= new CitationMetadata { Citations = new List<Dictionary<string, object?>>() };
            agg.Citations.Citations ??= new List<Dictionary<string, object?>>();
            if (evt.CitationMetadata.Citations != null)
                agg.Citations.Citations.AddRange(evt.CitationMetadata.Citations);
        }

        if (evt.CustomMetadata != null)
        {
            agg.CustomMeta ??= new Dictionary<string, object?>();
            foreach (var (k, v) in evt.CustomMetadata) agg.CustomMeta[k] = v;
        }

        if (evt.GroundingMetadata != null) agg.Grounding = evt.GroundingMetadata;
        if (evt.UsageMetadata != null) agg.Usage = evt.UsageMetadata;
    }

    private static Event BuildNonPartialAggregation(InvocationContext context, ArtifactAggregation agg)
    {
        PromoteTextBlocksToParts(agg);
        return Event.Create(e =>
        {
            e.Author = context.Agent.Name;
            e.InvocationId = context.InvocationId;
            e.Content = agg.Parts.Count > 0 ? new Content { Role = "model", Parts = agg.Parts.ToList() } : null;
            e.CustomMetadata = agg.CustomMeta;
            e.GroundingMetadata = agg.Grounding;
            e.CitationMetadata = agg.Citations;
            e.UsageMetadata = agg.Usage;
            e.TurnComplete = false;
            e.Partial = false;
        });
    }

    private static void PromoteTextBlocksToParts(ArtifactAggregation agg)
    {
        if (!string.IsNullOrEmpty(agg.AggregatedThoughts))
        {
            agg.Parts.Add(new Part { Thought = true, Text = agg.AggregatedThoughts });
            agg.AggregatedThoughts = string.Empty;
        }
        if (!string.IsNullOrEmpty(agg.AggregatedText))
        {
            agg.Parts.Add(new Part { Text = agg.AggregatedText });
            agg.AggregatedText = string.Empty;
        }
    }

    public void UpdateCustomMetadata(Event evt, IA2aEvent? response = null)
    {
        var toAdd = new Dictionary<string, object?>();
        if (_request != null && evt.TurnComplete == true)
            toAdd["request"] = _request;
        if (response != null)
        {
            toAdd["response"] = response;
            switch (response)
            {
                case Task task:
                    if (!string.IsNullOrEmpty(task.Id)) toAdd["task_id"] = task.Id;
                    if (!string.IsNullOrEmpty(task.ContextId)) toAdd["context_id"] = task.ContextId;
                    break;
                case TaskStatusUpdateEvent status:
                    toAdd["task_id"] = status.TaskId;
                    if (!string.IsNullOrEmpty(status.ContextId)) toAdd["context_id"] = status.ContextId;
                    break;
                case TaskArtifactUpdateEvent artifact:
                    toAdd["task_id"] = artifact.TaskId;
                    if (!string.IsNullOrEmpty(artifact.ContextId)) toAdd["context_id"] = artifact.ContextId;
                    break;
            }
        }

        if (toAdd.Count == 0) return;
        evt.CustomMetadata ??= new Dictionary<string, object?>();
        foreach (var (k, v) in toAdd)
        {
            if (v == null) continue;
            evt.CustomMetadata[$"a2a:{k}"] = v;
        }
    }
}

