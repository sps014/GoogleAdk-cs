using GoogleAdk;
using GoogleAdk.ApiServer;
using GoogleAdk.Core;
using GoogleAdk.Core.Abstractions.Tools;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Tools;

AdkEnv.Load();

var ragCorpus = Environment.GetEnvironmentVariable("VERTEX_AI_RAG_CORPUS");
if (string.IsNullOrEmpty(ragCorpus))
{
    Console.WriteLine("VERTEX_AI_RAG_CORPUS is missing from the environment. Please set it in .env");
    return;
}

var ragTool = new VertexAiRagRetrievalTool(
    ragCorpora: [ragCorpus],
    similarityTopK: 5,
    vectorDistanceThreshold: 0.3
);

var agent = new LlmAgent(new LlmAgentConfig
{
    Model = "gemini-2.5-flash",
    Name = "RagAgent",
    Description = "An agent that can do things with vertex ai rag search",
    Instruction = "Use your tools to find information in the corporate documents when the user asks.",
    Tools = [ragTool]
});

// Run the server on port 8080.
Console.WriteLine("Starting ADK server on http://localhost:8080...");
await AdkServer.RunAsync(agent, options => 
{
    options.Port = 8080;
});