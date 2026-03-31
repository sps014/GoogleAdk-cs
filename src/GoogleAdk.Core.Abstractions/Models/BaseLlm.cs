// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Events;

namespace GoogleAdk.Core.Abstractions.Models;

/// <summary>
/// Base abstract class for LLM implementations.
/// </summary>
public abstract class BaseLlm
{
    /// <summary>
    /// The model name, e.g. gemini-2.0-flash or gemini-1.5-flash-001.
    /// </summary>
    public string Model { get; }

    protected BaseLlm(string model)
    {
        Model = model;
    }

    /// <summary>
    /// List of supported model patterns (regex) for LlmRegistry.
    /// </summary>
    public static IReadOnlyList<string> SupportedModels => Array.Empty<string>();

    /// <summary>
    /// Generates content from the given request.
    /// For non-streaming calls, yields a single LlmResponse.
    /// For streaming calls, yields partial responses.
    /// </summary>
    /// <param name="llmRequest">The request to send to the LLM.</param>
    /// <param name="stream">Whether to do a streaming call.</param>
    /// <returns>An async enumerable of LlmResponse.</returns>
    public abstract IAsyncEnumerable<LlmResponse> GenerateContentAsync(
        LlmRequest llmRequest,
        bool stream = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a live (bidirectional) connection to the LLM.
    /// </summary>
    /// <param name="llmRequest">The request to send to the LLM.</param>
    /// <returns>A live connection to the LLM.</returns>
    public abstract Task<BaseLlmConnection> ConnectAsync(LlmRequest llmRequest);

    /// <summary>
    /// Ensures there is a user content so the model can continue to output.
    /// </summary>
    public virtual void MaybeAppendUserContent(LlmRequest llmRequest)
    {
        llmRequest.MaybeAppendUserContent();
    }
}

/// <summary>
/// A live bidirectional connection to an LLM.
/// </summary>
public abstract class BaseLlmConnection : IAsyncDisposable
{
    /// <summary>
    /// Sends content to the LLM.
    /// </summary>
    public abstract Task SendAsync(Events.LlmRequest request);

    /// <summary>
    /// Receives responses from the LLM.
    /// </summary>
    public abstract IAsyncEnumerable<LlmResponse> ReceiveAsync();

    /// <summary>
    /// Closes the connection.
    /// </summary>
    public abstract ValueTask DisposeAsync();
}
