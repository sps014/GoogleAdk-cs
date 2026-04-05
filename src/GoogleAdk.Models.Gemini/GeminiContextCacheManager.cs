using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using GenerativeAI;
using GenerativeAI.Types;
using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using CacheMetadata = GoogleAdk.Core.Abstractions.Models.CacheMetadata;
using Google.GenAI;
using Google.GenAI.Types;

namespace GoogleAdk.Models.Gemini;

/// <summary>
/// Manages context cache lifecycle for Gemini models.
/// </summary>
public sealed class GeminiContextCacheManager
{
    private readonly GenerativeModel _model;
    private readonly Client _genAiClient;

    public GeminiContextCacheManager(GenerativeModel model)
    {
        _model = model;
        
        var useVertexAi = string.Equals(
            System.Environment.GetEnvironmentVariable("GOOGLE_GENAI_USE_VERTEXAI"),
            "True", StringComparison.OrdinalIgnoreCase);

        if (useVertexAi)
        {
            var projectId = System.Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT");
            var location = System.Environment.GetEnvironmentVariable("GOOGLE_CLOUD_LOCATION") ?? "us-central1";
            _genAiClient = new Client(vertexAI: true, project: projectId, location: location);
        }
        else
        {
            var apiKey = System.Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
            _genAiClient = new Client(apiKey: apiKey);
        }
    }

    public async Task<CacheMetadata?> HandleContextCachingAsync(LlmRequest llmRequest)
    {
        var cacheConfig = llmRequest.CacheConfig as ContextCacheConfig;
        if (cacheConfig == null) return null;

        if (llmRequest.CacheMetadata != null)
        {
            if (IsCacheValid(llmRequest, cacheConfig))
            {
                var cacheName = llmRequest.CacheMetadata.CacheName;
                var cacheContentsCount = llmRequest.CacheMetadata.ContentsCount;
                ApplyCacheToRequest(llmRequest, cacheName, cacheContentsCount);
                return llmRequest.CacheMetadata.Clone();
            }
            else
            {
                var oldCacheMetadata = llmRequest.CacheMetadata;
                if (oldCacheMetadata.CacheName != null)
                {
                    await CleanupCacheAsync(oldCacheMetadata.CacheName);
                }
            }
        }

        var totalContentsCount = FindCountOfContentsToCache(llmRequest.Contents);
        var fingerprintForAll = GenerateCacheFingerprint(llmRequest, totalContentsCount);

        var newCacheMetadata = await CreateNewCacheWithContentsAsync(llmRequest, cacheConfig, totalContentsCount);
        if (newCacheMetadata != null)
        {
            ApplyCacheToRequest(llmRequest, newCacheMetadata.CacheName, totalContentsCount);
            return newCacheMetadata;
        }

        return new CacheMetadata
        {
            Fingerprint = fingerprintForAll,
            ContentsCount = totalContentsCount
        };
    }

    private int FindCountOfContentsToCache(List<GoogleAdk.Core.Abstractions.Models.Content> contents)
    {
        if (contents == null || contents.Count == 0) return 0;
        
        int lastUserBatchStart = contents.Count;
        for (int i = contents.Count - 1; i >= 0; i--)
        {
            if (string.Equals(contents[i].Role, "user", StringComparison.OrdinalIgnoreCase))
            {
                lastUserBatchStart = i;
            }
            else
            {
                break;
            }
        }
        return lastUserBatchStart;
    }

    private bool IsCacheValid(LlmRequest llmRequest, ContextCacheConfig config)
    {
        var meta = llmRequest.CacheMetadata;
        if (meta == null || meta.CacheName == null) return false;

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (now >= meta.ExpireTime) return false;

        if (meta.InvocationsUsed > config.CacheIntervals) return false;

        var currentFingerprint = GenerateCacheFingerprint(llmRequest, meta.ContentsCount);
        if (currentFingerprint != meta.Fingerprint) return false;

        return true;
    }

    private string GenerateCacheFingerprint(LlmRequest request, int cacheContentsCount)
    {
        var data = new Dictionary<string, object>();

        if (request.Config?.SystemInstruction != null)
            data["system_instruction"] = request.Config.SystemInstruction;

        if (request.Config?.Tools != null)
            data["tools"] = request.Config.Tools;

        if (cacheContentsCount > 0 && request.Contents != null)
            data["cached_contents"] = request.Contents.Take(cacheContentsCount).ToList();

        var json = JsonSerializer.Serialize(data);
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(json));
        var hex = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        return hex.Substring(0, 16);
    }

    private async Task<CacheMetadata?> CreateNewCacheWithContentsAsync(LlmRequest request, ContextCacheConfig config, int cacheContentsCount)
    {
        if (request.CacheableContentsTokenCount == null) return null;
        if (request.CacheableContentsTokenCount < config.MinTokens) return null;

        try
        {
            var genAiConfig = new CreateCachedContentConfig
            {
                Ttl = $"{config.TtlSeconds}s",
                DisplayName = $"adk-cache-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}-{cacheContentsCount}contents",
                Contents = MapToGenAiContents(request.Contents.Take(cacheContentsCount))
            };

            if (request.Config?.SystemInstruction != null)
            {
                genAiConfig.SystemInstruction = new Google.GenAI.Types.Content
                {
                    Role = "system",
                    Parts = new List<Google.GenAI.Types.Part> { new Google.GenAI.Types.Part { Text = request.Config.SystemInstruction } }
                };
            }
            
            if (request.Config?.Tools != null && request.Config.Tools.Count > 0)
            {
                var toolsList = new List<Google.GenAI.Types.Tool>();
                foreach (var tool in request.Config.Tools)
                {
                    if (tool.FunctionDeclarations != null && tool.FunctionDeclarations.Count > 0)
                    {
                        var genAiTool = new Google.GenAI.Types.Tool { FunctionDeclarations = new List<Google.GenAI.Types.FunctionDeclaration>() };
                        foreach (var fd in tool.FunctionDeclarations)
                        {
                            var genAiFd = new Google.GenAI.Types.FunctionDeclaration
                            {
                                Name = fd.Name,
                                Description = fd.Description,
                                // We don't map the parameters explicitly for caching since it only needs the signature,
                                // but we should pass it to ensure the fingerprint/model has the exact same context.
                                // It might be safer to serialize/deserialize
                            };
                            
                            if (fd.Parameters != null)
                            {
                                var jsonParams = JsonSerializer.Serialize(fd.Parameters);
                                genAiFd.Parameters = JsonSerializer.Deserialize<Google.GenAI.Types.Schema>(jsonParams);
                            }
                            genAiTool.FunctionDeclarations.Add(genAiFd);
                        }
                        toolsList.Add(genAiTool);
                    }
                    else if (tool.GoogleSearch != null)
                    {
                        toolsList.Add(new Google.GenAI.Types.Tool { GoogleSearch = new Google.GenAI.Types.GoogleSearch() });
                    }
                    else if (tool.GoogleSearchRetrieval != null)
                    {
                        toolsList.Add(new Google.GenAI.Types.Tool { GoogleSearch = new Google.GenAI.Types.GoogleSearch() });
                    }
                    else if (tool.Retrieval?.VertexAiSearch != null)
                    {
                        var vs = tool.Retrieval.VertexAiSearch;
                        var specs = vs.DataStoreSpecs?.Select(s => new Google.GenAI.Types.VertexAISearchDataStoreSpec { DataStore = s.DataStore }).ToList();
                        
                        toolsList.Add(new Google.GenAI.Types.Tool 
                        { 
                            Retrieval = new Google.GenAI.Types.Retrieval 
                            {
                                VertexAiSearch = new Google.GenAI.Types.VertexAISearch
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
                    else if (tool.Retrieval?.VertexRagStore != null)
                    {
                        var vrs = tool.Retrieval.VertexRagStore;
                        
                        toolsList.Add(new Google.GenAI.Types.Tool 
                        { 
                            Retrieval = new Google.GenAI.Types.Retrieval 
                            {
                                VertexRagStore = new Google.GenAI.Types.VertexRagStore
                                {
                                    RagCorpora = vrs.RagCorpora,
                                    SimilarityTopK = vrs.SimilarityTopK,
                                    VectorDistanceThreshold = vrs.VectorDistanceThreshold
                                }
                            }
                        });
                    }
                }
                genAiConfig.Tools = toolsList;
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var createdCache = await _genAiClient.Caches.CreateAsync(_model.Model, genAiConfig);

            return new CacheMetadata
            {
                CacheName = createdCache.Name,
                ExpireTime = now + config.TtlSeconds,
                Fingerprint = GenerateCacheFingerprint(request, cacheContentsCount),
                InvocationsUsed = 1,
                ContentsCount = cacheContentsCount,
                CreatedAt = now
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GeminiContextCacheManager] Failed to create cache: {ex.Message}");
            return null;
        }
    }

    private async Task CleanupCacheAsync(string cacheName)
    {
        try
        {
            await _genAiClient.Caches.DeleteAsync(cacheName);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GeminiContextCacheManager] Failed to cleanup cache: {ex.Message}");
        }
    }

    private void ApplyCacheToRequest(LlmRequest request, string? cacheName, int cacheContentsCount)
    {
        if (request.Config != null)
        {
            request.Config.SystemInstruction = null;
            request.Config.Tools = null;
        }

        _model.CachedContent = new GenerativeAI.Types.CachedContent { Name = cacheName, Model = _model.Model };

        if (request.Contents != null && request.Contents.Count >= cacheContentsCount)
        {
            request.Contents = request.Contents.Skip(cacheContentsCount).ToList();
        }
    }

    private List<Google.GenAI.Types.Content> MapToGenAiContents(IEnumerable<GoogleAdk.Core.Abstractions.Models.Content> source)
    {
        var list = new List<Google.GenAI.Types.Content>();
        foreach (var c in source)
        {
            var targetContent = new Google.GenAI.Types.Content { Role = c.Role, Parts = new List<Google.GenAI.Types.Part>() };
            if (c.Parts != null)
            {
                foreach (var p in c.Parts)
                {
                    if (p.Text != null) 
                    {
                        targetContent.Parts.Add(new Google.GenAI.Types.Part { Text = p.Text });
                    }
                    else if (p.InlineData != null)
                    {
                        targetContent.Parts.Add(new Google.GenAI.Types.Part
                        {
                            InlineData = new Google.GenAI.Types.Blob
                            {
                                MimeType = p.InlineData.MimeType,
                                Data = Convert.FromBase64String(p.InlineData.Data)
                            }
                        });
                    }
                }
            }
            list.Add(targetContent);
        }
        return list;
    }
}