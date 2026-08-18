using Esox.SharpAndRusty.ObjectPool.CircuitBreaker;
using Esox.SharpAndRusty.ObjectPool.Eviction;
using Esox.SharpAndRusty.ObjectPool.Policies;
using Esox.SharpAndRusty.Types;

namespace Esox.SharpAndRusty.ObjectPool.Models;
/// <summary>
/// Configuration settings for an object pool, including size limits, timeouts, validation, eviction, circuit breaker, and lifecycle hooks.
/// </summary>
/// <typeparam name="T">The type of objects managed by the pool</typeparam>
public class PoolConfiguration<T>
{
    /// <summary>
    /// Gets or sets the maximum number of objects allowed in the pool.
    /// </summary>
    public int MaxPoolSize { get; set; } = int.MaxValue;

    /// <summary>
    /// Gets or sets the maximum number of active objects allowed in the pool.
    /// </summary>
    public int MaxActiveObjects { get; set; } = int.MaxValue;

    /// <summary>
    /// Gets or sets the default timeout for pool operations.
    /// </summary>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);
    
    /// <summary>
    /// Gets or sets a value indicating whether objects should be validated when returned to the pool.
    /// </summary>  
    public bool ValidateOnReturn { get; set; } = false;

    /// <summary>
    /// Gets or sets a function to validate objects before they are returned to the pool. The function should return an ExtendedResult indicating success or failure.
    /// </summary>
    public Func<T,ExtendedResult<Unit, Error>>? ValidationFunction { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether detailed statistics should be enabled.
    /// </summary>
    public bool EnableDetailedStatistics { get; set; } = true;

    /// <summary>
    /// Gets or sets the eviction configuration for the pool.
    /// </summary>
    public EvictionConfiguration? EvictionConfiguration { get; set; }

    /// <summary>
    /// Gets or sets the circuit breaker configuration for the pool.
    /// </summary>
    public CircuitBreakerConfiguration? CircuitBreakerConfiguration { get; set; }

    /// <summary>
    /// Gets or sets the lifecycle hooks for the pool, which can be used to execute custom logic during object creation, retrieval, and return.
    /// </summary>
    public object? LifecycleHooks { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the pool should continue operating even if a lifecycle hook fails. If set to true, the pool will ignore errors in lifecycle hooks; if false, it will propagate the error.
    /// </summary>
    public bool ContinueOnLifecycleHookError { get; set; } = true;

    /// <summary>
    /// Gets or sets the pooling policy type for the pool, which determines how objects are managed and retrieved from the pool (e.g., LIFO, FIFO, priority-based).
    /// </summary>
    public PoolingPolicyType PoolingPolicyType { get; set; } = PoolingPolicyType.Lifo;

    /// <summary>
    /// Gets or sets a priority selector function for the pool, which is used to determine the priority of objects when using a priority-based pooling policy. The function should return an integer representing the priority of an object (higher values indicate higher priority).
    /// </summary>
    public object? PrioritySelector { get; set; }

    /// <summary>
    /// Gets or sets an asynchronous validation function for objects before they are returned to the pool. The function should return a ValueTask containing an ExtendedResult indicating success or failure.
    /// </summary>
    public Func<T,ValueTask<ExtendedResult<Unit,Error>>>? AsyncValidationFunction { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether asynchronous disposal should be used for objects that implement IAsyncDisposable. If set to true, the pool will use asynchronous disposal; if false, it will use synchronous disposal.
    /// </summary>
    public bool UseAsyncDisposal { get; set; } = true;
}