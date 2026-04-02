namespace GoogleAdk.Core.Abstractions.Artifacts;

/// <summary>
/// Represents a request to list all artifact keys for a user and session.
/// </summary>
public class ListArtifactKeysRequest
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
}