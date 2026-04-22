using GoogleAdk.Core;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Skills;
using GoogleAdk.Core.Tools;
using GoogleAdk.Core.Runner;
using GoogleAdk.Models.Gemini;

Console.WriteLine("=== Skills Sample (Gemini 2.5 Flash) ===\n");

AdkEnv.Load();

        // 1. Load the skill from the .skill folder
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        // Search upward to find the .skill folder since execution directory might be in bin/Debug
        var currentDir = new DirectoryInfo(baseDir);
        string? skillPath = null;
        
        while (currentDir != null)
        {
            var testPath = Path.Combine(currentDir.FullName, ".skill");
            if (Directory.Exists(testPath))
            {
                skillPath = testPath;
                break;
            }
            currentDir = currentDir.Parent;
        }

        if (skillPath == null)
        {
            Console.WriteLine("Could not find the .skill directory.");
            return;
        }

        var skill = SkillLoader.LoadFromDirectory(skillPath);

        // 2. Initialize the toolset
var toolset = new SkillToolset(new[] { skill });

        // 3. Create the Agent
        var agent = new LlmAgent(new LlmAgentConfig
        {
            Name = "SkillAgent",
            Model = "gemini-2.5-flash",
            Instruction = "You are a helpful assistant equipped with agent skills.",
            Tools = [toolset]
        });

Console.WriteLine("Agent configured with 'hello-world-skill'.\n");
Console.WriteLine("Example prompts:");
Console.WriteLine("- \"Use the hello-world-skill to greet me.\"");
Console.WriteLine("- \"What skills do you have available?\"\n");

await ConsoleRunner.RunAsync(agent, cfg =>
{
    cfg.FigletText = "Skills";
    cfg.DebugMode = true;
    cfg.EnableStreaming = true;
});

Console.WriteLine("\n=== Skills Sample Complete ===");