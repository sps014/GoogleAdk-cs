using GenerativeAI;
using GenerativeAI.Microsoft;
using GenerativeAI.Types;
using GenerativeAI.Types.RagEngine;
using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Models.Meai;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;
using AdkContent = GoogleAdk.Core.Abstractions.Models.Content;
using AdkPart = GoogleAdk.Core.Abstractions.Models.Part;

namespace GoogleAdk.Models.Gemini;

/// <summary>
/// A specialized LLM wrapper for Gemini models that supports native Gemini features
/// like Google Search and Vertex AI Search (Retrieval) which are not supported
/// by standard MEAI ChatOptions.
/// </summary>
public class GeminiLlm : MeaiLlm
{
    private readonly GenerativeAIChatClient _client;

    // Per-streaming-turn buffer for accumulating thought text across chunks.
    // Reset after each final aggregated response.
    private readonly System.Text.StringBuilder _thinkingBuffer = new();

    public GeminiLlm(string model, GenerativeAIChatClient client) : base(model, client)
    {
        _client = client;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_RetrievalTool")]
    extern static void SetRetrievalTool(GenerativeModel obj, GenerativeAI.Types.Tool? value);

    protected override Microsoft.Extensions.AI.ChatOptions? ConvertToMeaiOptions(LlmRequest llmRequest)
    {
        var options = base.ConvertToMeaiOptions(llmRequest);
        if (options == null) return null;

        var config = llmRequest.Config;
        if (config != null)
        {
            if (config.ResponseModalities?.Count > 0)
            {
                var genConfigModalities = config.ResponseModalities
                    .Select(m => Enum.TryParse<GenerativeAI.Types.Modality>(m.ToString(), true, out var mod) ? mod : GenerativeAI.Types.Modality.MODALITY_UNSPECIFIED)
                    .Where(m => m != GenerativeAI.Types.Modality.MODALITY_UNSPECIFIED)
                    .ToList();
                
                options.AdditionalProperties ??= new Microsoft.Extensions.AI.AdditionalPropertiesDictionary();
                options.AdditionalProperties.Remove("responseModalities");
                options.AdditionalProperties["ResponseModalities"] = genConfigModalities;
            }

            options.AdditionalProperties?.Remove("speechConfig");

            // Inject ThinkingConfig via AdditionalProperties (the bridge reads these keys).
            if (config.ThinkingConfig != null)
            {
                options.AdditionalProperties ??= new Microsoft.Extensions.AI.AdditionalPropertiesDictionary();
                if (config.ThinkingConfig.IncludeThoughts.HasValue)
                    options.AdditionalProperties["IncludeThoughts"] = config.ThinkingConfig.IncludeThoughts.Value;
                if (config.ThinkingConfig.ThinkingBudget.HasValue)
                    options.AdditionalProperties["ThinkingBudget"] = config.ThinkingConfig.ThinkingBudget.Value;
            }

            options.RawRepresentationFactory = _ =>
            {
                var genConfig = new GenerativeAI.Types.GenerationConfig();

                if (config.SpeechConfig != null)
                {
                    var adkSpeechConfig = config.SpeechConfig;
                    var genSpeechConfig = new GenerativeAI.Types.SpeechConfig();

                    if (adkSpeechConfig.VoiceConfig != null)
                    {
                        var voiceConfig = new GenerativeAI.Types.VoiceConfig();
                        if (adkSpeechConfig.VoiceConfig.PrebuiltVoiceConfig != null)
                        {
                            voiceConfig.PrebuiltVoiceConfig = new GenerativeAI.Types.PrebuiltVoiceConfig
                            {
                                VoiceName = adkSpeechConfig.VoiceConfig.PrebuiltVoiceConfig.VoiceName
                            };
                        }
                        genSpeechConfig.VoiceConfig = voiceConfig;
                    }
                    // genConfig.SpeechConfig = genSpeechConfig;
                }

                // Inject ThinkingConfig so the Gemini API enables model-native thinking.
                // The GenerativeAI.Microsoft bridge reads these from AdditionalProperties.
                if (config.ThinkingConfig != null)
                {
                    genConfig.ThinkingConfig = new GenerativeAI.Types.ThinkingConfig
                    {
                        ThinkingBudget = config.ThinkingConfig.ThinkingBudget,
                        IncludeThoughts = config.ThinkingConfig.IncludeThoughts ?? false
                    };
                }

                return genConfig;
            };
        }

        return options;
    }

    public override async IAsyncEnumerable<LlmResponse> GenerateContentAsync(
        LlmRequest llmRequest,
        bool stream = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Extract the underlying GenerativeModel to configure native tools
        var genModel = _client.model;

        if (genModel != null && llmRequest.CacheConfig != null)
        {
            var cacheManager = new GeminiContextCacheManager(genModel);
            var cacheMetadata = await cacheManager.HandleContextCachingAsync(llmRequest);
            if (cacheMetadata != null)
                llmRequest.CacheMetadata = cacheMetadata;
        }

        if (genModel != null && llmRequest.Config?.Tools != null)
        {
            genModel.UseGoogleSearch = false;
            SetRetrievalTool(genModel, null);

            foreach (var toolDecl in llmRequest.Config.Tools)
            {
                if (toolDecl.GoogleSearch != null || toolDecl.GoogleSearchRetrieval != null)
                    genModel.UseGoogleSearch = true;

                if (toolDecl.Retrieval?.VertexAiSearch != null)
                {
                    var vs = toolDecl.Retrieval.VertexAiSearch;
                    var specs = vs.DataStoreSpecs?.Select(s => new VertexAISearchDataStoreSpec { DataStore = s.DataStore }).ToList();
                    SetRetrievalTool(genModel, new GenerativeAI.Types.Tool
                    {
                        Retrieval = new VertexRetrievalTool
                        {
                            VertexAiSearch = new VertexAISearch
                            {
                                Datastore = vs.Datastore,
                                Engine = vs.Engine,
                                Filter = vs.Filter,
                                DataStoreSpecs = specs,
                                MaxResults = vs.MaxResults
                            }
                        }
                    });
                }
                else if (toolDecl.Retrieval?.VertexRagStore != null)
                {
                    var vrs = toolDecl.Retrieval.VertexRagStore;
                    SetRetrievalTool(genModel, new GenerativeAI.Types.Tool
                    {
                        Retrieval = new VertexRetrievalTool
                        {
                            VertexRagStore = new VertexRagStore
                            {
                                RagCorpora = vrs.RagCorpora,
                                SimilarityTopK = vrs.SimilarityTopK,
                                VectorDistanceThreshold = (float?)vrs.VectorDistanceThreshold
                            }
                        }
                    });
                }
            }
        }

        if (stream)
        {
            // Drive the MEAI streaming loop directly so we can inspect every chunk's
            // RawRepresentation for Gemini thought flags — the MEAI bridge may not
            // expose thought text in update.Text at all (it silently drops it).
            var messages = ConvertToMeaiMessages(llmRequest);
            var options = ConvertToMeaiOptions(llmRequest);

            var regularTextBuffer = new System.Text.StringBuilder();
            var fcParts = new List<AdkPart>();
            object? lastRaw = null;

            await foreach (var update in ((IChatClient)_client).GetStreamingResponseAsync(
                messages, options, cancellationToken))
            {
                lastRaw = update.RawRepresentation ?? lastRaw;

                // --- Detect thought flag from raw Gemini chunk ---
                bool isThought = false;
                string? rawThoughtText = null;
                if (update.RawRepresentation is GenerateContentResponse chunkRaw)
                {
                    var chunkParts = chunkRaw.Candidates?.FirstOrDefault()?.Content?.Parts;
                    if (chunkParts?.Count > 0 && chunkParts[0].Thought == true)
                    {
                        isThought = true;
                        rawThoughtText = chunkParts[0].Text;
                    }
                }

                // Determine effective text: prefer update.Text; fall back to raw for thoughts
                // that the MEAI bridge dropped (i.e. didn't put into update.Text).
                var effectiveText = update.Text;
                if (isThought && string.IsNullOrEmpty(effectiveText))
                    effectiveText = rawThoughtText;

                if (!string.IsNullOrEmpty(effectiveText))
                {
                    if (isThought)
                    {
                        _thinkingBuffer.Append(effectiveText);
                        yield return new LlmResponse
                        {
                            Content = new AdkContent
                            {
                                Role = "model",
                                Parts = new List<AdkPart>
                                    { new AdkPart { Text = effectiveText, Thought = true } }
                            },
                            Partial = true,
                            RawRepresentation = update.RawRepresentation,
                        };
                    }
                    else
                    {
                        regularTextBuffer.Append(effectiveText);
                        yield return new LlmResponse
                        {
                            Content = new AdkContent
                            {
                                Role = "model",
                                Parts = new List<AdkPart>
                                    { new AdkPart { Text = effectiveText } }
                            },
                            Partial = true,
                            RawRepresentation = update.RawRepresentation,
                        };
                    }
                }

                // Handle function calls, reasoning content (Ollama/OpenAI), and data content
                if (update.Contents != null)
                {
                    foreach (var content in update.Contents)
                    {
                        if (content is TextReasoningContent reasoningContent && reasoningContent.Text is { Length: > 0 })
                        {
                            // Non-Gemini provider reasoning (e.g. Ollama deepseek-r1)
                            _thinkingBuffer.Append(reasoningContent.Text);
                            yield return new LlmResponse
                            {
                                Content = new AdkContent
                                {
                                    Role = "model",
                                    Parts = new List<AdkPart>
                                        { new AdkPart { Text = reasoningContent.Text, Thought = true } }
                                },
                                Partial = true,
                                RawRepresentation = update.RawRepresentation,
                            };
                        }
                        else if (content is FunctionCallContent functionCall)
                        {
                            var fcPart = new AdkPart
                            {
                                FunctionCall = new GoogleAdk.Core.Abstractions.Models.FunctionCall
                                {
                                    Name = functionCall.Name,
                                    Args = ConvertArgsToDictionary(functionCall.Arguments),
                                    Id = functionCall.CallId,
                                }
                            };
                            fcParts.Add(fcPart);
                            yield return new LlmResponse
                            {
                                Content = new AdkContent { Role = "model", Parts = new List<AdkPart> { fcPart } },
                                Partial = true,
                                RawRepresentation = update.RawRepresentation,
                            };
                        }
                        else if (content is DataContent dataContent)
                        {
                            var dataPart = new AdkPart
                            {
                                InlineData = new InlineData
                                {
                                    MimeType = dataContent.MediaType,
                                    Data = Convert.ToBase64String(dataContent.Data.ToArray())
                                }
                            };
                            fcParts.Add(dataPart);
                            yield return new LlmResponse
                            {
                                Content = new AdkContent { Role = "model", Parts = new List<AdkPart> { dataPart } },
                                Partial = true,
                                RawRepresentation = update.RawRepresentation,
                            };
                        }
                    }
                }
            }

            // Build the final aggregated response.
            // Thought parts are intentionally excluded: they were already emitted as
            // partial streaming events and including them again here would cause the
            // web UI (and ConsoleRunner) to render the thinking content twice.
            var finalParts = new List<AdkPart>();
            if (regularTextBuffer.Length > 0)
                finalParts.Add(new AdkPart { Text = regularTextBuffer.ToString() });
            finalParts.AddRange(fcParts);

            _thinkingBuffer.Clear();

            if (finalParts.Count > 0 || lastRaw != null)
            {
                var finalResp = new LlmResponse
                {
                    Content = new AdkContent
                    {
                        Role = "model",
                        Parts = finalParts.Count > 0 ? finalParts : new List<AdkPart>()
                    },
                    Partial = false,
                    TurnComplete = true,
                    RawRepresentation = lastRaw,
                };

                if (lastRaw is GenerateContentResponse finalRaw)
                    ApplyGroundingMetadata(finalResp, finalRaw);

                if (llmRequest.CacheMetadata != null)
                    finalResp.CacheMetadata = llmRequest.CacheMetadata.Clone();

                yield return finalResp;
            }
        }
        else
        {
            // Non-streaming: use base class, then inject thought parts from raw response.
            // The MEAI bridge may not include thought content in TextContent items, so we
            // read them directly from resp.RawRepresentation (which is always a
            // GenerateContentResponse for non-streaming Gemini calls).
            await foreach (var resp in base.GenerateContentAsync(llmRequest, false, cancellationToken))
            {
                if (resp.RawRepresentation is GenerateContentResponse raw)
                {
                    ApplyGroundingMetadata(resp, raw);
                    InjectThoughtParts(resp, raw);
                }

                if (llmRequest.CacheMetadata != null)
                    resp.CacheMetadata = llmRequest.CacheMetadata.Clone();

                yield return resp;
            }
        }
    }

    /// <summary>
    /// Copies grounding metadata from a raw Gemini response onto an LlmResponse.
    /// </summary>
    private static void ApplyGroundingMetadata(LlmResponse resp, GenerateContentResponse raw)
    {
        var gm = raw.Candidates?.FirstOrDefault()?.GroundingMetadata;
        if (gm == null) return;

        resp.GroundingMetadata = new GoogleAdk.Core.Abstractions.Models.GroundingMetadata
        {
            WebSearchQueries = gm.WebSearchQueries,
            SearchEntryPoint = gm.SearchEntryPoint == null ? null : new GoogleAdk.Core.Abstractions.Models.SearchEntryPoint
            {
                RenderedContent = gm.SearchEntryPoint.RenderedContent
            },
            GroundingChunks = gm.GroundingChunks?.Select(c => new GoogleAdk.Core.Abstractions.Models.GroundingChunk
            {
                Web = c.Web == null ? null : new GoogleAdk.Core.Abstractions.Models.WebGroundingChunk { Uri = c.Web.Uri, Title = c.Web.Title },
                RetrievedContext = c.RetrievedContext == null ? null : new GoogleAdk.Core.Abstractions.Models.RetrievedContextGroundingChunk { Uri = c.RetrievedContext.Uri, Title = c.RetrievedContext.Title }
            }).ToList(),
            GroundingSupports = gm.GroundingSupports?.Select(s => new GoogleAdk.Core.Abstractions.Models.GroundingSupport
            {
                Segment = s.Segment == null ? null : new GoogleAdk.Core.Abstractions.Models.Segment
                {
                    StartIndex = s.Segment.StartIndex,
                    EndIndex = s.Segment.EndIndex,
                    Text = s.Segment.Text
                },
                GroundingChunkIndices = s.GroundingChunkIndices?.ToList()
            }).ToList()
        };
    }

    /// <summary>
    /// Prepends thought parts from the raw Gemini response onto an LlmResponse.
    /// The MEAI bridge may drop thought-flagged parts from its TextContent mapping,
    /// so we read them directly from the raw candidate parts.
    /// </summary>
    private static void InjectThoughtParts(LlmResponse resp, GenerateContentResponse raw)
    {
        var rawParts = raw.Candidates?.FirstOrDefault()?.Content?.Parts;
        if (rawParts == null || resp.Content == null) return;

        var thoughtParts = rawParts
            .Where(p => p.Thought == true && !string.IsNullOrEmpty(p.Text))
            .Select(p => new AdkPart { Text = p.Text, Thought = true })
            .ToList();

        if (thoughtParts.Count == 0) return;

        // Prepend thought parts before existing non-thought parts.
        var existing = resp.Content.Parts ?? new List<AdkPart>();
        var nonThought = existing.Where(p => p.Thought != true).ToList();
        resp.Content.Parts = [.. thoughtParts, .. nonThought];
    }

    public override Task<BaseLlmConnection> ConnectAsync(LlmRequest llmRequest)
    {
        BaseLlmConnection connection = new GeminiLiveConnection(this, llmRequest);
        return Task.FromResult(connection);
    }
}
