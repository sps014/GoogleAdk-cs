using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using GoogleAdk.Core.Abstractions.Artifacts;
using GoogleAdk.Core.Abstractions.Models;
using Microsoft.EntityFrameworkCore;

namespace GoogleAdk.Sessions.EfCore;

/// <summary>
/// EF Core-based artifact service supporting any database provider.
/// Stores artifacts natively as BLOBs/VARBINARY, allowing seamless streaming
/// support for audio, video, and other file types alongside session state.
/// </summary>
public class EfCoreArtifactService : IBaseArtifactService
{
    private readonly IDbContextFactory<AdkSessionDbContext> _dbFactory;

    public EfCoreArtifactService(IDbContextFactory<AdkSessionDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    private (string RealFilename, string? ResolvedSessionId) ParseIdentity(string filename, string defaultSessionId)
    {
        if (filename.StartsWith("user:"))
        {
            return (filename.Substring(5), null); // Global user artifact, no session
        }
        return (filename, defaultSessionId);
    }

    public async Task<int> SaveArtifactAsync(SaveArtifactRequest request)
    {
        if (request.Artifact.InlineData == null && request.Artifact.Text == null)
        {
            throw new ArgumentException("Artifact must have either InlineData or Text content.");
        }

        var (filename, sessionId) = ParseIdentity(request.Filename, request.SessionId);
        // The EF Core composite key doesn't allow nulls in SQL Server for primary keys typically,
        // but EF Core handles nullable strings in composite keys differently based on provider.
        // Actually, we mapped the key to have e.SessionId. If SessionId is null, we can just use empty string.
        var effectiveSessionId = sessionId ?? string.Empty;

        await using var db = await _dbFactory.CreateDbContextAsync();

        // 1. Get or Create the Artifact Identity
        var artifact = await db.Artifacts
            .FirstOrDefaultAsync(a => a.AppName == request.AppName &&
                                      a.UserId == request.UserId &&
                                      a.SessionId == effectiveSessionId &&
                                      a.Filename == filename);

        if (artifact == null)
        {
            artifact = new StorageArtifact
            {
                AppName = request.AppName,
                UserId = request.UserId,
                SessionId = effectiveSessionId,
                Filename = filename
            };
            db.Artifacts.Add(artifact);
            await db.SaveChangesAsync(); // save immediately to establish parent
        }

        // 2. Determine Next Version
        var latestVersion = await db.ArtifactVersions
            .Where(v => v.AppName == request.AppName &&
                        v.UserId == request.UserId &&
                        v.SessionId == effectiveSessionId &&
                        v.Filename == filename)
            .MaxAsync(v => (int?)v.Version) ?? -1;

        int nextVersion = latestVersion + 1;

        // 3. Create the Payload Version
        var version = new StorageArtifactVersion
        {
            AppName = request.AppName,
            UserId = request.UserId,
            SessionId = effectiveSessionId,
            Filename = filename,
            Version = nextVersion,
            CustomMetadataJson = request.CustomMetadata != null ? JsonSerializer.Serialize(request.CustomMetadata) : null,
            // Create a pseudo-URI for tracking
            CanonicalUri = $"efcore://{request.AppName}/{request.UserId}/{effectiveSessionId}/{filename}/v{nextVersion}"
        };

        if (request.Artifact.InlineData != null)
        {
            version.MimeType = request.Artifact.InlineData.MimeType ?? "application/octet-stream";
            version.Data = Convert.FromBase64String(request.Artifact.InlineData.Data ?? string.Empty);
        }
        else if (request.Artifact.Text != null)
        {
            version.Text = request.Artifact.Text;
        }

        db.ArtifactVersions.Add(version);
        await db.SaveChangesAsync();

        return nextVersion;
    }

    public async Task<Part?> LoadArtifactAsync(LoadArtifactRequest request)
    {
        var (filename, sessionId) = ParseIdentity(request.Filename, request.SessionId);
        var effectiveSessionId = sessionId ?? string.Empty;

        await using var db = await _dbFactory.CreateDbContextAsync();

        var query = db.ArtifactVersions
            .Where(v => v.AppName == request.AppName &&
                        v.UserId == request.UserId &&
                        v.SessionId == effectiveSessionId &&
                        v.Filename == filename);

        if (request.Version.HasValue)
        {
            query = query.Where(v => v.Version == request.Version.Value);
        }
        else
        {
            query = query.OrderByDescending(v => v.Version).Take(1);
        }

        var version = await query.FirstOrDefaultAsync();
        if (version == null) return null;

        if (version.Data != null)
        {
            return new Part
            {
                InlineData = new InlineData
                {
                    MimeType = version.MimeType ?? "application/octet-stream",
                    Data = Convert.ToBase64String(version.Data)
                }
            };
        }

        if (version.Text != null)
        {
            return new Part { Text = version.Text };
        }

        return null;
    }

    public async Task<List<string>> ListArtifactKeysAsync(ListArtifactKeysRequest request)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        // Fetch session scoped keys
        var sessionKeys = await db.Artifacts
            .Where(a => a.AppName == request.AppName &&
                        a.UserId == request.UserId &&
                        a.SessionId == request.SessionId)
            .Select(a => a.Filename)
            .ToListAsync();

        // Fetch user scoped keys
        var userKeys = await db.Artifacts
            .Where(a => a.AppName == request.AppName &&
                        a.UserId == request.UserId &&
                        a.SessionId == string.Empty)
            .Select(a => "user:" + a.Filename)
            .ToListAsync();

        var allKeys = sessionKeys.Concat(userKeys).OrderBy(k => k, StringComparer.Ordinal).ToList();
        return allKeys;
    }

    public async Task DeleteArtifactAsync(DeleteArtifactRequest request)
    {
        var (filename, sessionId) = ParseIdentity(request.Filename, request.SessionId);
        var effectiveSessionId = sessionId ?? string.Empty;

        await using var db = await _dbFactory.CreateDbContextAsync();

        var artifact = await db.Artifacts
            .FirstOrDefaultAsync(a => a.AppName == request.AppName &&
                                      a.UserId == request.UserId &&
                                      a.SessionId == effectiveSessionId &&
                                      a.Filename == filename);

        if (artifact != null)
        {
            db.Artifacts.Remove(artifact); // Cascade delete will remove versions
            await db.SaveChangesAsync();
        }
    }

    public async Task<List<int>> ListVersionsAsync(ListVersionsRequest request)
    {
        var (filename, sessionId) = ParseIdentity(request.Filename, request.SessionId);
        var effectiveSessionId = sessionId ?? string.Empty;

        await using var db = await _dbFactory.CreateDbContextAsync();

        var versions = await db.ArtifactVersions
            .Where(v => v.AppName == request.AppName &&
                        v.UserId == request.UserId &&
                        v.SessionId == effectiveSessionId &&
                        v.Filename == filename)
            .Select(v => v.Version)
            .OrderBy(v => v)
            .ToListAsync();

        return versions;
    }

    public async Task<List<ArtifactVersion>> ListArtifactVersionsAsync(ListVersionsRequest request)
    {
        var (filename, sessionId) = ParseIdentity(request.Filename, request.SessionId);
        var effectiveSessionId = sessionId ?? string.Empty;

        await using var db = await _dbFactory.CreateDbContextAsync();

        var versions = await db.ArtifactVersions
            .Where(v => v.AppName == request.AppName &&
                        v.UserId == request.UserId &&
                        v.SessionId == effectiveSessionId &&
                        v.Filename == filename)
            .OrderBy(v => v.Version)
            .Select(v => new ArtifactVersion
            {
                Version = v.Version,
                MimeType = v.MimeType,
                CanonicalUri = v.CanonicalUri,
                CustomMetadata = v.CustomMetadataJson != null ? 
                    JsonSerializer.Deserialize<Dictionary<string, object?>>(v.CustomMetadataJson, (JsonSerializerOptions)null!) : null
            })
            .ToListAsync();

        return versions;
    }

    public async Task<ArtifactVersion?> GetArtifactVersionAsync(LoadArtifactRequest request)
    {
        var (filename, sessionId) = ParseIdentity(request.Filename, request.SessionId);
        var effectiveSessionId = sessionId ?? string.Empty;

        await using var db = await _dbFactory.CreateDbContextAsync();

        var query = db.ArtifactVersions
            .Where(v => v.AppName == request.AppName &&
                        v.UserId == request.UserId &&
                        v.SessionId == effectiveSessionId &&
                        v.Filename == filename);

        if (request.Version.HasValue)
        {
            query = query.Where(v => v.Version == request.Version.Value);
        }
        else
        {
            query = query.OrderByDescending(v => v.Version).Take(1);
        }

        var version = await query.Select(v => new ArtifactVersion
            {
                Version = v.Version,
                MimeType = v.MimeType,
                CanonicalUri = v.CanonicalUri,
                CustomMetadata = v.CustomMetadataJson != null ? 
                    JsonSerializer.Deserialize<Dictionary<string, object?>>(v.CustomMetadataJson, (JsonSerializerOptions)null!) : null
            }).FirstOrDefaultAsync();

        return version;
    }
}
