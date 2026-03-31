// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using GoogleAdk.Core.Abstractions.Events;

namespace GoogleAdk.Core.Agents.Processors;

/// <summary>
/// Handles global instruction (from root agent) and local instruction (from current agent).
/// Supports both static strings and dynamic InstructionProviders.
/// Performs session state injection for {var_name} placeholders in static strings.
/// </summary>
public class InstructionsLlmRequestProcessor : BaseLlmRequestProcessor
{
    public static readonly InstructionsLlmRequestProcessor Instance = new();

    public override async IAsyncEnumerable<Event> RunAsync(
        InvocationContext invocationContext,
        LlmRequest llmRequest)
    {
        if (invocationContext.Agent is not LlmAgent agent)
            yield break;

        var readonlyContext = new ReadonlyContext(invocationContext);

        // Step 1: Append global instructions from root agent
        var rootAgent = agent.RootAgent;
        if (rootAgent is LlmAgent rootLlm && rootLlm.GlobalInstruction != null)
        {
            var (instruction, requireStateInjection) = await rootLlm.ResolveGlobalInstructionAsync(readonlyContext);
            if (!string.IsNullOrEmpty(instruction))
            {
                if (requireStateInjection)
                    instruction = await InstructionInjector.InjectSessionStateAsync(instruction, readonlyContext);
                llmRequest.AppendInstructions(instruction);
            }
        }

        // Step 2: Append local instructions from current agent
        var (localInstruction, localRequiresInjection) = await agent.ResolveInstructionAsync(readonlyContext);
        if (!string.IsNullOrEmpty(localInstruction))
        {
            if (localRequiresInjection)
                localInstruction = await InstructionInjector.InjectSessionStateAsync(localInstruction, readonlyContext);
            llmRequest.AppendInstructions(localInstruction);
        }
    }
}
