using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Runner;
using GoogleAdk.ApiServer;
using GoogleAdk.Models.Meai;
using Microsoft.Extensions.AI;
using GoogleAdk.Core.Abstractions.Tools;
using GoogleAdk.Core.Abstractions.Models;


// 1. Create the Microsoft.Extensions.AI chat client for Ollama
// Adjust the model name to whichever model you have pulled in Ollama
string modelName = "qwen3"; // Or "llama3.1", "mistral", "phi3", etc.
IChatClient ollamaClient = new OllamaChatClient(new Uri("http://localhost:11434"), modelName);

// 2. Wrap it in the ADK's MeaiLlm
var llm = new MeaiLlm(modelName, ollamaClient);

// 3. Pass it to the agent!
var agent = new LlmAgent(new LlmAgentConfig
{
    Name = "ollama_agent",
    Model = llm,
    Instruction = """
    You are a helpful AI assistant.
    """,
    Tools = [GetWeatherDataTool]
});

var runner = new InMemoryRunner("ollama-sample", agent);
var session = await runner.SessionService.CreateSessionAsync(new GoogleAdk.Core.Abstractions.Sessions.CreateSessionRequest
{
    AppName = "ollama-sample",
    UserId = "user-1"
});



/// <summary>
/// Fetches the current weather data for a given location.
/// you can use this tool to get the weather data for a given location.
/// </summary>
/// <param name="location">The location to get the weather for (e.g., 'New York')</param>
/// <returns>A WeatherData object containing the location and forecast</returns>
[FunctionTool]
static WeatherData? GetWeatherData(string location)
{
    // trigger recompilation
    return new WeatherData(location, "Sunny with a chance of rainbows");
}
public record WeatherData(string Location, string Forecast);