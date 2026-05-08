using System.Text;

namespace GoogleAdk.Core.Runner;

internal class ConsoleRenderState 
{
    public string? StreamingAuthor { get; set; }
    public StringBuilder TextBuffer { get; } = new();
    public string? StreamingThinkingAuthor { get; set; }
    public StringBuilder ThinkingBuffer { get; } = new();
    public string CurrentAgent { get; set; } = string.Empty;
}
