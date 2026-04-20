using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Tools;
using Microsoft.Playwright;
using System.Text.RegularExpressions;

namespace GoogleAdk.Samples.ComputerUse.Drivers;

public sealed class PlaywrightComputerDriver : IComputerDriver, IAsyncDisposable
{
    private const int ScreenWidth = 1920;
    private const int ScreenHeight = 1080;
    
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IPage? _page;

    public Task PrepareAsync(AgentContext toolContext) => Task.CompletedTask;

    public Task<(int Width, int Height)> ScreenSizeAsync() => Task.FromResult((ScreenWidth, ScreenHeight));

    public async Task InitializeAsync()
    {
        Log("Initialize Playwright");
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        _page = await _browser.NewPageAsync(new BrowserNewPageOptions 
        { 
            ViewportSize = new ViewportSize { Width = ScreenWidth, Height = ScreenHeight } 
        });
    }

    public async Task CloseAsync()
    {
        Log("Close Playwright");
        if (_browser != null) await _browser.CloseAsync();
        _playwright?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync();
    }

    public Task<ComputerEnvironment> EnvironmentAsync() => Task.FromResult(ComputerEnvironment.EnvironmentBrowser);

    public async Task<ComputerState> OpenWebBrowserAsync()
    {
        Log("Open browser");
        if (_page == null) await InitializeAsync();
        await _page!.GotoAsync("about:blank");
        return await CreateStateAsync();
    }

    public async Task<ComputerState> ClickAtAsync(int x, int y)
    {
        Log($"Click at ({x}, {y})");
        if (_page != null) await _page.Mouse.ClickAsync(x, y);
        return await CreateStateAsync();
    }

    public async Task<ComputerState> HoverAtAsync(int x, int y)
    {
        Log($"Hover at ({x}, {y})");
        if (_page != null) await _page.Mouse.MoveAsync(x, y);
        return await CreateStateAsync();
    }

    public async Task<ComputerState> TypeTextAtAsync(int x, int y, string text, bool pressEnter = true, bool clearBeforeTyping = true)
    {
        Log($"Type '{text}' at ({x}, {y})");
        if (_page != null)
        {
            await _page.Mouse.ClickAsync(x, y, new MouseClickOptions { ClickCount = clearBeforeTyping ? 3 : 1 });
            if (clearBeforeTyping) await _page.Keyboard.PressAsync("Backspace");
            await _page.Keyboard.TypeAsync(text);
            if (pressEnter) await _page.Keyboard.PressAsync("Enter");
        }
        return await CreateStateAsync();
    }

    public async Task<ComputerState> ScrollDocumentAsync(string direction)
    {
        Log($"Scroll document {direction}");
        if (_page != null)
        {
            if (direction.Equals("up", StringComparison.OrdinalIgnoreCase))
                await _page.Mouse.WheelAsync(0, -500);
            else if (direction.Equals("down", StringComparison.OrdinalIgnoreCase))
                await _page.Mouse.WheelAsync(0, 500);
        }
        return await CreateStateAsync();
    }

    public async Task<ComputerState> ScrollAtAsync(int x, int y, string direction, int magnitude)
    {
        Log($"Scroll {direction} {magnitude} at ({x}, {y})");
        if (_page != null)
        {
            await _page.Mouse.MoveAsync(x, y);
            if (direction.Equals("up", StringComparison.OrdinalIgnoreCase))
                await _page.Mouse.WheelAsync(0, -magnitude);
            else if (direction.Equals("down", StringComparison.OrdinalIgnoreCase))
                await _page.Mouse.WheelAsync(0, magnitude);
        }
        return await CreateStateAsync();
    }

    public async Task<ComputerState> WaitAsync(int seconds)
    {
        Log($"Wait {seconds}s");
        await Task.Delay(seconds * 1000);
        return await CreateStateAsync();
    }

    public async Task<ComputerState> GoBackAsync()
    {
        Log("Go back");
        if (_page != null) await _page.GoBackAsync();
        return await CreateStateAsync();
    }

    public async Task<ComputerState> GoForwardAsync()
    {
        Log("Go forward");
        if (_page != null) await _page.GoForwardAsync();
        return await CreateStateAsync();
    }

    public async Task<ComputerState> SearchAsync()
    {
        Log("Open search page");
        if (_page != null) await _page.GotoAsync("https://www.google.com");
        return await CreateStateAsync();
    }

    public async Task<ComputerState> NavigateAsync(string url)
    {
        Log($"Navigate to {url}");
        if (_page != null) await _page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        return await CreateStateAsync();
    }

    public async Task<ComputerState> KeyCombinationAsync(List<string> keys)
    {
        Log($"Key combo: {string.Join("+", keys)}");
        if (_page != null) await _page.Keyboard.PressAsync(string.Join("+", keys));
        return await CreateStateAsync();
    }

    public async Task<ComputerState> DragAndDropAsync(int x, int y, int destinationX, int destinationY)
    {
        Log($"Drag ({x}, {y}) to ({destinationX}, {destinationY})");
        if (_page != null)
        {
            await _page.Mouse.MoveAsync(x, y);
            await _page.Mouse.DownAsync();
            await _page.Mouse.MoveAsync(destinationX, destinationY, new MouseMoveOptions { Steps = 10 });
            await _page.Mouse.UpAsync();
        }
        return await CreateStateAsync();
    }

    public async Task<ComputerState> CurrentStateAsync() => await CreateStateAsync();

    private static void Log(string message) => Console.WriteLine($"[Driver] {message}");

    private async Task<ComputerState> CreateStateAsync()
    {
        string currentUrl = "about:blank";
        byte[]? screenshot = null;
        string? textContent = null;

        if (_page != null)
        {
            currentUrl = _page.Url;
            
            try
            {
                screenshot = await _page.ScreenshotAsync(new PageScreenshotOptions { Type = ScreenshotType.Png });
                System.IO.File.WriteAllBytes("screenshot.png", screenshot);
            }
            catch (Exception ex)
            {
                Log($"Failed to take screenshot: {ex.Message}");
            }

            try
            {
                var content = await _page.ContentAsync();
                var text = Regex.Replace(content, "<.*?>", " ");
                text = Regex.Replace(text, @"\s+", " ").Trim();
                textContent = text.Length > 800 ? text[..800] + "..." : text;
            }
            catch (Exception ex)
            {
                 Log($"Failed to extract text: {ex.Message}");
            }
        }

        return new ComputerState
        {
            Url = currentUrl,
            Screenshot = screenshot,
            TextContent = textContent
        };
    }
}