using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Agents;

namespace GoogleAdk.Core.Tools;

/// <summary>
/// A tool that wraps computer control functions for use with LLMs.
/// Automatically normalizes coordinates from a virtual coordinate space
/// (by default 1000x1000) to the actual screen size.
/// </summary>
public sealed class ComputerUseTool : BaseTool
{
    private readonly IComputerDriver _driver;
    private readonly (int Width, int Height) _virtualScreenSize;
    private (int Width, int Height)? _actualScreenSize;

    public ComputerUseTool(IComputerDriver driver, (int Width, int Height)? virtualScreenSize = null)
        : base("computer_use", "Use a computer to automate tasks like clicking, typing, and scrolling.")
    {
        _driver = driver;
        _virtualScreenSize = virtualScreenSize ?? (1000, 1000);
    }

    public override FunctionDeclaration? GetDeclaration()
    {
        return new FunctionDeclaration
        {
            Name = Name,
            Description = Description,
            Parameters = new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?>
                {
                    ["action"] = new Dictionary<string, object?>
                    {
                        ["type"] = "string",
                        ["description"] = "The action to perform: left_click, type, mouse_move, scroll, wait, go_back, go_forward, search, navigate, drag_and_drop",
                        ["enum"] = new[] { "left_click", "type", "mouse_move", "scroll", "wait", "go_back", "go_forward", "search", "navigate", "drag_and_drop" }
                    },
                    ["x"] = new Dictionary<string, object?> { ["type"] = "integer", ["description"] = "Virtual X coordinate" },
                    ["y"] = new Dictionary<string, object?> { ["type"] = "integer", ["description"] = "Virtual Y coordinate" },
                    ["destination_x"] = new Dictionary<string, object?> { ["type"] = "integer", ["description"] = "Destination virtual X coordinate for drag" },
                    ["destination_y"] = new Dictionary<string, object?> { ["type"] = "integer", ["description"] = "Destination virtual Y coordinate for drag" },
                    ["text"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Text to type or URL to navigate to" },
                    ["direction"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Direction to scroll: up, down, left, right", ["enum"] = new[] { "up", "down", "left", "right" } },
                    ["magnitude"] = new Dictionary<string, object?> { ["type"] = "integer", ["description"] = "Amount to scroll or wait" }
                },
                ["required"] = new[] { "action" }
            }
        };
    }

    public override async Task<object?> RunAsync(Dictionary<string, object?> args, AgentContext context)
    {
        await _driver.PrepareAsync(context);

        if (_actualScreenSize == null)
        {
            _actualScreenSize = await _driver.ScreenSizeAsync();
        }

        var action = args.TryGetValue("action", out var actionObj) ? FunctionToolArgs.Get<string>(actionObj) : null;
        if (string.IsNullOrEmpty(action))
            return new Dictionary<string, object?> { ["error"] = "action is required." };

        int? x = args.TryGetValue("x", out var xObj) ? FunctionToolArgs.Get<int>(xObj) : null;
        int? y = args.TryGetValue("y", out var yObj) ? FunctionToolArgs.Get<int>(yObj) : null;

        if (x.HasValue) x = NormalizeX(x.Value);
        if (y.HasValue) y = NormalizeY(y.Value);

        try
        {
            ComputerState state = new ComputerState();
            switch (action)
            {
                case "left_click":
                    if (x == null || y == null) return new { error = "x and y are required for left_click." };
                    state = await _driver.ClickAtAsync(x.Value, y.Value);
                    break;
                case "mouse_move":
                    if (x == null || y == null) return new { error = "x and y are required for mouse_move." };
                    state = await _driver.HoverAtAsync(x.Value, y.Value);
                    break;
                case "type":
                    if (x == null || y == null) return new { error = "x and y are required for type." };
                    var text = args.TryGetValue("text", out var textObj) ? FunctionToolArgs.Get<string>(textObj) : "";
                    state = await _driver.TypeTextAtAsync(x.Value, y.Value, text);
                    break;
                case "scroll":
                    var direction = args.TryGetValue("direction", out var dirObj) ? FunctionToolArgs.Get<string>(dirObj) : "down";
                    var magnitude = args.TryGetValue("magnitude", out var magObj) ? FunctionToolArgs.Get<int>(magObj) : 10;
                    if (x.HasValue && y.HasValue)
                        state = await _driver.ScrollAtAsync(x.Value, y.Value, direction, magnitude);
                    else
                        state = await _driver.ScrollDocumentAsync(direction);
                    break;
                case "wait":
                    var seconds = args.TryGetValue("magnitude", out var secObj) ? FunctionToolArgs.Get<int>(secObj) : 2;
                    state = await _driver.WaitAsync(seconds);
                    break;
                case "go_back":
                    state = await _driver.GoBackAsync();
                    break;
                case "go_forward":
                    state = await _driver.GoForwardAsync();
                    break;
                case "search":
                    state = await _driver.SearchAsync();
                    break;
                case "navigate":
                    var url = args.TryGetValue("text", out var urlObj) ? FunctionToolArgs.Get<string>(urlObj) : "";
                    state = await _driver.NavigateAsync(url);
                    break;
                case "drag_and_drop":
                    if (x == null || y == null) return new { error = "x and y are required for drag_and_drop." };
                    int? destX = args.TryGetValue("destination_x", out var dxObj) ? FunctionToolArgs.Get<int>(dxObj) : null;
                    int? destY = args.TryGetValue("destination_y", out var dyObj) ? FunctionToolArgs.Get<int>(dyObj) : null;
                    if (destX == null || destY == null) return new { error = "destination_x and destination_y are required for drag_and_drop." };
                    state = await _driver.DragAndDropAsync(x.Value, y.Value, NormalizeX(destX.Value), NormalizeY(destY.Value));
                    break;
                default:
                    return new { error = $"Unknown action: {action}" };
            }

            return new
            {
                url = state.Url,
                screenshot_status = state.Screenshot != null ? "Saved to disk locally (omitted from LLM context to save tokens)" : "No screenshot",
                textContent = state.TextContent
            };
        }
        catch (Exception ex)
        {
            return new { error = ex.Message };
        }
    }

    public int NormalizeX(int x)
    {
        if (_actualScreenSize == null) return x;
        var normalized = (int)((double)x / _virtualScreenSize.Width * _actualScreenSize.Value.Width);
        return Math.Max(0, Math.Min(normalized, _actualScreenSize.Value.Width - 1));
    }

    public int NormalizeY(int y)
    {
        if (_actualScreenSize == null) return y;
        var normalized = (int)((double)y / _virtualScreenSize.Height * _actualScreenSize.Value.Height);
        return Math.Max(0, Math.Min(normalized, _actualScreenSize.Value.Height - 1));
    }
}
