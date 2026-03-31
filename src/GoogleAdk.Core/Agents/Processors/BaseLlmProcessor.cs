// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Events;

namespace GoogleAdk.Core.Agents.Processors;

/// <summary>
/// Base class for LLM request processors. Each processor runs before
/// the LLM call and can modify the <see cref="LlmRequest"/> or yield events.
/// </summary>
public abstract class BaseLlmRequestProcessor
{
    /// <summary>
    /// Runs the processor, optionally modifying the request and yielding events.
    /// </summary>
    public abstract IAsyncEnumerable<Event> RunAsync(
        InvocationContext invocationContext,
        LlmRequest llmRequest);
}

/// <summary>
/// Base class for LLM response processors. Each processor runs after
/// the LLM call and can inspect the <see cref="LlmResponse"/> or yield events.
/// </summary>
public abstract class BaseLlmResponseProcessor
{
    /// <summary>
    /// Processes the LLM response, optionally yielding events.
    /// </summary>
    public abstract IAsyncEnumerable<Event> RunAsync(
        InvocationContext invocationContext,
        LlmResponse llmResponse);
}
