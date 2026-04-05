using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Abstractions.Sessions;
using System.Collections.Generic;

namespace GoogleAdk.Core.Agents;

/// <summary>
/// Configuration for the tool thread pool executor.
/// </summary>
public class ToolThreadPoolConfig
{
    /// <summary>
    /// Maximum number of worker threads in the pool. Defaults to 4.
    /// </summary>
    public int MaxWorkers { get; set; } = 4;
}

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

    /// <summary>Speech configuration for the live agent.</summary>
    public SpeechConfig? SpeechConfig { get; set; }

    /// <summary>The output modalities. If not set, defaults to AUDIO in live contexts.</summary>
    public List<string>? ResponseModalities { get; set; }

    /// <summary>Whether to support CFC (Compositional Function Calling).</summary>
    public bool SupportCfc { get; set; }

    /// <summary>Output transcription for live agents with audio response.</summary>
    public AudioTranscriptionConfig? OutputAudioTranscription { get; set; }

    /// <summary>Input transcription for live agents with audio input from user.</summary>
    public AudioTranscriptionConfig? InputAudioTranscription { get; set; }

    /// <summary>Realtime input config for live agents with audio input from user.</summary>
    public RealtimeInputConfig? RealtimeInputConfig { get; set; }

    /// <summary>If enabled, the model will detect emotions and adapt its responses accordingly.</summary>
    public bool? EnableAffectiveDialog { get; set; }

    /// <summary>Configures the proactivity of the model.</summary>
    public ProactivityConfig? Proactivity { get; set; }

    /// <summary>Configures session resumption mechanism.</summary>
    public SessionResumptionConfig? SessionResumption { get; set; }

    /// <summary>Configuration for context window compression.</summary>
    public ContextWindowCompressionConfig? ContextWindowCompression { get; set; }

    /// <summary>Saves live video and audio data to session and artifact service.</summary>
    public bool SaveLiveBlob { get; set; }

    /// <summary>Configuration for running tools in a thread pool for live mode.</summary>
    public ToolThreadPoolConfig? ToolThreadPoolConfig { get; set; }

    /// <summary>Custom metadata for the current invocation.</summary>
    public Dictionary<string, object?>? CustomMetadata { get; set; }

    /// <summary>Configuration for controlling which events are fetched when loading a session.</summary>
    public GetSessionConfig? GetSessionConfig { get; set; }
}

public enum StreamingMode
{
    None,
    Sse,
    Bidi
}
