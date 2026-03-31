// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

namespace GoogleAdk.Core.Agents;

/// <summary>
/// Runtime configuration for agent execution.
/// </summary>
public class RunConfig
{
    /// <summary>Streaming mode: None, SSE, or BIDI.</summary>
    public StreamingMode StreamingMode { get; set; } = StreamingMode.None;

    /// <summary>
    /// A limit on the total number of LLM calls for a given run.
    /// Values > 0 enforce the limit. Values ≤ 0 allow unbounded calls.
    /// </summary>
    public int MaxLlmCalls { get; set; } = 500;

    /// <summary>
    /// If true, the agent loop will suspend on ANY tool call, allowing
    /// the client to intercept and execute tools (Client-Side Tool Execution).
    /// </summary>
    public bool PauseOnToolCalls { get; set; }

    /// <summary>Whether to save input blobs as artifacts.</summary>
    public bool SaveInputBlobsAsArtifacts { get; set; }
}

public enum StreamingMode
{
    None,
    Sse,
    Bidi
}
