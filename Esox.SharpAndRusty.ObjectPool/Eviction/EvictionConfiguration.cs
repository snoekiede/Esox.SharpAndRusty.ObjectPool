using System;
using System.Collections.Generic;
using System.Text;

namespace Esox.SharpAndRusty.ObjectPool.Eviction;


public enum EvictionPolicy
{
    None = 0,

    TimeToLive = 1,

    IdleTimeOut = 2,

    Combined = 3
}

public class EvictionConfiguration
{
    public EvictionPolicy Policy { get; set; } = EvictionPolicy.None;

    public TimeSpan TimeToLive { get; set; } = TimeSpan.FromMinutes(30);

    public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(5);

    public TimeSpan EvictionInterval { get; set; } = TimeSpan.FromMinutes(1);

    public bool EnableBackgroundEviction { get; set; } = true;

    public int MaxEvictionsPerRun { get; set; } = int.MaxValue;


}

public class ObjectMetadata
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastAccessedAt { get; set; }

    public DateTime? LastReturnedAt { get; set; }

    public int AccessCount { get; set; }

    public Dictionary<string, object> Tags { get; set; } = [];

    public TimeSpan Age => DateTime.UtcNow - CreatedAt;

    public TimeSpan IdleTime => LastAccessedAt.HasValue ? DateTime.UtcNow - LastAccessedAt.Value : Age;

    public bool HasBeenAccessed => LastAccessedAt.HasValue;
}

public class EvictionStatistics
{
    /// <summary>
    /// Total number of objects evicted
    /// </summary>
    public long TotalEvictions { get; set; }

    /// <summary>
    /// Number of evictions due to TTL expiration
    /// </summary>
    public long TtlEvictions { get; set; }

    /// <summary>
    /// Number of evictions due to idle timeout
    /// </summary>
    public long IdleEvictions { get; set; }

    /// <summary>
    /// Number of evictions due to custom predicate
    /// </summary>
    public long CustomEvictions { get; set; }

    /// <summary>
    /// When the last eviction run occurred
    /// </summary>
    public DateTime? LastEvictionRun { get; set; }

    /// <summary>
    /// Duration of the last eviction run
    /// </summary>
    public TimeSpan LastEvictionDuration { get; set; }

    /// <summary>
    /// Number of eviction runs
    /// </summary>
    public long EvictionRuns { get; set; }

    /// <summary>
    /// Average evictions per run
    /// </summary>
    public double AverageEvictionsPerRun => EvictionRuns > 0
        ? (double)TotalEvictions / EvictionRuns
        : 0;
}
