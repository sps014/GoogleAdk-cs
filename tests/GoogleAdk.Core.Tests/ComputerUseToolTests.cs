using System.Collections.Generic;
using System.Threading.Tasks;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Tools;
using Xunit;

namespace GoogleAdk.Core.Tests;

public class ComputerUseToolTests
{
    private class MockComputerDriver : IComputerDriver
    {
        public Task PrepareAsync(AgentContext toolContext) => Task.CompletedTask;

        public Task<(int Width, int Height)> ScreenSizeAsync() => Task.FromResult((1920, 1080));

        public Task<ComputerState> OpenWebBrowserAsync() => Task.FromResult(new ComputerState());

        public Task<ComputerState> ClickAtAsync(int x, int y) 
            => Task.FromResult(new ComputerState { Url = $"clicked_{x}_{y}" });

        public Task<ComputerState> HoverAtAsync(int x, int y) 
            => Task.FromResult(new ComputerState { Url = $"hovered_{x}_{y}" });

        public Task<ComputerState> TypeTextAtAsync(int x, int y, string text, bool pressEnter = true, bool clearBeforeTyping = true) 
            => Task.FromResult(new ComputerState { Url = $"typed_{text}_at_{x}_{y}" });

        public Task<ComputerState> ScrollDocumentAsync(string direction) 
            => Task.FromResult(new ComputerState { Url = $"scrolled_{direction}" });

        public Task<ComputerState> ScrollAtAsync(int x, int y, string direction, int magnitude) 
            => Task.FromResult(new ComputerState { Url = $"scrolled_{direction}_{magnitude}_at_{x}_{y}" });

        public Task<ComputerState> WaitAsync(int seconds) 
            => Task.FromResult(new ComputerState { Url = $"waited_{seconds}" });

        public Task<ComputerState> GoBackAsync() => Task.FromResult(new ComputerState { Url = "went_back" });

        public Task<ComputerState> GoForwardAsync() => Task.FromResult(new ComputerState { Url = "went_forward" });

        public Task<ComputerState> SearchAsync() => Task.FromResult(new ComputerState { Url = "search" });

        public Task<ComputerState> NavigateAsync(string url) => Task.FromResult(new ComputerState { Url = $"navigated_{url}" });

        public Task<ComputerState> KeyCombinationAsync(List<string> keys) => Task.FromResult(new ComputerState { Url = "keys" });

        public Task<ComputerState> DragAndDropAsync(int x, int y, int destinationX, int destinationY) 
            => Task.FromResult(new ComputerState { Url = $"dragged_{x}_{y}_to_{destinationX}_{destinationY}" });

        public Task<ComputerState> CurrentStateAsync() => Task.FromResult(new ComputerState());

        public Task InitializeAsync() => Task.CompletedTask;

        public Task CloseAsync() => Task.CompletedTask;

        public Task<ComputerEnvironment> EnvironmentAsync() => Task.FromResult(ComputerEnvironment.EnvironmentUnspecified);
    }

    private static AgentContext CreateToolContext()
    {
        var invCtx = new InvocationContext();
        return new AgentContext(invCtx);
    }

    [Fact]
    public async Task CoordinateNormalization_WorksCorrectly()
    {
        var driver = new MockComputerDriver();
        // Virtual screen is 1000x1000, actual is 1920x1080 (set when first called, or we can manually set actual to test Normalize without Run)
        var tool = new ComputerUseTool(driver, (1000, 1000));
        
        // We must run it once so it fetches screen size
        await tool.RunAsync(new Dictionary<string, object?> { ["action"] = "wait", ["magnitude"] = 0 }, CreateToolContext());

        // 500 / 1000 * 1920 = 960
        Assert.Equal(960, tool.NormalizeX(500));
        
        // 500 / 1000 * 1080 = 540
        Assert.Equal(540, tool.NormalizeY(500));

        // Clamping check
        Assert.Equal(1919, tool.NormalizeX(2000));
        Assert.Equal(0, tool.NormalizeX(-100));
    }

    [Fact]
    public async Task RunAsync_Click_NormalizesCoordinates()
    {
        var driver = new MockComputerDriver();
        var tool = new ComputerUseTool(driver, (1000, 1000));

        var result = await tool.RunAsync(new Dictionary<string, object?>
        {
            ["action"] = "left_click",
            ["x"] = 500,
            ["y"] = 500
        }, CreateToolContext());

        var props = result?.GetType().GetProperty("url");
        var url = props?.GetValue(result) as string;
        
        // 500 -> 960, 540
        Assert.Equal("clicked_960_540", url);
    }

    [Fact]
    public async Task RunAsync_DragAndDrop_NormalizesCoordinates()
    {
        var driver = new MockComputerDriver();
        var tool = new ComputerUseTool(driver, (1000, 1000));

        var result = await tool.RunAsync(new Dictionary<string, object?>
        {
            ["action"] = "drag_and_drop",
            ["x"] = 0,
            ["y"] = 0,
            ["destination_x"] = 1000,
            ["destination_y"] = 1000
        }, CreateToolContext());

        var props = result?.GetType().GetProperty("url");
        var url = props?.GetValue(result) as string;
        
        // 0 -> 0,0 and 1000 -> 1919, 1079 (since Math.Min(normalized, 1080 - 1))
        Assert.Equal("dragged_0_0_to_1919_1079", url);
    }
}
