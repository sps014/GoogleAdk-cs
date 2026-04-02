using System.Collections.Generic;
using System.Threading.Tasks;
using GoogleAdk.Core.Abstractions.Models;

namespace GoogleAdk.Core.Abstractions.Artifacts;

/// <summary>
/// Defines the contract for an artifact service that can save, load, delete, and list artifacts.
/// Artifacts are associated with a specific app, user, and session.
/// </summary>
public interface IBaseArtifactService
{
	/// <summary>
	/// Saves an artifact and returns its new version number.
	/// </summary>
	/// <param name="request">The request containing the artifact data and metadata.</param>
	/// <returns>The newly created version number.</returns>
	Task<int> SaveArtifactAsync(SaveArtifactRequest request);

	/// <summary>
	/// Loads a specific version of an artifact, or the latest version if no version is specified.
	/// </summary>
	/// <param name="request">The request identifying the artifact and optionally the version.</param>
	/// <returns>The loaded artifact part, or null if not found.</returns>
	Task<Part?> LoadArtifactAsync(LoadArtifactRequest request);

	/// <summary>
	/// Lists all artifact keys (filenames) for a specific user and session.
	/// </summary>
	/// <param name="request">The request identifying the user and session.</param>
	/// <returns>A list of artifact keys.</returns>
	Task<List<string>> ListArtifactKeysAsync(ListArtifactKeysRequest request);

	/// <summary>
	/// Deletes an artifact and all its versions.
	/// </summary>
	/// <param name="request">The request identifying the artifact to delete.</param>
	Task DeleteArtifactAsync(DeleteArtifactRequest request);

	/// <summary>
	/// Lists all available version numbers for a specific artifact.
	/// </summary>
	/// <param name="request">The request identifying the artifact.</param>
	/// <returns>A list of version numbers.</returns>
	Task<List<int>> ListVersionsAsync(ListVersionsRequest request);

	/// <summary>
	/// Lists metadata for all versions of a specific artifact.
	/// </summary>
	/// <param name="request">The request identifying the artifact.</param>
	/// <returns>A list of artifact version metadata objects.</returns>
	Task<List<ArtifactVersion>> ListArtifactVersionsAsync(ListVersionsRequest request);

	/// <summary>
	/// Retrieves the metadata for a specific version of an artifact, or the latest if not specified.
	/// </summary>
	/// <param name="request">The request identifying the artifact and optionally the version.</param>
	/// <returns>The artifact version metadata, or null if not found.</returns>
	Task<ArtifactVersion?> GetArtifactVersionAsync(LoadArtifactRequest request);
}