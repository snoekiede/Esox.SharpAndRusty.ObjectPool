namespace Esox.SharpAndRusty.ObjectPool.Eviction;

/// <summary>
/// Defines the eviction policy for objects in the pool
/// </summary>
public enum EvictionPolicy
{
    /// <summary>
    /// No eviction; objects will remain in the pool indefinitely unless explicitly removed
    /// </summary>
    None = 0,
    /// <summary>
    /// Evict objects based on their time-to-live (TTL) expiration
    /// </summary>
    TimeToLive = 1,
    /// <summary>
    /// Evict objects based on their idle timeout (time since last access)
    /// </summary>
    IdleTimeout = 2,

    /// <summary>
    /// Evict objects based on both TTL and idle timeout; whichever condition is met first will trigger eviction
    /// </summary>
    Combined = 3
}

/// <summary>
/// Configuration settings for eviction behavior in the object pool
/// </summary>
public class EvictionConfiguration
{
    /// <summary>
    /// The eviction policy to apply to objects in the pool
    /// </summary>
    public EvictionPolicy Policy { get; set; } = EvictionPolicy.None;

    /// <summary>
    /// The time-to-live (TTL) duration for objects in the pool
    /// </summary>
    public TimeSpan TimeToLive { get; set; } = TimeSpan.FromMinutes(30);
    
    /// <summary>
    /// The idle timeout duration for objects in the pool
    /// </summary>
    public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The interval at which eviction runs are performed
    /// </summary>
    public TimeSpan EvictionInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Whether to enable background eviction
    /// </summary>
    public bool EnableBackgroundEviction { get; set; } = true;

    /// <summary>
    /// The maximum number of evictions to perform per run
    /// </summary>
    public int MaxEvictionsPerRun { get; set; } = int.MaxValue;

    /// <summary>
    /// Optional custom eviction predicate
    /// </summary>
    public Func<object, ObjectMetadata, bool>? CustomEvictionPredicate { get; set; }

    /// <summary>
    /// Whether to dispose evicted objects if they implement IDisposable
    /// </summary>
    public bool DisposeEvictedObjects { get; set; } = true;
}
/// <summary>
/// Metadata associated with an object in the pool, used for eviction decisions
/// </summary>
public class ObjectMetadata
{
    /// <summary>
    /// The time when the object was created or added to the pool
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The time when the object was last accessed
    /// </summary>
    public DateTime? LastAccessedAt { get; set; }

    /// <summary>
    /// The time when the object was last returned to the pool
    /// </summary>
    public DateTime? LastReturnedAt { get; set; }

    /// <summary>
    /// The number of times the object has been accessed
    /// </summary>
    public int AccessCount { get; set; }

    /// <summary>
    /// A collection of tags associated with the object
    /// </summary>
    public Dictionary<string, object> Tags { get; set; } = new();

    /// <summary>
    /// The age of the object since it was created
    /// </summary>
    public TimeSpan Age => DateTime.UtcNow - CreatedAt;

    /// <summary>
    /// The idle time of the object since it was last accessed
    /// </summary>
    public TimeSpan IdleTime => LastAccessedAt.HasValue ? DateTime.UtcNow - LastAccessedAt.Value : Age;

    /// <summary>
    /// Whether the object has been accessed at least once
    /// </summary>
    public bool HasBeenAccessed => LastAccessedAt.HasValue;
}

/// <summary>
/// Statistics related to eviction operations in the object pool
/// </summary>
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
