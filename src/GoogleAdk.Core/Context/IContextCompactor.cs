// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Agents;

namespace GoogleAdk.Core.Context;

/// <summary>
/// Interface for compacting the context history in an agent session.
/// Implementations decide when and how to trim conversation history.
/// </summary>
public interface IContextCompactor
{
    /// <summary>
    /// Determines whether the context should be compacted.
    /// </summary>
    Task<bool> ShouldCompactAsync(InvocationContext invocationContext);

    /// <summary>
    /// Compacts the context in place by modifying the session events.
    /// </summary>
    Task CompactAsync(InvocationContext invocationContext);
}
