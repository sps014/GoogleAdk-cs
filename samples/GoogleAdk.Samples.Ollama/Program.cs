using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Runner;
using GoogleAdk.Dev;
using GoogleAdk.Models.Meai;
using Microsoft.Extensions.AI;

Console.WriteLine("==> Demo: Ollama Integration\n");
Console.WriteLine("Ensure Ollama is running locally and you have pulled a model (e.g., 'ollama run llama3.2').");
Console.WriteLine("The endpoint defaults to http://localhost:11434");
Console.WriteLine();

// 1. Create the Microsoft.Extensions.AI chat client for Ollama
// Adjust the model name to whichever model you have pulled in Ollama
string modelName = "llama3.2"; // Or "llama3.1", "mistral", "phi3", etc.
IChatClient ollamaClient = new OllamaChatClient(new Uri("http://localhost:11434"), modelName);

// 2. Wrap it in the ADK's MeaiLlm
var llm = new MeaiLlm(modelName, ollamaClient);

// 3. Pass it to the agent!
var agent = new LlmAgent(new LlmAgentConfig 
{ 
    Name = "ollama_agent", 
    Model = llm,
    Instruction = "You are a helpful, brief, and concise AI assistant running locally via Ollama." 
});

if (args.Contains("--web"))
{
    await AdkWeb.RunAsync(agent);
    return;
}

var runner = new InMemoryRunner("ollama-app", agent);

// Create a persistent session so conversation history is preserved across turns
var session = await runner.SessionService.CreateSessionAsync(
    new GoogleAdk.Core.Abstractions.Sessions.CreateSessionRequest
    {
        AppName = "ollama-app",
        UserId = "user-1",
    });

Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
Console.WriteLine("║  ADK C# — Ollama Sample                                  ║");
Console.WriteLine("║  Ask anything! The agent is running 100% locally.        ║");
Console.WriteLine("║  Type 'quit' to exit.                                    ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
Console.WriteLine();

while (true)
{
    Console.Write("You: ");
    var input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input) || input.Equals("quit", StringComparison.OrdinalIgnoreCase))
        break;

    var userMessage = new GoogleAdk.Core.Abstractions.Models.Content
    {
        Role = "user",
        Parts = new List<GoogleAdk.Core.Abstractions.Models.Part> { new() { Text = input } }
    };

    Console.WriteLine();
    Console.Write($"[{agent.Name}]: ");
    
    try
    {
        await foreach (var evt in runner.RunAsync("user-1", session.Id, userMessage))
        {
            var text = evt.Content?.Parts?.FirstOrDefault()?.Text;
            if (text != null && evt.Partial == true)
            {
                // Streaming output
                Console.Write(text);
            }
            else if (text != null && evt.TurnComplete == true)
            {
                // Final flush if not streaming, though MEAI streams perfectly via MEAI wrapper.
                // We'll ignore the final text dump if we already printed chunks to avoid duplication,
                // but just in case streaming was disabled, we can print it here if partial was false.
                if (evt.Partial != true) 
                {
                    Console.Write(text);
                }
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\n[Error communicating with Ollama]: {ex.Message}");
    }
    
    Console.WriteLine();
    Console.WriteLine(new string('─', 60));
}
