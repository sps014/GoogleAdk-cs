using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using GoogleAdk.Core.Abstractions.Artifacts;
using GoogleAdk.Core.Abstractions.Models;

namespace GoogleAdk.Core.Artifacts;

/// <summary>
/// A file-system-based implementation of the artifact service.
/// Stores artifacts and their metadata in the local file system.
/// </summary>
public class FileArtifactService : IBaseArtifactService
{
	private readonly string _rootDir;

	/// <summary>
	/// Initializes a new instance of the <see cref="FileArtifactService"/> class.
	/// </summary>
	/// <param name="rootDir">The root directory where artifacts will be stored.</param>
	public FileArtifactService(string rootDir)
	{
		_rootDir = Path.GetFullPath(rootDir);
	}

	/// <inheritdoc/>
	public async Task<int> SaveArtifactAsync(SaveArtifactRequest request)
	{
		if (request.Artifact.InlineData == null && request.Artifact.Text == null)
		{
			throw new ArgumentException("Artifact must have either InlineData or Text content.");
		}
		string artifactDir = GetArtifactDir(request.UserId, request.SessionId, request.Filename);
		Directory.CreateDirectory(artifactDir);
		List<int> versions = GetVersionsFromDir(artifactDir);
		int nextVersion = versions.Count > 0 ? versions[versions.Count - 1] + 1 : 0;
		
		string versionDir = Path.Combine(GetVersionsDir(artifactDir), nextVersion.ToString());
		Directory.CreateDirectory(versionDir);
		string sanitizedFilename = SanitizeFilename(request.Filename);
		string contentPath = Path.Combine(versionDir, sanitizedFilename);
		string? mimeType = null;
		if (request.Artifact.InlineData != null)
		{
			byte[] data = Convert.FromBase64String(request.Artifact.InlineData.Data ?? string.Empty);
			await File.WriteAllBytesAsync(contentPath, data);
			mimeType = request.Artifact.InlineData.MimeType ?? "application/octet-stream";
		}
		else if (request.Artifact.Text != null)
		{
			await File.WriteAllTextAsync(contentPath, request.Artifact.Text);
		}
		await WriteMetadataAsync(Path.Combine(versionDir, "metadata.json"), new ArtifactVersion
		{
			Version = nextVersion,
			MimeType = mimeType,
			CustomMetadata = request.CustomMetadata,
			CanonicalUri = new Uri(contentPath).AbsoluteUri
		});
		return nextVersion;
	}

	/// <inheritdoc/>
	public async Task<Part?> LoadArtifactAsync(LoadArtifactRequest request)
	{
		string artifactDir = GetArtifactDir(request.UserId, request.SessionId, request.Filename);
		if (!Directory.Exists(artifactDir)) return null;
		
		List<int> versions = GetVersionsFromDir(artifactDir);
		if (versions.Count == 0) return null;
		
		int versionToLoad;
		if (!request.Version.HasValue)
		{
			versionToLoad = versions[versions.Count - 1];
		}
		else
		{
			if (!versions.Contains(request.Version.Value)) return null;
			versionToLoad = request.Version.Value;
		}
		string versionDir = Path.Combine(GetVersionsDir(artifactDir), versionToLoad.ToString());
		string metadataPath = Path.Combine(versionDir, "metadata.json");
		ArtifactVersion? metadata = await ReadMetadataAsync(metadataPath);
		string sanitizedFilename = SanitizeFilename(request.Filename);
		string contentPath = Path.Combine(versionDir, sanitizedFilename);
		if (metadata?.CanonicalUri != null)
		{
			try
			{
				string uriPath = new Uri(metadata.CanonicalUri).LocalPath;
				if (File.Exists(uriPath)) contentPath = uriPath;
			}
			catch { }
		}
		if (!string.IsNullOrEmpty(metadata?.MimeType))
		{
			byte[] data = await File.ReadAllBytesAsync(contentPath);
			return new Part
			{
				InlineData = new InlineData
				{
					MimeType = metadata.MimeType,
					Data = Convert.ToBase64String(data)
				}
			};
		}
		string text = await File.ReadAllTextAsync(contentPath);
		return new Part { Text = text };
	}

	/// <inheritdoc/>
	public Task<List<string>> ListArtifactKeysAsync(ListArtifactKeysRequest request)
	{
		List<string> list = new List<string>();
		string path = Path.Combine(_rootDir, "users", request.UserId, "sessions", request.SessionId, "artifacts");
		if (Directory.Exists(path))
		{
			foreach (string d in Directory.GetDirectories(path))
			{
				list.Add(Path.GetFileName(d));
			}
		}
		string userPath = Path.Combine(_rootDir, "users", request.UserId, "artifacts");
		if (Directory.Exists(userPath))
		{
			foreach (string d in Directory.GetDirectories(userPath))
			{
				list.Add("user:" + Path.GetFileName(d));
			}
		}
		list.Sort(StringComparer.Ordinal);
		return Task.FromResult(list);
	}

	/// <inheritdoc/>
	public Task DeleteArtifactAsync(DeleteArtifactRequest request)
	{
		string artifactDir = GetArtifactDir(request.UserId, request.SessionId, request.Filename);
		if (Directory.Exists(artifactDir))
		{
			Directory.Delete(artifactDir, recursive: true);
		}
		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	public Task<List<int>> ListVersionsAsync(ListVersionsRequest request)
	{
		string artifactDir = GetArtifactDir(request.UserId, request.SessionId, request.Filename);
		return Task.FromResult(GetVersionsFromDir(artifactDir));
	}

	/// <inheritdoc/>
	public async Task<List<ArtifactVersion>> ListArtifactVersionsAsync(ListVersionsRequest request)
	{
		string artifactDir = GetArtifactDir(request.UserId, request.SessionId, request.Filename);
		List<int> versions = GetVersionsFromDir(artifactDir);
		List<ArtifactVersion> result = new List<ArtifactVersion>();
		foreach (int v in versions)
		{
			string versionDir = Path.Combine(GetVersionsDir(artifactDir), v.ToString());
			string metadataPath = Path.Combine(versionDir, "metadata.json");
			ArtifactVersion? meta = await ReadMetadataAsync(metadataPath);
			if (meta != null) result.Add(meta);
		}
		return result;
	}

	/// <inheritdoc/>
	public async Task<ArtifactVersion?> GetArtifactVersionAsync(LoadArtifactRequest request)
	{
		string artifactDir = GetArtifactDir(request.UserId, request.SessionId, request.Filename);
		List<int> versions = GetVersionsFromDir(artifactDir);
		if (versions.Count == 0) return null;
		
		int versionToLoad = request.Version ?? versions[versions.Count - 1];
		string versionDir = Path.Combine(GetVersionsDir(artifactDir), versionToLoad.ToString());
		string metadataPath = Path.Combine(versionDir, "metadata.json");
		return await ReadMetadataAsync(metadataPath);
	}

	private string GetArtifactDir(string userId, string sessionId, string filename)
	{
		string name = filename.StartsWith("user:") ? filename.Substring(5) : filename;
		string text = SanitizeFilename(name);
		
		if (filename.StartsWith("user:"))
		{
			return Path.Combine(_rootDir, "users", userId, "artifacts", text);
		}
		return Path.Combine(_rootDir, "users", userId, "sessions", sessionId, "artifacts", text);
	}

	private static string GetVersionsDir(string artifactDir) => Path.Combine(artifactDir, "versions");

	private static List<int> GetVersionsFromDir(string artifactDir)
	{
		string versionsDir = GetVersionsDir(artifactDir);
		if (!Directory.Exists(versionsDir)) return new List<int>();
		
		return Directory.GetDirectories(versionsDir)
			.Select(d => int.TryParse(Path.GetFileName(d), out int result) ? result : -1)
			.Where(v => v >= 0)
			.OrderBy(v => v)
			.ToList();
	}

	private static string SanitizeFilename(string filename)
	{
		if (filename.Contains("..")) throw new ArgumentException("Filename cannot contain path traversal sequences.");
		return Path.GetFileName(filename);
	}

	private static async Task WriteMetadataAsync(string path, ArtifactVersion metadata)
	{
		string json = JsonSerializer.Serialize(metadata);
		await File.WriteAllTextAsync(path, json);
	}

	private static async Task<ArtifactVersion?> ReadMetadataAsync(string path)
	{
		if (!File.Exists(path)) return null;
		return JsonSerializer.Deserialize<ArtifactVersion>(await File.ReadAllTextAsync(path));
	}
}