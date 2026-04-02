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
	// Stores artifacts in memory using their path as the key, mapped to a list of versions
	private readonly ConcurrentDictionary<string, List<(Part Part, ArtifactVersion Metadata)>> _artifacts = new();

	/// <inheritdoc/>
	public Task<int> SaveArtifactAsync(SaveArtifactRequest request)
	{
		if (request.Artifact.InlineData == null && request.Artifact.Text == null)
		{
			throw new ArgumentException("Artifact must have either InlineData or Text content.");
		}
		
		// Determine the virtual storage path based on user, session, and filename
		string artifactPath = GetArtifactPath(request.AppName, request.UserId, request.SessionId, request.Filename);
		Console.WriteLine("[InMemoryArtifactService] SAVING: " + artifactPath);
		
		// Retrieve existing versions or initialize a new list for this artifact
		List<(Part Part, ArtifactVersion Metadata)> list = _artifacts.GetOrAdd(artifactPath, _ => new List<(Part, ArtifactVersion)>());
		
		// The next version number is simply the count of existing versions (0-indexed)
		int count = list.Count;
		
		// Construct a canonical URI for reference
		string canonicalUri = $"memory://apps/{request.AppName}/users/{request.UserId}/sessions/{request.SessionId}/artifacts/{request.Filename}/versions/{count}";
		string mimeType = request.Artifact.InlineData?.MimeType ?? "text/plain";
		
		// Build the metadata object for this new version
		ArtifactVersion item = new ArtifactVersion
		{
			Version = count,
			CanonicalUri = canonicalUri,
			MimeType = mimeType,
			CustomMetadata = request.CustomMetadata
		};
		
		// Append the new version to the in-memory list
		list.Add((request.Artifact, item));
		return Task.FromResult(count);
	}

	/// <inheritdoc/>
	public Task<Part?> LoadArtifactAsync(LoadArtifactRequest request)
	{
		string artifactPath = GetArtifactPath(request.AppName, request.UserId, request.SessionId, request.Filename);
		
		// Return null if the artifact does not exist
		if (!_artifacts.TryGetValue(artifactPath, out List<(Part Part, ArtifactVersion Metadata)>? value))
		{
			return Task.FromResult<Part?>(null);
		}
		
		// Load the requested version, or default to the latest version (last element)
		int index = request.Version ?? (value.Count - 1);
		return Task.FromResult<Part?>(value[index].Part);
	}

	/// <inheritdoc/>
	public Task<List<string>> ListArtifactKeysAsync(ListArtifactKeysRequest request)
	{
		// Prefix matching for both session-scoped and user-scoped artifacts
		string sessionPrefix = $"{request.AppName}/{request.UserId}/{request.SessionId}/";
		string userPrefix = request.AppName + "/" + request.UserId + "/user/";
		
		List<string> list = new List<string>();
		
		// Scan through all keys in the dictionary
		foreach (string key in _artifacts.Keys)
		{
			// Extract filename for session-scoped artifacts
			if (key.StartsWith(sessionPrefix))
			{
				list.Add(key.Substring(sessionPrefix.Length));
			}
			// Extract filename for user-scoped artifacts and append the 'user:' prefix
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
		
		// Remove the entire list of versions associated with the artifact path
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
		
		// Generate a list of version integers based on the count
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
		
		// Extract and return just the metadata portion of all versions
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
		
		// Ensure the requested version index is within valid bounds before returning
		if (num >= 0 && num < value.Count)
		{
			return Task.FromResult<ArtifactVersion?>(value[num].Metadata);
		}
		return Task.FromResult<ArtifactVersion?>(null);
	}

	private static string GetArtifactPath(string appName, string userId, string sessionId, string filename)
	{
		// Special handling for user-level (global) artifacts vs session-level artifacts
		if (!filename.StartsWith("user:"))
		{
			return $"{appName}/{userId}/{sessionId}/{filename}";
		}
		return $"{appName}/{userId}/user/{filename}";
	}
}