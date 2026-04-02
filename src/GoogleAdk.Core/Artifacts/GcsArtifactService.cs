using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.Cloud.Storage.V1;
using GoogleAdk.Core.Abstractions.Artifacts;
using GoogleAdk.Core.Abstractions.Models;

namespace GoogleAdk.Core.Artifacts;

/// <summary>
/// A Google Cloud Storage (GCS) based implementation of the artifact service.
/// </summary>
public sealed class GcsArtifactService : IBaseArtifactService
{
	private readonly StorageClient _client;
	private readonly string _bucket;

	/// <summary>
	/// Initializes a new instance of the <see cref="GcsArtifactService"/> class.
	/// </summary>
	/// <param name="bucketName">The name of the GCS bucket.</param>
	/// <param name="client">The optional GCS storage client.</param>
	public GcsArtifactService(string bucketName, StorageClient? client = null)
	{
		_bucket = bucketName;
		_client = client ?? StorageClient.Create();
	}

	/// <inheritdoc/>
	public async Task<int> SaveArtifactAsync(SaveArtifactRequest request)
	{
		if (request.Artifact.InlineData == null && request.Artifact.Text == null)
		{
			throw new ArgumentException("Artifact must have either InlineData or Text content.");
		}
		string prefix = GetArtifactPrefix(request.AppName, request.UserId, request.SessionId, request.Filename);
		List<int> versions = await ListVersionsInternalAsync(prefix);
		int nextVersion = versions.Count > 0 ? versions[versions.Count - 1] + 1 : 0;

		string objectName = $"{prefix}{nextVersion}";
		string contentType = "text/plain";
		byte[] data;
		if (request.Artifact.InlineData != null)
		{
			data = Convert.FromBase64String(request.Artifact.InlineData.Data ?? string.Empty);
			contentType = request.Artifact.InlineData.MimeType ?? "application/octet-stream";
		}
		else
		{
			data = Encoding.UTF8.GetBytes(request.Artifact.Text ?? string.Empty);
		}
		using MemoryStream stream = new MemoryStream(data);
		Google.Apis.Storage.v1.Data.Object obj = await _client.UploadObjectAsync(_bucket, objectName, contentType, stream);
		
		if (request.CustomMetadata != null && request.CustomMetadata.Count > 0)
		{
			obj.Metadata = request.CustomMetadata.ToDictionary(k => k.Key, v => v.Value?.ToString() ?? string.Empty);
			await _client.UpdateObjectAsync(obj);
		}
		return nextVersion;
	}

	/// <inheritdoc/>
	public async Task<Part?> LoadArtifactAsync(LoadArtifactRequest request)
	{
		string prefix = GetArtifactPrefix(request.AppName, request.UserId, request.SessionId, request.Filename);
		int? version = request.Version ?? (await GetLatestVersionAsync(prefix));
		if (!version.HasValue) return null;

		string objectName = $"{prefix}{version}";
		Google.Apis.Storage.v1.Data.Object? obj = await SafeGetObjectAsync(objectName);
		if (obj == null) return null;

		using MemoryStream stream = new MemoryStream();
		await _client.DownloadObjectAsync(obj, stream);
		byte[] bytes = stream.ToArray();
		if (!string.IsNullOrEmpty(obj.ContentType) && !obj.ContentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
		{
			return new Part
			{
				InlineData = new InlineData
				{
					MimeType = obj.ContentType,
					Data = Convert.ToBase64String(bytes)
				}
			};
		}
		return new Part
		{
			Text = Encoding.UTF8.GetString(bytes)
		};
	}

	/// <inheritdoc/>
	public async Task<List<string>> ListArtifactKeysAsync(ListArtifactKeysRequest request)
	{
		HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
		string sessionPrefix = $"{request.AppName}/{request.UserId}/{request.SessionId}/";
		foreach (Google.Apis.Storage.v1.Data.Object obj in _client.ListObjects(_bucket, sessionPrefix))
		{
			string name = obj.Name;
			string rest = name.Substring(sessionPrefix.Length);
			string[] parts = rest.Split('/', StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length > 1) keys.Add(parts[0]);
		}
		string userPrefix = $"{request.AppName}/{request.UserId}/user/";
		foreach (Google.Apis.Storage.v1.Data.Object obj in _client.ListObjects(_bucket, userPrefix))
		{
			string name = obj.Name;
			string rest = name.Substring(userPrefix.Length);
			string[] parts = rest.Split('/', StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length > 1) keys.Add("user:" + parts[0]);
		}
		return keys.OrderBy(k => k).ToList();
	}

	/// <inheritdoc/>
	public async Task DeleteArtifactAsync(DeleteArtifactRequest request)
	{
		string prefix = GetArtifactPrefix(request.AppName, request.UserId, request.SessionId, request.Filename);
		foreach (Google.Apis.Storage.v1.Data.Object obj in _client.ListObjects(_bucket, prefix))
		{
			await _client.DeleteObjectAsync(obj);
		}
	}

	/// <inheritdoc/>
	public Task<List<int>> ListVersionsAsync(ListVersionsRequest request)
	{
		string artifactPrefix = GetArtifactPrefix(request.AppName, request.UserId, request.SessionId, request.Filename);
		return ListVersionsInternalAsync(artifactPrefix);
	}

	/// <inheritdoc/>
	public async Task<List<ArtifactVersion>> ListArtifactVersionsAsync(ListVersionsRequest request)
	{
		string prefix = GetArtifactPrefix(request.AppName, request.UserId, request.SessionId, request.Filename);
		List<ArtifactVersion> versions = new List<ArtifactVersion>();
		foreach (Google.Apis.Storage.v1.Data.Object obj in _client.ListObjects(_bucket, prefix))
		{
			if (TryParseVersion(obj.Name, out int version))
			{
				versions.Add(new ArtifactVersion
				{
					Version = version,
					CanonicalUri = $"gs://{_bucket}/{obj.Name}",
					CustomMetadata = obj.Metadata?.ToDictionary(k => k.Key, v => (object?)v.Value),
					MimeType = obj.ContentType
				});
			}
		}
		return versions.OrderBy(v => v.Version).ToList();
	}

	/// <inheritdoc/>
	public async Task<ArtifactVersion?> GetArtifactVersionAsync(LoadArtifactRequest request)
	{
		string prefix = GetArtifactPrefix(request.AppName, request.UserId, request.SessionId, request.Filename);
		int? version = request.Version ?? (await GetLatestVersionAsync(prefix));
		if (!version.HasValue) return null;

		Google.Apis.Storage.v1.Data.Object? obj = await SafeGetObjectAsync($"{prefix}{version}");
		if (obj == null) return null;

		return new ArtifactVersion
		{
			Version = version.Value,
			CanonicalUri = $"gs://{_bucket}/{obj.Name}",
			CustomMetadata = obj.Metadata?.ToDictionary(k => k.Key, v => (object?)v.Value),
			MimeType = obj.ContentType
		};
	}

	private static string GetArtifactPrefix(string appName, string userId, string sessionId, string filename)
	{
		string value = filename.StartsWith("user:") ? filename.Substring(5) : filename;
		if (!filename.StartsWith("user:"))
		{
			return $"{appName}/{userId}/{sessionId}/{value}/";
		}
		return $"{appName}/{userId}/user/{value}/";
	}

	private async Task<List<int>> ListVersionsInternalAsync(string prefix)
	{
		List<int> versions = new List<int>();
		foreach (Google.Apis.Storage.v1.Data.Object obj in _client.ListObjects(_bucket, prefix))
		{
			if (TryParseVersion(obj.Name, out int version)) versions.Add(version);
		}
		versions.Sort();
		return await Task.FromResult(versions);
	}

	private async Task<int?> GetLatestVersionAsync(string prefix)
	{
		List<int> versions = await ListVersionsInternalAsync(prefix);
		return versions.Count > 0 ? versions[versions.Count - 1] : null;
	}

	private bool TryParseVersion(string objectName, out int version)
	{
		version = 0;
		string? text = objectName.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
		return text != null && int.TryParse(text, out version);
	}

	private async Task<Google.Apis.Storage.v1.Data.Object?> SafeGetObjectAsync(string objectName)
	{
		try
		{
			return await _client.GetObjectAsync(_bucket, objectName);
		}
		catch
		{
			return null;
		}
	}
}