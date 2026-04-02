using System.Collections.Generic;
using GoogleAdk.Core.Abstractions.Models;

namespace GoogleAdk.Core.Abstractions.Artifacts;

/// <summary>
/// Represents a request to save an artifact.
/// </summary>
public class SaveArtifactRequest
{
	/// <summary>
	/// Gets or sets the application name.
	/// </summary>
	public string AppName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the user ID.
	/// </summary>
	public string UserId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the session ID.
	/// </summary>
	public string SessionId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the filename of the artifact.
	/// </summary>
	public string Filename { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the artifact content.
	/// </summary>
	public Part Artifact { get; set; } = null!;

	/// <summary>
	/// Gets or sets optional custom metadata associated with the artifact.
	/// </summary>
	public Dictionary<string, object?>? CustomMetadata { get; set; }
}