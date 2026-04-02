using System.Text.Json.Serialization;

namespace GoogleAdk.Core.Abstractions.Models;

/// <summary>
/// Metadata for context cache associated with LLM responses.
/// </summary>
public class CacheMetadata
{
    /// <summary>Unique fingerprint of the cache contents.</summary>
    [JsonPropertyName("fingerprint")]
    public string Fingerprint { get; set; } = string.Empty;

    /// <summary>Resource name of the created cache.</summary>
    [JsonPropertyName("cache_name")]
    public string? CacheName { get; set; }

    /// <summary>Time when the cache expires (Unix timestamp in seconds).</summary>
    [JsonPropertyName("expire_time")]
    public double ExpireTime { get; set; }

    /// <summary>Time when the cache was created (Unix timestamp in seconds).</summary>
    [JsonPropertyName("created_at")]
    public double CreatedAt { get; set; }

    /// <summary>Number of times this cache has been used for invocations.</summary>
    [JsonPropertyName("invocations_used")]
    public int InvocationsUsed { get; set; }

    /// <summary>Number of contents included in this cache.</summary>
    [JsonPropertyName("contents_count")]
    public int ContentsCount { get; set; }
    
    public CacheMetadata Clone()
    {
        return new CacheMetadata
        {
            Fingerprint = Fingerprint,
            CacheName = CacheName,
            ExpireTime = ExpireTime,
            CreatedAt = CreatedAt,
            InvocationsUsed = InvocationsUsed,
            ContentsCount = ContentsCount
        };
    }

    public override string ToString() => 
        $"CacheMetadata(Name={CacheName}, Fingerprint={Fingerprint}, Used={InvocationsUsed}, Contents={ContentsCount})";
}
