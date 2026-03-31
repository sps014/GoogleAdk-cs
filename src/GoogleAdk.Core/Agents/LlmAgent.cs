// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Agents.Processors;
using GoogleAdk.Core.Context;
using GoogleAdk.Core.Tools;
using System.Runtime.CompilerServices;

namespace GoogleAdk.Core.Agents;

/// <summary>
/// Provides an instruction string, optionally dynamically based on context.
/// </summary>
public delegate Task<string> InstructionProvider(ReadonlyContext context);

/// <summary>
/// Callback that runs before a request is sent to the model.
/// Return an LlmResponse to skip the model call.
/// </summary>
public delegate Task<LlmResponse?> BeforeModelCallback(AgentContext context, LlmRequest request);

/// <summary>
/// Callback that runs after a response is received from the model.
/// Return an LlmResponse to override the actual model response.
/// </summary>
public delegate Task<LlmResponse?> AfterModelCallback(AgentContext context, LlmResponse response);

/// <summary>
/// Callback that runs before a tool is called.
/// Return a dictionary to skip the actual tool call.
/// </summary>
public delegate Task<Dictionary<string, object?>?> BeforeToolCallback(
    BaseTool tool, Dictionary<string, object?> args, AgentContext context);

/// <summary>
/// Callback that runs after a tool is called.
/// Return a dictionary to override the tool result.
/// </summary>
public delegate Task<Dictionary<string, object?>?> AfterToolCallback(
    BaseTool tool, Dictionary<string, object?> args, AgentContext context, Dictionary<string, object?> response);

/// <summary>
/// Configuration for an LLM-based agent.
/// </summary>
public class LlmAgentConfig : BaseAgentConfig
{
    /// <summary>The LLM model to use (BaseLlm instance or model name string).</summary>
    public BaseLlm? Model { get; set; }

    /// <summary>The model name (resolved via LlmRegistry if Model is not set).</summary>
    public string? ModelName { get; set; }

    /// <summary>Static instruction string or dynamic provider.</summary>
    public string? Instruction { get; set; }

    /// <summary>Dynamic instruction provider.</summary>
    public InstructionProvider? InstructionProvider { get; set; }

    /// <summary>
    /// Global instruction applied to all agents in the tree.
    /// Only the globalInstruction on the root agent takes effect.
    /// </summary>
    public string? GlobalInstruction { get; set; }

    /// <summary>Dynamic global instruction provider.</summary>
    public InstructionProvider? GlobalInstructionProvider { get; set; }

    /// <summary>Tools available to this agent.</summary>
    public List<IBaseTool>? Tools { get; set; }

    /// <summary>Generate content configuration.</summary>
    public GenerateContentConfig? GenerateContentConfig { get; set; }

    /// <summary>Before model callbacks.</summary>
    public List<BeforeModelCallback>? BeforeModelCallbacks { get; set; }

    /// <summary>After model callbacks.</summary>
    public List<AfterModelCallback>? AfterModelCallbacks { get; set; }

    /// <summary>Before tool callbacks.</summary>
    public List<BeforeToolCallback>? BeforeToolCallbacks { get; set; }

    /// <summary>After tool callbacks.</summary>
    public List<AfterToolCallback>? AfterToolCallbacks { get; set; }

    /// <summary>Output schema for structured output.</summary>
    public Dictionary<string, object?>? OutputSchema { get; set; }

    /// <summary>Input schema for the agent.</summary>
    public Dictionary<string, object?>? InputSchema { get; set; }

    /// <summary>
    /// Key in session state to store the agent's output.
    /// Enables extracting agent replies for later use in tools, callbacks, or inter-agent coordination.
    /// </summary>
    public string? OutputKey { get; set; }

    /// <summary>Disallow transfer to parent agent.</summary>
    public bool DisallowTransferToParent { get; set; }

    /// <summary>Disallow transfer to peer agents.</summary>
    public bool DisallowTransferToPeers { get; set; }

    /// <summary>Controls how conversation contents are included in LLM requests.</summary>
    public IncludeContentsMode IncludeContents { get; set; } = IncludeContentsMode.Default;

    /// <summary>Custom request processors. When null, uses the default pipeline.</summary>
    public List<BaseLlmRequestProcessor>? RequestProcessors { get; set; }

    /// <summary>Custom response processors. When null, uses an empty list.</summary>
    public List<BaseLlmResponseProcessor>? ResponseProcessors { get; set; }

    /// <summary>Context compactors to evaluate in priority order before content loading.</summary>
    public List<IContextCompactor>? ContextCompactors { get; set; }
}

/// <summary>
/// An LLM-based agent that runs a processor pipeline:
/// request processors → tool preprocessors → call model → response processors →
/// handle function calls → agent transfer. Loops until final response.
/// </summary>
public class LlmAgent : BaseAgent
{
    public BaseLlm? Model { get; set; }
    public string? ModelName { get; set; }
    public string? Instruction { get; set; }
    public InstructionProvider? InstructionProviderFunc { get; set; }
    public string? GlobalInstruction { get; set; }
    public InstructionProvider? GlobalInstructionProviderFunc { get; set; }
    public List<IBaseTool> Tools { get; }
    public GenerateContentConfig? GenerateContentConfig { get; set; }
    public List<BeforeModelCallback> BeforeModelCallbacks { get; }
    public List<AfterModelCallback> AfterModelCallbacks { get; }
    public List<BeforeToolCallback> BeforeToolCallbacks { get; }
    public List<AfterToolCallback> AfterToolCallbacks { get; }
    public Dictionary<string, object?>? OutputSchema { get; set; }
    public Dictionary<string, object?>? InputSchema { get; set; }
    public string? OutputKey { get; set; }
    public bool DisallowTransferToParent { get; set; }
    public bool DisallowTransferToPeers { get; set; }
    public IncludeContentsMode IncludeContents { get; set; } = IncludeContentsMode.Default;
    public List<BaseLlmRequestProcessor> RequestProcessors { get; }
    public List<BaseLlmResponseProcessor> ResponseProcessors { get; }

    public LlmAgent(LlmAgentConfig config) : base(config)
    {
        Model = config.Model;
        ModelName = config.ModelName;
        Instruction = config.Instruction;
        InstructionProviderFunc = config.InstructionProvider;
        GlobalInstruction = config.GlobalInstruction;
        GlobalInstructionProviderFunc = config.GlobalInstructionProvider;
        Tools = config.Tools ?? new List<IBaseTool>();
        GenerateContentConfig = config.GenerateContentConfig;
        BeforeModelCallbacks = config.BeforeModelCallbacks ?? new();
        AfterModelCallbacks = config.AfterModelCallbacks ?? new();
        BeforeToolCallbacks = config.BeforeToolCallbacks ?? new();
        AfterToolCallbacks = config.AfterToolCallbacks ?? new();
        OutputSchema = config.OutputSchema;
        InputSchema = config.InputSchema;
        OutputKey = config.OutputKey;
        DisallowTransferToParent = config.DisallowTransferToParent;
        DisallowTransferToPeers = config.DisallowTransferToPeers;
        IncludeContents = config.IncludeContents;

        // Build request processor pipeline (order matters)
        RequestProcessors = config.RequestProcessors ?? new List<BaseLlmRequestProcessor>
        {
            BasicLlmRequestProcessor.Instance,
            IdentityLlmRequestProcessor.Instance,
            InstructionsLlmRequestProcessor.Instance,
            ContentRequestProcessor.Instance,
        };

        // Insert context compactor before content processor when using defaults
        if (config.RequestProcessors == null && config.ContextCompactors is { Count: > 0 })
        {
            var contentIndex = RequestProcessors.IndexOf(ContentRequestProcessor.Instance);
            if (contentIndex >= 0)
                RequestProcessors.Insert(contentIndex, new ContextCompactorRequestProcessor(config.ContextCompactors));
            else
                RequestProcessors.Add(new ContextCompactorRequestProcessor(config.ContextCompactors));
        }

        // Append agent transfer processor unless all transfers disabled
        var agentTransferDisabled = DisallowTransferToParent && DisallowTransferToPeers && SubAgents.Count == 0;
        if (!agentTransferDisabled)
            RequestProcessors.Add(AgentTransferLlmRequestProcessor.Instance);

        ResponseProcessors = config.ResponseProcessors ?? new();

        // Validate generateContentConfig
        if (config.GenerateContentConfig != null)
        {
            if (config.GenerateContentConfig.Tools is { Count: > 0 })
                throw new ArgumentException("All tools must be set via LlmAgent.Tools, not GenerateContentConfig.Tools.");
            if (!string.IsNullOrEmpty(config.GenerateContentConfig.SystemInstruction))
                throw new ArgumentException("System instruction must be set via LlmAgent.Instruction, not GenerateContentConfig.SystemInstruction.");
            if (config.GenerateContentConfig.ResponseSchema != null)
                throw new ArgumentException("Response schema must be set via LlmAgent.OutputSchema, not GenerateContentConfig.ResponseSchema.");
        }

        // Warn about invalid outputSchema + transfer combination
        if (OutputSchema != null && (!DisallowTransferToParent || !DisallowTransferToPeers))
        {
            DisallowTransferToParent = true;
            DisallowTransferToPeers = true;
        }
    }

    /// <summary>
    /// The resolved BaseLlm instance. Inherits from ancestor LlmAgents if not set locally.
    /// </summary>
    public BaseLlm CanonicalModel
    {
        get
        {
            if (Model != null)
                return Model;

            var ancestor = ParentAgent;
            while (ancestor != null)
            {
                if (ancestor is LlmAgent llmAncestor && llmAncestor.Model != null)
                    return llmAncestor.CanonicalModel;
                ancestor = ancestor.ParentAgent;
            }

            throw new InvalidOperationException($"No model found for agent \"{Name}\".");
        }
    }

    /// <summary>
    /// Resolves the instruction to a string and whether it needs state injection.
    /// Static strings require injection; dynamic providers do not.
    /// </summary>
    public async Task<(string instruction, bool requireStateInjection)> ResolveInstructionAsync(ReadonlyContext context)
    {
        if (InstructionProviderFunc != null)
            return (await InstructionProviderFunc(context), false);
        return (Instruction ?? "", true);
    }

    /// <summary>
    /// Resolves the global instruction to a string and whether it needs state injection.
    /// </summary>
    public async Task<(string instruction, bool requireStateInjection)> ResolveGlobalInstructionAsync(ReadonlyContext context)
    {
        if (GlobalInstructionProviderFunc != null)
            return (await GlobalInstructionProviderFunc(context), false);
        return (GlobalInstruction ?? "", true);
    }

    /// <summary>
    /// Resolves all tools (including from toolsets) into a flat list of BaseTool.
    /// </summary>
    public async Task<List<BaseTool>> CanonicalToolsAsync(ReadonlyContext? context = null)
    {
        var resolved = new List<BaseTool>();
        foreach (var tool in Tools)
        {
            if (tool is BaseTool bt)
            {
                resolved.Add(bt);
            }
            else if (tool is BaseToolset toolset)
            {
                AgentContext? agentCtx = context != null ? new AgentContext(context.InvocationContext) : null;
                var tools = await toolset.GetToolsAsync(agentCtx);
                resolved.AddRange(tools);
            }
        }
        return resolved;
    }

    protected override async IAsyncEnumerable<Event> RunAsyncImpl(
        InvocationContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (!context.EndInvocation)
        {
            Event? lastEvent = null;
            await foreach (var evt in RunOneStepAsync(context, cancellationToken).WithCancellation(cancellationToken))
            {
                lastEvent = evt;
                MaybeSaveOutputToState(evt);
                yield return evt;
            }

            if (lastEvent == null || lastEvent.IsFinalResponse())
                break;
            if (lastEvent.Partial == true)
                break;
        }
    }

    private async IAsyncEnumerable<Event> RunOneStepAsync(
        InvocationContext invocationContext,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var llmRequest = new LlmRequest();

        // === Run request processors ===
        foreach (var processor in RequestProcessors)
        {
            await foreach (var evt in processor.RunAsync(invocationContext, llmRequest).WithCancellation(cancellationToken))
                yield return evt;
        }

        // === Run tool preprocessors ===
        foreach (var toolUnion in Tools)
        {
            var toolContext = new AgentContext(invocationContext);
            if (toolUnion is BaseTool bt)
            {
                await bt.ProcessLlmRequestAsync(toolContext, llmRequest);
            }
            else if (toolUnion is BaseToolset toolset)
            {
                var readonlyCtx = new ReadonlyContext(invocationContext);
                var resolvedTools = await toolset.GetToolsAsync(toolContext);
                foreach (var tool in resolvedTools)
                    await tool.ProcessLlmRequestAsync(toolContext, llmRequest);
            }
        }

        if (invocationContext.EndInvocation)
            yield break;

        // === Call the LLM ===
        var modelResponseEvent = Event.Create(e =>
        {
            e.InvocationId = invocationContext.InvocationId;
            e.Author = Name;
            e.Branch = invocationContext.Branch;
        });

        await foreach (var llmResponse in CallLlmAsync(invocationContext, llmRequest, modelResponseEvent, cancellationToken))
        {
            // === Postprocess: response processors + function calls ===
            await foreach (var evt in PostprocessAsync(invocationContext, llmRequest, llmResponse, modelResponseEvent, cancellationToken))
            {
                modelResponseEvent = Event.Create(e =>
                {
                    e.InvocationId = invocationContext.InvocationId;
                    e.Author = Name;
                    e.Branch = invocationContext.Branch;
                });
                yield return evt;
            }
        }
    }

    private async IAsyncEnumerable<Event> PostprocessAsync(
        InvocationContext invocationContext,
        LlmRequest llmRequest,
        LlmResponse llmResponse,
        Event modelResponseEvent,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Run response processors
        foreach (var processor in ResponseProcessors)
        {
            await foreach (var evt in processor.RunAsync(invocationContext, llmResponse).WithCancellation(cancellationToken))
                yield return evt;
        }

        // Skip if no content
        if (llmResponse.Content == null && llmResponse.ErrorCode == null && llmResponse.Interrupted != true)
            yield break;

        // Build merged event
        var mergedEvent = Event.Create(e =>
        {
            e.InvocationId = modelResponseEvent.InvocationId;
            e.Author = modelResponseEvent.Author;
            e.Branch = modelResponseEvent.Branch;
            e.Actions = modelResponseEvent.Actions;
            e.Content = llmResponse.Content;
            e.GroundingMetadata = llmResponse.GroundingMetadata;
            e.CitationMetadata = llmResponse.CitationMetadata;
            e.UsageMetadata = llmResponse.UsageMetadata;
            e.FinishReason = llmResponse.FinishReason;
            e.ErrorCode = llmResponse.ErrorCode;
            e.ErrorMessage = llmResponse.ErrorMessage;
            e.Partial = llmResponse.Partial;
            e.Interrupted = llmResponse.Interrupted;
            e.CustomMetadata = llmResponse.CustomMetadata;
        });

        if (mergedEvent.Content != null)
        {
            var functionCalls = mergedEvent.GetFunctionCalls();
            if (functionCalls.Count > 0)
            {
                FunctionCallHandler.PopulateClientFunctionCallId(mergedEvent);
                mergedEvent.LongRunningToolIds = FunctionCallHandler
                    .GetLongRunningFunctionCalls(functionCalls, llmRequest.ToolsDict)
                    .ToList();
            }
        }

        yield return mergedEvent;

        // === Process function calls ===
        var calls = mergedEvent.GetFunctionCalls();
        if (calls.Count == 0)
            yield break;

        if (invocationContext.RunConfig?.PauseOnToolCalls == true)
        {
            invocationContext.EndInvocation = true;
            yield break;
        }

        var functionResponseEvent = await FunctionCallHandler.HandleFunctionCallsAsync(
            invocationContext, mergedEvent, llmRequest.ToolsDict,
            BeforeToolCallbacks, AfterToolCallbacks);

        if (functionResponseEvent == null)
            yield break;

        // Auth event
        var authEvent = FunctionCallHandler.GenerateAuthEvent(invocationContext, functionResponseEvent);
        if (authEvent != null)
            yield return authEvent;

        // Tool confirmation event
        var confirmEvent = FunctionCallHandler.GenerateRequestConfirmationEvent(
            invocationContext, mergedEvent, functionResponseEvent);
        if (confirmEvent != null)
        {
            yield return confirmEvent;
            invocationContext.EndInvocation = true;
            yield break;
        }

        // Yield function response
        yield return functionResponseEvent;

        // Agent transfer
        var nextAgentName = functionResponseEvent.Actions.TransferToAgent;
        if (nextAgentName != null)
        {
            var rootAgent = invocationContext.Agent.RootAgent;
            var nextAgent = rootAgent.FindAgent(nextAgentName)
                ?? throw new InvalidOperationException($"Agent \"{nextAgentName}\" not found in the agent tree.");

            await foreach (var evt in nextAgent.RunAsync(invocationContext, cancellationToken).WithCancellation(cancellationToken))
                yield return evt;
        }
    }

    private async IAsyncEnumerable<LlmResponse> CallLlmAsync(
        InvocationContext invocationContext,
        LlmRequest llmRequest,
        Event modelResponseEvent,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var callbackContext = new AgentContext(invocationContext, eventActions: modelResponseEvent.Actions);

        // Plugin before model → canonical before model
        var beforeResponse = await HandleBeforeModelCallbackAsync(invocationContext, llmRequest, callbackContext);
        if (beforeResponse != null)
        {
            yield return beforeResponse;
            yield break;
        }

        // Call LLM
        var llm = CanonicalModel;
        invocationContext.IncrementLlmCallCount();

        await foreach (var llmResponse in llm.GenerateContentAsync(llmRequest).WithCancellation(cancellationToken))
        {
            // Plugin after model → canonical after model
            var altered = await HandleAfterModelCallbackAsync(invocationContext, llmResponse, callbackContext);
            yield return altered ?? llmResponse;
        }
    }

    private async Task<LlmResponse?> HandleBeforeModelCallbackAsync(
        InvocationContext invocationContext, LlmRequest llmRequest, AgentContext callbackContext)
    {
        // Plugin callbacks first
        if (invocationContext.PluginManager != null)
        {
            var pluginResponse = await invocationContext.PluginManager.RunBeforeModelCallbackAsync(callbackContext, llmRequest);
            if (pluginResponse != null)
                return pluginResponse;
        }

        // Canonical callbacks
        foreach (var callback in BeforeModelCallbacks)
        {
            var response = await callback(callbackContext, llmRequest);
            if (response != null) return response;
        }
        return null;
    }

    private async Task<LlmResponse?> HandleAfterModelCallbackAsync(
        InvocationContext invocationContext, LlmResponse llmResponse, AgentContext callbackContext)
    {
        // Plugin callbacks first
        if (invocationContext.PluginManager != null)
        {
            var pluginResponse = await invocationContext.PluginManager.RunAfterModelCallbackAsync(callbackContext, llmResponse);
            if (pluginResponse != null)
                return pluginResponse;
        }

        // Canonical callbacks
        foreach (var callback in AfterModelCallbacks)
        {
            var response = await callback(callbackContext, llmResponse);
            if (response != null) return response;
        }
        return null;
    }

    private void MaybeSaveOutputToState(Event evt)
    {
        if (evt.Author != Name) return;
        if (string.IsNullOrEmpty(OutputKey)) return;
        if (!evt.IsFinalResponse()) return;
        if (evt.Content?.Parts == null || evt.Content.Parts.Count == 0) return;

        var resultStr = string.Join("", evt.Content.Parts.Select(p => p.Text ?? ""));
        object result = resultStr;

        if (OutputSchema != null)
        {
            if (string.IsNullOrWhiteSpace(resultStr)) return;
            try
            {
                result = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(resultStr)
                    ?? (object)resultStr;
            }
            catch
            {
                // Keep as string if JSON parse fails
            }
        }

        evt.Actions.StateDelta[OutputKey] = result;
    }
}
