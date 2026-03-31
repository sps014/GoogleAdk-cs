// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

// ============================================================================
// Artifacts Sample — File-Based & In-Memory Artifact Storage
// ============================================================================
//
// Demonstrates:
//   1. InMemoryArtifactService — ephemeral artifact storage
//   2. FileArtifactService — persistent filesystem-backed storage with versioning
//   3. Saving, loading, versioning, and listing artifacts
// ============================================================================

using GoogleAdk.Core.Abstractions.Artifacts;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Artifacts;

Console.WriteLine("=== Artifacts Sample ===\n");

// ── 1. InMemoryArtifactService ─────────────────────────────────────────────

Console.WriteLine("--- InMemoryArtifactService ---\n");

var memoryService = new InMemoryArtifactService();

// Save a text artifact
var v0 = await memoryService.SaveArtifactAsync(new SaveArtifactRequest
{
    AppName = "demo-app",
    UserId = "user-1",
    SessionId = "session-1",
    Filename = "notes.txt",
    Artifact = new Part { Text = "First version of notes." }
});
Console.WriteLine($"Saved notes.txt version: {v0}");

// Save a second version
var v1 = await memoryService.SaveArtifactAsync(new SaveArtifactRequest
{
    AppName = "demo-app",
    UserId = "user-1",
    SessionId = "session-1",
    Filename = "notes.txt",
    Artifact = new Part { Text = "Updated notes with more detail." }
});
Console.WriteLine($"Saved notes.txt version: {v1}");

// Load latest version
var latest = await memoryService.LoadArtifactAsync(new LoadArtifactRequest
{
    AppName = "demo-app",
    UserId = "user-1",
    SessionId = "session-1",
    Filename = "notes.txt"
});
Console.WriteLine($"Latest version text: \"{latest?.Text}\"");

// Load specific version
var first = await memoryService.LoadArtifactAsync(new LoadArtifactRequest
{
    AppName = "demo-app",
    UserId = "user-1",
    SessionId = "session-1",
    Filename = "notes.txt",
    Version = 0
});
Console.WriteLine($"Version 0 text: \"{first?.Text}\"");

// List artifact keys
var keys = await memoryService.ListArtifactKeysAsync(new ListArtifactKeysRequest
{
    AppName = "demo-app",
    UserId = "user-1",
    SessionId = "session-1"
});
Console.WriteLine($"Artifact keys: [{string.Join(", ", keys)}]");

// List versions
var versions = await memoryService.ListVersionsAsync(new ListVersionsRequest
{
    AppName = "demo-app",
    UserId = "user-1",
    SessionId = "session-1",
    Filename = "notes.txt"
});
Console.WriteLine($"Available versions: [{string.Join(", ", versions)}]");

// ── 2. FileArtifactService ─────────────────────────────────────────────────

Console.WriteLine("\n--- FileArtifactService ---\n");

var tempDir = Path.Combine(Path.GetTempPath(), "adk-artifacts-sample");
var fileService = new FileArtifactService(tempDir);

Console.WriteLine($"Storage root: {tempDir}");

// Save a text artifact
var fv0 = await fileService.SaveArtifactAsync(new SaveArtifactRequest
{
    AppName = "demo-app",
    UserId = "user-1",
    SessionId = "session-1",
    Filename = "report.txt",
    Artifact = new Part { Text = "Quarterly report: Q1 revenue $1.2M" },
    CustomMetadata = new Dictionary<string, object?> { ["department"] = "finance" }
});
Console.WriteLine($"Saved report.txt to filesystem, version: {fv0}");

// Save a binary artifact (simulated image)
var imageBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG header stub
var fv1 = await fileService.SaveArtifactAsync(new SaveArtifactRequest
{
    AppName = "demo-app",
    UserId = "user-1",
    SessionId = "session-1",
    Filename = "chart.png",
    Artifact = new Part
    {
        InlineData = new InlineData
        {
            MimeType = "image/png",
            Data = Convert.ToBase64String(imageBytes)
        }
    }
});
Console.WriteLine($"Saved chart.png to filesystem, version: {fv1}");

// Load the text artifact back
var fileLoaded = await fileService.LoadArtifactAsync(new LoadArtifactRequest
{
    AppName = "demo-app",
    UserId = "user-1",
    SessionId = "session-1",
    Filename = "report.txt"
});
Console.WriteLine($"Loaded report.txt: \"{fileLoaded?.Text}\"");

// List keys
var fileKeys = await fileService.ListArtifactKeysAsync(new ListArtifactKeysRequest
{
    AppName = "demo-app",
    UserId = "user-1",
    SessionId = "session-1"
});
Console.WriteLine($"File artifact keys: [{string.Join(", ", fileKeys)}]");

// Get version metadata
var versionInfo = await fileService.GetArtifactVersionAsync(new LoadArtifactRequest
{
    AppName = "demo-app",
    UserId = "user-1",
    SessionId = "session-1",
    Filename = "report.txt"
});
Console.WriteLine($"report.txt version metadata: v{versionInfo?.Version}, mime={versionInfo?.MimeType ?? "text"}");

// Cleanup
try { Directory.Delete(tempDir, true); } catch { }
Console.WriteLine($"\nCleaned up temp directory.");

Console.WriteLine("\n=== Artifacts Sample Complete ===");
