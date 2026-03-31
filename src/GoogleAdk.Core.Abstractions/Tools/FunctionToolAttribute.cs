// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

namespace GoogleAdk.Core.Abstractions.Tools;

/// <summary>
/// Marks a static method as an ADK FunctionTool. A source generator will
/// create the <c>FunctionTool</c> instance automatically, using XML doc
/// comments for the description and parameter metadata.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class FunctionToolAttribute : Attribute
{
    /// <summary>
    /// Optional override for the tool name. Defaults to the method name in snake_case.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Whether this is a long-running tool.
    /// </summary>
    public bool IsLongRunning { get; set; }
}
