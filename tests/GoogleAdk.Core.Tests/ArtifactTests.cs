// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Artifacts;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Artifacts;

namespace GoogleAdk.Core.Tests;

public class ArtifactTests
{
    // --- InMemoryArtifactService ---

    [Fact]
    public async Task InMemory_SaveAndLoad_TextArtifact()
    {
        var service = new InMemoryArtifactService();

        var version = await service.SaveArtifactAsync(new SaveArtifactRequest
        {
            AppName = "app", UserId = "u1", SessionId = "s1",
            Filename = "notes.txt",
            Artifact = new Part { Text = "Hello world" }
        });

        Assert.Equal(0, version);

        var loaded = await service.LoadArtifactAsync(new LoadArtifactRequest
        {
            AppName = "app", UserId = "u1", SessionId = "s1",
            Filename = "notes.txt"
        });

        Assert.NotNull(loaded);
        Assert.Equal("Hello world", loaded!.Text);
    }

    [Fact]
    public async Task InMemory_MultipleVersions()
    {
        var service = new InMemoryArtifactService();
        var req = new SaveArtifactRequest
        {
            AppName = "app", UserId = "u1", SessionId = "s1",
            Filename = "doc.txt",
            Artifact = new Part { Text = "v0" }
        };

        var v0 = await service.SaveArtifactAsync(req);
        req.Artifact = new Part { Text = "v1" };
        var v1 = await service.SaveArtifactAsync(req);

        Assert.Equal(0, v0);
        Assert.Equal(1, v1);

        var latest = await service.LoadArtifactAsync(new LoadArtifactRequest
        {
            AppName = "app", UserId = "u1", SessionId = "s1",
            Filename = "doc.txt"
        });
        Assert.Equal("v1", latest!.Text);

        var first = await service.LoadArtifactAsync(new LoadArtifactRequest
        {
            AppName = "app", UserId = "u1", SessionId = "s1",
            Filename = "doc.txt",
            Version = 0
        });
        Assert.Equal("v0", first!.Text);
    }

    [Fact]
    public async Task InMemory_ListArtifactKeys()
    {
        var service = new InMemoryArtifactService();

        await service.SaveArtifactAsync(new SaveArtifactRequest
        {
            AppName = "app", UserId = "u1", SessionId = "s1",
            Filename = "a.txt", Artifact = new Part { Text = "a" }
        });
        await service.SaveArtifactAsync(new SaveArtifactRequest
        {
            AppName = "app", UserId = "u1", SessionId = "s1",
            Filename = "b.txt", Artifact = new Part { Text = "b" }
        });

        var keys = await service.ListArtifactKeysAsync(new ListArtifactKeysRequest
        {
            AppName = "app", UserId = "u1", SessionId = "s1"
        });

        Assert.Equal(2, keys.Count);
        Assert.Contains("a.txt", keys);
        Assert.Contains("b.txt", keys);
    }

    [Fact]
    public async Task InMemory_DeleteArtifact()
    {
        var service = new InMemoryArtifactService();

        await service.SaveArtifactAsync(new SaveArtifactRequest
        {
            AppName = "app", UserId = "u1", SessionId = "s1",
            Filename = "temp.txt", Artifact = new Part { Text = "temp" }
        });

        await service.DeleteArtifactAsync(new DeleteArtifactRequest
        {
            AppName = "app", UserId = "u1", SessionId = "s1",
            Filename = "temp.txt"
        });

        var loaded = await service.LoadArtifactAsync(new LoadArtifactRequest
        {
            AppName = "app", UserId = "u1", SessionId = "s1",
            Filename = "temp.txt"
        });

        Assert.Null(loaded);
    }

    [Fact]
    public async Task InMemory_ListVersions()
    {
        var service = new InMemoryArtifactService();

        for (int i = 0; i < 3; i++)
        {
            await service.SaveArtifactAsync(new SaveArtifactRequest
            {
                AppName = "app", UserId = "u1", SessionId = "s1",
                Filename = "doc.txt", Artifact = new Part { Text = $"v{i}" }
            });
        }

        var versions = await service.ListVersionsAsync(new ListVersionsRequest
        {
            AppName = "app", UserId = "u1", SessionId = "s1",
            Filename = "doc.txt"
        });

        Assert.Equal(new List<int> { 0, 1, 2 }, versions);
    }

    [Fact]
    public async Task InMemory_LoadReturnsNull_WhenMissing()
    {
        var service = new InMemoryArtifactService();

        var loaded = await service.LoadArtifactAsync(new LoadArtifactRequest
        {
            AppName = "app", UserId = "u1", SessionId = "s1",
            Filename = "missing.txt"
        });

        Assert.Null(loaded);
    }

    // --- FileArtifactService ---

    [Fact]
    public async Task File_SaveAndLoad_TextArtifact()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"adk-test-{Guid.NewGuid()}");
        try
        {
            var service = new FileArtifactService(tempDir);

            var version = await service.SaveArtifactAsync(new SaveArtifactRequest
            {
                AppName = "app", UserId = "u1", SessionId = "s1",
                Filename = "report.txt",
                Artifact = new Part { Text = "Quarterly report" }
            });

            Assert.Equal(0, version);

            var loaded = await service.LoadArtifactAsync(new LoadArtifactRequest
            {
                AppName = "app", UserId = "u1", SessionId = "s1",
                Filename = "report.txt"
            });

            Assert.NotNull(loaded);
            Assert.Equal("Quarterly report", loaded!.Text);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task File_SaveAndLoad_BinaryArtifact()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"adk-test-{Guid.NewGuid()}");
        try
        {
            var service = new FileArtifactService(tempDir);

            var data = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
            var version = await service.SaveArtifactAsync(new SaveArtifactRequest
            {
                AppName = "app", UserId = "u1", SessionId = "s1",
                Filename = "image.png",
                Artifact = new Part
                {
                    InlineData = new InlineData
                    {
                        MimeType = "image/png",
                        Data = Convert.ToBase64String(data)
                    }
                }
            });

            Assert.Equal(0, version);

            var loaded = await service.LoadArtifactAsync(new LoadArtifactRequest
            {
                AppName = "app", UserId = "u1", SessionId = "s1",
                Filename = "image.png"
            });

            Assert.NotNull(loaded);
            Assert.NotNull(loaded!.InlineData);
            Assert.Equal("image/png", loaded.InlineData!.MimeType);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task File_MultipleVersions()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"adk-test-{Guid.NewGuid()}");
        try
        {
            var service = new FileArtifactService(tempDir);

            await service.SaveArtifactAsync(new SaveArtifactRequest
            {
                AppName = "app", UserId = "u1", SessionId = "s1",
                Filename = "doc.txt", Artifact = new Part { Text = "v0" }
            });
            await service.SaveArtifactAsync(new SaveArtifactRequest
            {
                AppName = "app", UserId = "u1", SessionId = "s1",
                Filename = "doc.txt", Artifact = new Part { Text = "v1" }
            });

            var latest = await service.LoadArtifactAsync(new LoadArtifactRequest
            {
                AppName = "app", UserId = "u1", SessionId = "s1",
                Filename = "doc.txt"
            });
            Assert.Equal("v1", latest!.Text);

            var first = await service.LoadArtifactAsync(new LoadArtifactRequest
            {
                AppName = "app", UserId = "u1", SessionId = "s1",
                Filename = "doc.txt", Version = 0
            });
            Assert.Equal("v0", first!.Text);

            var versions = await service.ListVersionsAsync(new ListVersionsRequest
            {
                AppName = "app", UserId = "u1", SessionId = "s1",
                Filename = "doc.txt"
            });
            Assert.Equal(new List<int> { 0, 1 }, versions);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task File_ListArtifactKeys()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"adk-test-{Guid.NewGuid()}");
        try
        {
            var service = new FileArtifactService(tempDir);

            await service.SaveArtifactAsync(new SaveArtifactRequest
            {
                AppName = "app", UserId = "u1", SessionId = "s1",
                Filename = "a.txt", Artifact = new Part { Text = "a" }
            });
            await service.SaveArtifactAsync(new SaveArtifactRequest
            {
                AppName = "app", UserId = "u1", SessionId = "s1",
                Filename = "b.txt", Artifact = new Part { Text = "b" }
            });

            var keys = await service.ListArtifactKeysAsync(new ListArtifactKeysRequest
            {
                AppName = "app", UserId = "u1", SessionId = "s1"
            });

            Assert.Equal(2, keys.Count);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}
