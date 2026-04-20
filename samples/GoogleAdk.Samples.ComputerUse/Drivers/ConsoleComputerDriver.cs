using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Tools;

namespace GoogleAdk.Samples.ComputerUse.Drivers;

public sealed class ConsoleComputerDriver : IComputerDriver
{
    private const int ScreenWidth = 1920;
    private const int ScreenHeight = 1080;
    private string? _currentUrl = "about:blank";
    private string? _textContent = null;
    private static readonly HttpClient _httpClient = new();

    public Task PrepareAsync(AgentContext toolContext) => Task.CompletedTask;

    public Task<(int Width, int Height)> ScreenSizeAsync() => Task.FromResult((ScreenWidth, ScreenHeight));

    public Task InitializeAsync()
    {
        Log("Initialize");
        return Task.CompletedTask;
    }

    public Task CloseAsync()
    {
        Log("Close");
        return Task.CompletedTask;
    }

    public Task<ComputerEnvironment> EnvironmentAsync() => Task.FromResult(ComputerEnvironment.EnvironmentBrowser);

    public Task<ComputerState> OpenWebBrowserAsync()
    {
        _currentUrl = "about:blank";
        Log("Open browser");
        return Task.FromResult(CreateState());
    }

    public Task<ComputerState> ClickAtAsync(int x, int y)
    {
        Log($"Click at ({x}, {y})");
        return Task.FromResult(CreateState());
    }

    public Task<ComputerState> HoverAtAsync(int x, int y)
    {
        Log($"Hover at ({x}, {y})");
        return Task.FromResult(CreateState());
    }

    public Task<ComputerState> TypeTextAtAsync(int x, int y, string text, bool pressEnter = true, bool clearBeforeTyping = true)
    {
        Log($"Type '{text}' at ({x}, {y})");
        return Task.FromResult(CreateState());
    }

    public Task<ComputerState> ScrollDocumentAsync(string direction)
    {
        Log($"Scroll document {direction}");
        return Task.FromResult(CreateState());
    }

    public Task<ComputerState> ScrollAtAsync(int x, int y, string direction, int magnitude)
    {
        Log($"Scroll {direction} {magnitude} at ({x}, {y})");
        return Task.FromResult(CreateState());
    }

    public Task<ComputerState> WaitAsync(int seconds)
    {
        Log($"Wait {seconds}s");
        return Task.FromResult(CreateState());
    }

    public Task<ComputerState> GoBackAsync()
    {
        Log("Go back");
        return Task.FromResult(CreateState());
    }

    public Task<ComputerState> GoForwardAsync()
    {
        Log("Go forward");
        return Task.FromResult(CreateState());
    }

    public Task<ComputerState> SearchAsync()
    {
        _currentUrl = "https://www.google.com";
        Log("Open search page");
        return Task.FromResult(CreateState());
    }

    public async Task<ComputerState> NavigateAsync(string url)
    {
        _currentUrl = url;
        Log($"Navigate to {url}");
        try
        {
            if (url.StartsWith("http"))
            {
                var content = await _httpClient.GetStringAsync(url);
                // Strip HTML tags roughly to make it readable text for the LLM
                var text = System.Text.RegularExpressions.Regex.Replace(content, "<.*?>", " ");
                text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
                _textContent = text.Length > 800 ? text[..800] + "..." : text;
                Log($"Fetched content snippet: {_textContent[..Math.Min(50, _textContent.Length)]}...");
            }
            else
            {
                _textContent = null;
            }
        }
        catch (Exception ex)
        {
            _textContent = $"[Error fetching page: {ex.Message}]";
            Log(_textContent);
        }
        return CreateState();
    }

    public Task<ComputerState> KeyCombinationAsync(List<string> keys)
    {
        Log($"Key combo: {string.Join("+", keys)}");
        return Task.FromResult(CreateState());
    }

    public Task<ComputerState> DragAndDropAsync(int x, int y, int destinationX, int destinationY)
    {
        Log($"Drag ({x}, {y}) to ({destinationX}, {destinationY})");
        return Task.FromResult(CreateState());
    }

    public Task<ComputerState> CurrentStateAsync() => Task.FromResult(CreateState());

    private static void Log(string message) => Console.WriteLine($"[Driver] {message}");

    private ComputerState CreateState()
    {
        return new ComputerState
        {
            Url = _currentUrl,
            Screenshot = null,
            TextContent = _textContent
        };
    }
}
