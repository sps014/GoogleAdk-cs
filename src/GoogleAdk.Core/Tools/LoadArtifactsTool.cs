// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Agents;

namespace GoogleAdk.Core.Tools;

/// <summary>
/// A tool that loads a specific artifact from the artifact service.
/// </summary>
public class LoadArtifactsTool : BaseTool
{
    public static readonly LoadArtifactsTool Instance = new();

    public LoadArtifactsTool()
        : base("load_artifacts",
            "Loads artifacts into the session for this request.\n\n" +
            "NOTE: Call when you need access to artifacts (for example, uploads saved by the web UI).") { }

    public override FunctionDeclaration? GetDeclaration()
        => new FunctionDeclaration
        {
            Name = Name,
            Description = Description,
            Parameters = new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?>
                {
                    ["artifact_name"] = new Dictionary<string, object?>
                    {
                        ["type"] = "string",
                        ["description"] = "The name of the artifact to load."
                    }
                },
                ["required"] = new List<string> { "artifact_name" }
            }
        };

    public override async Task<object?> RunAsync(Dictionary<string, object?> args, AgentContext context)
    {
        var artifactName = args.GetValueOrDefault("artifact_name")?.ToString() ?? string.Empty;
        var artifactService = context.InvocationContext.ArtifactService
            ?? throw new InvalidOperationException("Artifact service is not initialized.");

        var artifact = await artifactService.LoadArtifactAsync(new Abstractions.Artifacts.LoadArtifactRequest
        {
            AppName = context.AppName,
            UserId = context.UserId,
            SessionId = context.Session.Id,
            Filename = artifactName
        });

        if (artifact == null)
            return $"Artifact '{artifactName}' not found.";

        // If the artifact has text, return it directly
        if (artifact.Text != null)
            return artifact.Text;

        // If the artifact has inline data, return a description
        if (artifact.InlineData != null)
        {
            var mimeType = artifact.InlineData.MimeType ?? "application/octet-stream";
            return $"[Artifact: {artifactName}, type: {mimeType}]";
        }

        return $"Artifact '{artifactName}' loaded.";
    }
}
