using GoogleAdk.Core.Agents;
using GoogleAdk.ApiServer;
using GoogleAdk.Core.Abstractions.Artifacts;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Abstractions.Tools;
using System.Text;
using GoogleAdk.Core;

namespace GoogleAdk.Samples.ArtifactsWeb;

/// <summary>
/// Tools for the Artifacts Web Sample.
/// </summary>
public static partial class ArtifactsWebTools
{
    /// <summary>
    /// Reads the text content of a file from the artifact store.
    /// </summary>
    /// <param name="fileName">Name of the file to read (e.g., input.txt)</param>
    /// <param name="context">The agent context injected by the runtime</param>
    [FunctionTool]
    public static async Task<object?> ReadTextFile(string fileName, AgentContext context)
    {
        try
        {
            var loadedPart = await context.LoadArtifactAsync(fileName);
            var text = loadedPart?.Text;

            if (loadedPart?.InlineData != null)
            {
                var dataBytes = Convert.FromBase64String(loadedPart.InlineData.Data);
                text = Encoding.UTF8.GetString(dataBytes);
            }
            else if (loadedPart?.FileData != null)
            {
                text = $"[File Data attached from {loadedPart.FileData.FileUri}]";
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                return $"Error: Artifact '{fileName}' not found or is empty.";
            }

            return text;
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Writes text content to a new file in the artifact store.
    /// </summary>
    /// <param name="fileName">Name of the file to write (e.g., summary.txt)</param>
    /// <param name="content">The text content to save</param>
    /// <param name="context">The agent context injected by the runtime</param>
    [FunctionTool]
    public static async Task<object?> WriteTextFile(string fileName, string content, AgentContext context)
    {
        try
        {
            var dataBytes = Encoding.UTF8.GetBytes(content);
            var base64Data = Convert.ToBase64String(dataBytes);

            var part = new Part 
            { 
                InlineData = new InlineData 
                { 
                    Data = base64Data, 
                    MimeType = "text/plain",
                    DisplayName = fileName
                } 
            };

            await context.SaveArtifactAsync(fileName, part);
            
            return $"Successfully wrote {content.Length} characters to {fileName}.";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }
}

/// <summary>
/// Main program class.
/// </summary>
public class Program
{
    /// <summary>
    /// Entry point for the sample.
    /// </summary>
    public static async Task Main(string[] args)
    {
        AdkEnv.Load();
        Console.WriteLine("==> Demo: Artifacts Web Sample (Summarization)\n");

        var agent = new LlmAgent(new LlmAgentConfig
        {
            Name = "artifact_web_agent",
            Model = "gemini-2.5-flash",
            Instruction = "You are a summarization assistant. When asked to summarize, use the ReadTextFile tool to read a file (use 'input.txt' as a default if none is provided), summarize its contents concisely, and use the WriteTextFile tool to save the summary to a new file. You can choose the output filename yourself without asking the user.",
            Tools = [
                ArtifactsWebTools.ReadTextFileTool,
                ArtifactsWebTools.WriteTextFileTool
            ]
        });

        await AdkServer.RunAsync(agent);
    }
}
