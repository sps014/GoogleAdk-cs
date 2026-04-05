using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Agents;

namespace GoogleAdk.Core.Tools;

public sealed class VertexAiRagRetrievalTool : BaseTool
{
    public List<string>? RagCorpora { get; }
    public List<VertexAiSearchDataStoreSpec>? RagResources { get; }
    public int? SimilarityTopK { get; }
    public double? VectorDistanceThreshold { get; }

    public VertexAiRagRetrievalTool(
        List<string>? ragCorpora = null,
        List<VertexAiSearchDataStoreSpec>? ragResources = null,
        int? similarityTopK = null,
        double? vectorDistanceThreshold = null)
        : base("vertex_ai_rag_retrieval", "A retrieval tool that uses Vertex AI RAG to retrieve data.")
    {
        RagCorpora = ragCorpora;
        RagResources = ragResources;
        SimilarityTopK = similarityTopK;
        VectorDistanceThreshold = vectorDistanceThreshold;
    }

    public override Task<object?> RunAsync(Dictionary<string, object?> args, AgentContext context)
        => Task.FromResult<object?>(null);

    public override Task ProcessLlmRequestAsync(AgentContext context, LlmRequest llmRequest)
    {
        llmRequest.Config ??= new GenerateContentConfig();
        llmRequest.Config.Tools ??= new List<ToolDeclaration>();

        // Currently Maps to VertexRagStore concept inside Retrieval
        llmRequest.Config.Tools.Add(new ToolDeclaration
        {
            Retrieval = new RetrievalConfig
            {
                VertexAiSearch = new VertexAiSearchConfig
                {
                    // Map RAG properties into the config
                    DataStoreSpecs = RagResources
                }
            }
        });
        
        return Task.CompletedTask;
    }
}
