using System.Collections.Generic;

namespace GoogleAdk.Core.Abstractions.Artifacts;

/// <summary>
/// Represents the metadata for a specific version of an artifact.
/// </summary>
public class ArtifactVersion
{
	/// <summary>
	/// Gets or sets the version number.
	/// </summary>
	public int Version { get; set; }

	/// <summary>
	/// Gets or sets the canonical URI where the artifact content is stored.
	/// </summary>
	public string? CanonicalUri { get; set; }

	/// <summary>
	/// Gets or sets custom metadata associated with this version of the artifact.
	/// </summary>
	public Dictionary<string, object?>? CustomMetadata { get; set; }

	/// <summary>
	/// Gets or sets the MIME type of the artifact content.
	/// </summary>
	public string? MimeType { get; set; }
}