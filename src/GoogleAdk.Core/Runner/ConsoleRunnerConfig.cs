namespace GoogleAdk.Core.Runner;

/// <summary>
/// Configuration for the ConsoleRunner.
/// </summary>
public class ConsoleRunnerConfig : RunnerConfig
{
    /// <summary>
    /// Gets or sets whether debug mode is enabled.
    /// When true, displays tool calls, results, and subagent transfers.
    /// Defaults to true.
    /// </summary>
    public bool DebugMode { get; set; } = true;

    /// <summary>
    /// Gets or sets whether streaming mode is enabled.
    /// When true, text output streams chunk by chunk.
    /// Defaults to false.
    /// </summary>
    public bool EnableStreaming { get; set; } = false;

    /// <summary>
    /// Gets or sets the text to display in the figlet logo.
    /// Defaults to "Google ADK".
    /// </summary>
    public string FigletText { get; set; } = "Google ADK";

    /// <summary>
    /// Gets or sets the initial message to send to the agent automatically on startup.
    /// If null, the runner waits for user input.
    /// </summary>
    public string? InitialMessage { get; set; } = null;

    /// <summary>
    /// Gets or sets whether the runner should automatically close after finishing the first turn.
    /// </summary>
    public bool CloseOnFinish { get; set; } = false;
}
