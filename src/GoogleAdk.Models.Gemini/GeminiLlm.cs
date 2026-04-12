using GenerativeAI;
using GenerativeAI.Microsoft;
using GenerativeAI.Types;
using GenerativeAI.Types.RagEngine;
using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Models.Meai;
using System.Runtime.CompilerServices;

namespace GoogleAdk.Models.Gemini;

/// <summary>
/// A specialized LLM wrapper for Gemini models that supports native Gemini features
/// like Google Search and Vertex AI Search (Retrieval) which are not supported
/// by standard MEAI ChatOptions.
/// </summary>
public class GeminiLlm : MeaiLlm
{
    private readonly GenerativeAIChatClient _client;

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
            {
                llmRequest.CacheMetadata = cacheMetadata;
            }
        }

        if (genModel != null && llmRequest.Config?.Tools != null)
        {
            // Reset specialized tool state
            genModel.UseGoogleSearch = false;
            SetRetrievalTool(genModel, null);

            foreach (var toolDecl in llmRequest.Config.Tools)
            {
                if (toolDecl.GoogleSearch != null || toolDecl.GoogleSearchRetrieval != null)
                {
                    genModel.UseGoogleSearch = true;
                }

                if (toolDecl.Retrieval?.VertexAiSearch != null)
                {
                    var vs = toolDecl.Retrieval.VertexAiSearch;
                    var specs = vs.DataStoreSpecs?.Select(s => new VertexAISearchDataStoreSpec { DataStore = s.DataStore }).ToList();

                    var toolObj = new GenerativeAI.Types.Tool
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
                    };
                    SetRetrievalTool(genModel, toolObj);
                }
                else if (toolDecl.Retrieval?.VertexRagStore != null)
                {
                    var vrs = toolDecl.Retrieval.VertexRagStore;

                    var toolObj = new GenerativeAI.Types.Tool
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
                    };
                    SetRetrievalTool(genModel, toolObj);
                }
            }
        }

        await foreach (var resp in base.GenerateContentAsync(llmRequest, stream, cancellationToken))
        {
            if (resp.RawRepresentation is GenerateContentResponse raw)
            {
                var gm = raw.Candidates?.FirstOrDefault()?.GroundingMetadata;
                if (gm != null)
                {
                    resp.GroundingMetadata = new GoogleAdk.Core.Abstractions.Models.GroundingMetadata
                    {
                        WebSearchQueries = gm.WebSearchQueries,
                        SearchEntryPoint = gm.SearchEntryPoint == null ? null : new GoogleAdk.Core.Abstractions.Models.SearchEntryPoint
                        {
                            RenderedContent = gm.SearchEntryPoint.RenderedContent
                        },
                        GroundingChunks = gm.GroundingChunks?.Select(c => new GoogleAdk.Core.Abstractions.Models.GroundingChunk
                        {
                            Web = c.Web == null ? null : new GoogleAdk.Core.Abstractions.Models.WebGroundingChunk
                            {
                                Uri = c.Web.Uri,
                                Title = c.Web.Title
                            },
                            RetrievedContext = c.RetrievedContext == null ? null : new GoogleAdk.Core.Abstractions.Models.RetrievedContextGroundingChunk
                            {
                                Uri = c.RetrievedContext.Uri,
                                Title = c.RetrievedContext.Title
                            }
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
            }

            if (llmRequest.CacheMetadata != null)
            {
                resp.CacheMetadata = llmRequest.CacheMetadata.Clone();
            }

            yield return resp;
        }
    }

    public override Task<BaseLlmConnection> ConnectAsync(LlmRequest llmRequest)
    {
        BaseLlmConnection connection = new GeminiLiveConnection(this, llmRequest);
        return Task.FromResult(connection);
    }
}
