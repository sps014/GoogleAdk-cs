// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.RegularExpressions;
using GoogleAdk.Core.Agents;

namespace GoogleAdk.Core.Agents;

/// <summary>
/// Populates values in instruction templates by replacing {var_name} placeholders
/// with session state values and {artifact.file_name} with artifact content.
/// Supports optional variables with {var_name?} syntax.
/// </summary>
public static class InstructionInjector
{
    private static readonly Regex PlaceholderPattern = new(@"\{+[^{}]*\}+", RegexOptions.Compiled);

    /// <summary>
    /// Injects session state and artifact values into an instruction template.
    /// </summary>
    public static async Task<string> InjectSessionStateAsync(string template, ReadonlyContext context)
    {
        var matches = PlaceholderPattern.Matches(template);
        if (matches.Count == 0)
            return template;

        var sb = new StringBuilder();
        int lastEnd = 0;

        foreach (Match match in matches)
        {
            sb.Append(template, lastEnd, match.Index - lastEnd);
            var replacement = await ReplaceMatchAsync(match.Value, context);
            sb.Append(replacement);
            lastEnd = match.Index + match.Length;
        }
        sb.Append(template, lastEnd, template.Length - lastEnd);
        return sb.ToString();
    }

    private static async Task<string> ReplaceMatchAsync(string match, ReadonlyContext context)
    {
        // Strip leading { and trailing }
        var key = match.TrimStart('{').TrimEnd('}').Trim();
        var isOptional = key.EndsWith('?');
        if (isOptional)
            key = key[..^1];

        // Handle artifact injection
        if (key.StartsWith("artifact."))
        {
            var fileName = key["artifact.".Length..];
            var artifactService = context.InvocationContext.ArtifactService
                ?? throw new InvalidOperationException("Artifact service is not initialized.");
            // For now, return the filename placeholder - full artifact loading depends on service implementation
            return $"[artifact:{fileName}]";
        }

        // Handle state variable injection
        if (!IsValidStateName(key))
            return match;

        if (context.State.TryGetValue(key, out var value))
            return value?.ToString() ?? string.Empty;

        if (isOptional)
            return string.Empty;

        throw new InvalidOperationException($"Context variable not found: `{key}`.");
    }

    private static bool IsValidStateName(string variableName)
    {
        var parts = variableName.Split(':');
        if (parts.Length == 0 || parts.Length > 2)
            return false;
        if (parts.Length == 1)
            return IsIdentifier(variableName);
        // Check valid prefixes: "app:", "user:", "temp:"
        var prefix = parts[0] + ":";
        if (prefix is "app:" or "user:" or "temp:")
            return IsIdentifier(parts[1]);
        return false;
    }

    private static bool IsIdentifier(string s)
        => !string.IsNullOrEmpty(s) && Regex.IsMatch(s, @"^[a-zA-Z_][a-zA-Z0-9_]*$");
}
