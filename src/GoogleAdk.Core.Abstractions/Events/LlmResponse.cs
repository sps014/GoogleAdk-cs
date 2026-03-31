// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Models;

namespace GoogleAdk.Core.Abstractions.Events;

/// <summary>
/// LLM response class that provides the first candidate response from the
/// model if available. Otherwise, returns error code and message.
/// </summary>
public class LlmResponse
{
    /// <summary>The content of the response.</summary>
    public Content? Content { get; set; }

    /// <summary>The grounding metadata of the response.</summary>
    public GroundingMetadata? GroundingMetadata { get; set; }

    /// <summary>The citation metadata of the response.</summary>
    public CitationMetadata? CitationMetadata { get; set; }

    /// <summary>
    /// Indicates whether the text content is part of an unfinished text stream.
    /// Only used for streaming mode when the content is plain text.
    /// </summary>
    public bool? Partial { get; set; }

    /// <summary>
    /// Indicates whether the response from the model is complete. Only used for streaming mode.
    /// </summary>
    public bool? TurnComplete { get; set; }

    /// <summary>Error code if the response is an error.</summary>
    public string? ErrorCode { get; set; }

    /// <summary>Error message if the response is an error.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Flag indicating that LLM was interrupted when generating the content.
    /// Usually due to user interruption during a bidi streaming.
    /// </summary>
    public bool? Interrupted { get; set; }

    /// <summary>
    /// Custom metadata for the LlmResponse. Must be JSON serializable.
    /// </summary>
    public Dictionary<string, object?>? CustomMetadata { get; set; }

    /// <summary>The usage metadata of the LlmResponse.</summary>
    public UsageMetadata? UsageMetadata { get; set; }

    /// <summary>The finish reason of the response.</summary>
    public string? FinishReason { get; set; }

    /// <summary>Audio transcription of user input.</summary>
    public Transcription? InputTranscription { get; set; }

    /// <summary>Audio transcription of model output.</summary>
    public Transcription? OutputTranscription { get; set; }
}
