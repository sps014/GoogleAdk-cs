using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Tools;

namespace GoogleAdk.Core.Agents.Processors;

/// <summary>
/// Injects SetModelResponseTool when output schema is used alongside tools.
/// </summary>
public sealed class OutputSchemaRequestProcessor : BaseLlmRequestProcessor
{
    public static readonly OutputSchemaRequestProcessor Instance = new();

    public override async IAsyncEnumerable<Event> RunAsync(
        InvocationContext invocationContext,
        LlmRequest llmRequest)
    {
        if (invocationContext.Agent is not LlmAgent agent || agent.OutputSchema == null)
            yield break;

        var tools = await agent.CanonicalToolsAsync(new ReadonlyContext(invocationContext));
        if (tools.Count == 0)
            yield break;

        llmRequest.Config ??= new GoogleAdk.Core.Abstractions.Models.GenerateContentConfig();
        llmRequest.Config.ResponseSchema = null;
        llmRequest.Config.ResponseMimeType = null;

        var setModelResponseTool = new SetModelResponseTool(SchemaHelper.TypeToSchemaDict(agent.OutputSchema));
        await setModelResponseTool.ProcessLlmRequestAsync(new AgentContext(invocationContext), llmRequest);

        llmRequest.AppendInstructions(
            "When you are ready to respond, call the tool `set_model_response` " +
            "with the final response JSON matching the required schema.");
    }
}
