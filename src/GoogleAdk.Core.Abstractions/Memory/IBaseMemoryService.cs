// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Abstractions.Sessions;

namespace GoogleAdk.Core.Abstractions.Memory;

/// <summary>
/// Represents one memory entry.
/// </summary>
public class MemoryEntry
{
    /// <summary>The content of the memory entry.</summary>
    public Content Content { get; set; } = null!;

    /// <summary>The author of the memory.</summary>
    public string? Author { get; set; }

    /// <summary>
    /// The timestamp when the original content of this memory happened (ISO 8601 preferred).
    /// </summary>
    public string? Timestamp { get; set; }
}

public class SearchMemoryRequest
{
    public string AppName { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
}

public class SearchMemoryResponse
{
    public List<MemoryEntry> Memories { get; set; } = new();
}

/// <summary>
/// Base interface for memory services.
/// </summary>
public interface IBaseMemoryService
{
    /// <summary>Adds a session to the memory.</summary>
    Task AddSessionToMemoryAsync(Session session);

    /// <summary>Searches for memories that match the query.</summary>
    Task<SearchMemoryResponse> SearchMemoryAsync(SearchMemoryRequest request);
}
