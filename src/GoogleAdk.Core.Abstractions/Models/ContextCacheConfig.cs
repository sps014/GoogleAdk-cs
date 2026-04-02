namespace GoogleAdk.Core.Abstractions.Models;

public sealed class ContextCacheConfig
{
    public int CacheIntervals { get; set; } = 10;
    public int TtlSeconds { get; set; } = 1800;
    public int MinTokens { get; set; } = 0;
    
    public string TtlString => $"{TtlSeconds}s";
    
    public override string ToString()
    {
        return $"ContextCacheConfig(CacheIntervals={CacheIntervals}, Ttl={TtlString}, MinTokens={MinTokens})";
    }
}
