using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Runner;
using GoogleAdk.ApiServer;
using GoogleAdk.Models.Meai;
using Microsoft.Extensions.AI;


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

await AdkServer.RunAsync(agent);
