using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GoogleAdk.Core.Agents;

namespace GoogleAdk.Core.Tools;

public enum ComputerEnvironment
{
    EnvironmentUnspecified,
    EnvironmentBrowser
}

public class ComputerState
{
    public byte[]? Screenshot { get; set; }
    public string? Url { get; set; }
    public string? TextContent { get; set; }
}

/// <summary>
/// Defines an interface for computer environments.
/// This interface defines the standard operations for controlling computer environments,
/// including web browsers and other interactive systems.
/// </summary>
public interface IComputerDriver
{
    Task PrepareAsync(AgentContext toolContext);

    /// <summary>
    /// Returns the screen size of the environment.
    /// </summary>
    /// <returns>A tuple of (width, height) in pixels.</returns>
    Task<(int Width, int Height)> ScreenSizeAsync();

    /// <summary>
    /// Opens the web browser.
    /// </summary>
    Task<ComputerState> OpenWebBrowserAsync();

    /// <summary>
    /// Clicks at a specific x, y coordinate.
    /// </summary>
    Task<ComputerState> ClickAtAsync(int x, int y);

    /// <summary>
    /// Hovers at a specific x, y coordinate.
    /// </summary>
    Task<ComputerState> HoverAtAsync(int x, int y);

    /// <summary>
    /// Types text at a specific x, y coordinate.
    /// </summary>
    Task<ComputerState> TypeTextAtAsync(int x, int y, string text, bool pressEnter = true, bool clearBeforeTyping = true);

    /// <summary>
    /// Scrolls the entire document.
    /// </summary>
    Task<ComputerState> ScrollDocumentAsync(string direction);

    /// <summary>
    /// Scrolls at a specific x, y coordinate by magnitude.
    /// </summary>
    Task<ComputerState> ScrollAtAsync(int x, int y, string direction, int magnitude);

    /// <summary>
    /// Waits for n seconds.
    /// </summary>
    Task<ComputerState> WaitAsync(int seconds);

    /// <summary>
    /// Navigates back.
    /// </summary>
    Task<ComputerState> GoBackAsync();

    /// <summary>
    /// Navigates forward.
    /// </summary>
    Task<ComputerState> GoForwardAsync();

    /// <summary>
    /// Directly jumps to a search engine home page.
    /// </summary>
    Task<ComputerState> SearchAsync();

    /// <summary>
    /// Navigates directly to a specified URL.
    /// </summary>
    Task<ComputerState> NavigateAsync(string url);

    /// <summary>
    /// Presses keyboard keys and combinations.
    /// </summary>
    Task<ComputerState> KeyCombinationAsync(List<string> keys);

    /// <summary>
    /// Drag and drop from one coordinate to another.
    /// </summary>
    Task<ComputerState> DragAndDropAsync(int x, int y, int destinationX, int destinationY);

    /// <summary>
    /// Returns the current state.
    /// </summary>
    Task<ComputerState> CurrentStateAsync();

    /// <summary>
    /// Initialize the computer.
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Cleanup resource of the computer.
    /// </summary>
    Task CloseAsync();

    /// <summary>
    /// Returns the environment of the computer.
    /// </summary>
    Task<ComputerEnvironment> EnvironmentAsync();
}
