namespace GoogleAdk.Core.Abstractions.Artifacts;

/// <summary>
/// Represents a request to list the versions of a specific artifact.
/// </summary>
public class ListVersionsRequest
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
}