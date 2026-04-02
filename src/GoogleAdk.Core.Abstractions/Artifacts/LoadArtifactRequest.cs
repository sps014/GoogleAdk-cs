namespace GoogleAdk.Core.Abstractions.Artifacts;

/// <summary>
/// Represents a request to load an artifact.
/// </summary>
public class LoadArtifactRequest
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
	/// Gets or sets the specific version to load. If null, the latest version is loaded.
	/// </summary>
	public int? Version { get; set; }
}