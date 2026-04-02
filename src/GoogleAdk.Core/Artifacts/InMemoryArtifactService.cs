using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GoogleAdk.Core.Abstractions.Artifacts;
using GoogleAdk.Core.Abstractions.Models;

namespace GoogleAdk.Core.Artifacts;

/// <summary>
/// An in-memory implementation of the artifact service, useful for testing or short-lived scenarios.
/// </summary>
public class InMemoryArtifactService : IBaseArtifactService
{
	private readonly ConcurrentDictionary<string, List<(Part Part, ArtifactVersion Metadata)>> _artifacts = new();

	/// <inheritdoc/>
	public Task<int> SaveArtifactAsync(SaveArtifactRequest request)
	{
		if (request.Artifact.InlineData == null && request.Artifact.Text == null)
		{
			throw new ArgumentException("Artifact must have either InlineData or Text content.");
		}
		string artifactPath = GetArtifactPath(request.AppName, request.UserId, request.SessionId, request.Filename);
		Console.WriteLine("[InMemoryArtifactService] SAVING: " + artifactPath);
		List<(Part Part, ArtifactVersion Metadata)> list = _artifacts.GetOrAdd(artifactPath, _ => new List<(Part, ArtifactVersion)>());
		int count = list.Count;
		string canonicalUri = $"memory://apps/{request.AppName}/users/{request.UserId}/sessions/{request.SessionId}/artifacts/{request.Filename}/versions/{count}";
		string mimeType = request.Artifact.InlineData?.MimeType ?? "text/plain";
		ArtifactVersion item = new ArtifactVersion
		{
			Version = count,
			CanonicalUri = canonicalUri,
			MimeType = mimeType,
			CustomMetadata = request.CustomMetadata
		};
		list.Add((request.Artifact, item));
		return Task.FromResult(count);
	}

	/// <inheritdoc/>
	public Task<Part?> LoadArtifactAsync(LoadArtifactRequest request)
	{
		string artifactPath = GetArtifactPath(request.AppName, request.UserId, request.SessionId, request.Filename);
		if (!_artifacts.TryGetValue(artifactPath, out List<(Part Part, ArtifactVersion Metadata)>? value))
		{
			return Task.FromResult<Part?>(null);
		}
		int index = request.Version ?? (value.Count - 1);
		return Task.FromResult<Part?>(value[index].Part);
	}

	/// <inheritdoc/>
	public Task<List<string>> ListArtifactKeysAsync(ListArtifactKeysRequest request)
	{
		string sessionPrefix = $"{request.AppName}/{request.UserId}/{request.SessionId}/";
		string userPrefix = request.AppName + "/" + request.UserId + "/user/";
		List<string> list = new List<string>();
		foreach (string key in _artifacts.Keys)
		{
			if (key.StartsWith(sessionPrefix))
			{
				list.Add(key.Substring(sessionPrefix.Length));
			}
			else if (key.StartsWith(userPrefix))
			{
				list.Add(key.Substring(userPrefix.Length));
			}
		}
		list.Sort();
		return Task.FromResult(list);
	}

	/// <inheritdoc/>
	public Task DeleteArtifactAsync(DeleteArtifactRequest request)
	{
		string artifactPath = GetArtifactPath(request.AppName, request.UserId, request.SessionId, request.Filename);
		_artifacts.TryRemove(artifactPath, out _);
		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	public Task<List<int>> ListVersionsAsync(ListVersionsRequest request)
	{
		string artifactPath = GetArtifactPath(request.AppName, request.UserId, request.SessionId, request.Filename);
		if (!_artifacts.TryGetValue(artifactPath, out var value))
		{
			return Task.FromResult(new List<int>());
		}
		return Task.FromResult(Enumerable.Range(0, value.Count).ToList());
	}

	/// <inheritdoc/>
	public Task<List<ArtifactVersion>> ListArtifactVersionsAsync(ListVersionsRequest request)
	{
		string artifactPath = GetArtifactPath(request.AppName, request.UserId, request.SessionId, request.Filename);
		if (!_artifacts.TryGetValue(artifactPath, out var value))
		{
			return Task.FromResult(new List<ArtifactVersion>());
		}
		return Task.FromResult(value.Select(a => a.Metadata).ToList());
	}

	/// <inheritdoc/>
	public Task<ArtifactVersion?> GetArtifactVersionAsync(LoadArtifactRequest request)
	{
		string artifactPath = GetArtifactPath(request.AppName, request.UserId, request.SessionId, request.Filename);
		Console.WriteLine("[InMemoryArtifactService] GET_METADATA: " + artifactPath);
		if (!_artifacts.TryGetValue(artifactPath, out var value))
		{
			return Task.FromResult<ArtifactVersion?>(null);
		}
		int num = request.Version ?? (value.Count - 1);
		if (num >= 0 && num < value.Count)
		{
			return Task.FromResult<ArtifactVersion?>(value[num].Metadata);
		}
		return Task.FromResult<ArtifactVersion?>(null);
	}

	private static string GetArtifactPath(string appName, string userId, string sessionId, string filename)
	{
		if (!filename.StartsWith("user:"))
		{
			return $"{appName}/{userId}/{sessionId}/{filename}";
		}
		return $"{appName}/{userId}/user/{filename}";
	}
}