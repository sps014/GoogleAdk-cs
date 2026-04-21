using System.Runtime.CompilerServices;
using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Agents;

namespace GoogleAdk.Core.Agents.Processors;

/// <summary>
/// Sets model config, output schema, and streaming/live config from the agent.
/// </summary>
public class BasicLlmRequestProcessor : BaseLlmRequestProcessor
{
    public static readonly BasicLlmRequestProcessor Instance = new();

    public override async IAsyncEnumerable<Event> RunAsync(
        InvocationContext invocationContext,
        LlmRequest llmRequest)
    {
        await Task.CompletedTask;

        if (invocationContext.Agent is not LlmAgent agent)
            yield break;

        llmRequest.Model = agent.CanonicalModel.Model;

        if (agent.GenerateContentConfig != null)
        {
            llmRequest.Config = new Abstractions.Models.GenerateContentConfig
            {
                SystemInstruction = agent.GenerateContentConfig.SystemInstruction,
                Temperature = agent.GenerateContentConfig.Temperature,
                TopP = agent.GenerateContentConfig.TopP,
                TopK = agent.GenerateContentConfig.TopK,
                MaxOutputTokens = agent.GenerateContentConfig.MaxOutputTokens,
                ResponseModalities = agent.GenerateContentConfig.ResponseModalities,
                SpeechConfig = agent.GenerateContentConfig.SpeechConfig,
                ThinkingConfig = agent.GenerateContentConfig.ThinkingConfig
            };
        }
        else
        {
            llmRequest.Config ??= new Abstractions.Models.GenerateContentConfig();
        }

        if (agent.OutputSchema != null)
            llmRequest.SetOutputSchema(SchemaHelper.TypeToSchemaDict(agent.OutputSchema));
    }
}
