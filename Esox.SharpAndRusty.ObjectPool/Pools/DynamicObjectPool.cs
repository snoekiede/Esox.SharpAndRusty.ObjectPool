using Esox.SharpAndRusty.ObjectPool.CircuitBreaker;
using Esox.SharpAndRusty.ObjectPool.Constants;
using Esox.SharpAndRusty.ObjectPool.Eviction;
using Esox.SharpAndRusty.ObjectPool.Lifecycle;
using Esox.SharpAndRusty.ObjectPool.Models;
using Esox.SharpAndRusty.ObjectPool.Warmup;
using Esox.SharpAndRusty.Types;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Esox.SharpAndRusty.ObjectPool.Pools;
/// <summary>
/// A dynamic object pool that supports on-demand object creation, eviction policies, circuit breaker protection, and lifecycle hooks. This pool allows for flexible management of pooled objects, including warming up the pool to a desired size or percentage of capacity.
/// </summary>
/// <typeparam name="T">The type of the objects being pooled</typeparam>
public class DynamicObjectPool<T> : ObjectPool<T>, IObjectPoolWarmer<T> where T : class
{
    /// <summary>
    /// The factory method to be used to create new objects
    /// </summary>
    private readonly Func<T>? _factory;

    /// <summary>
    /// Warm-up status tracking
    /// </summary>
    private WarmupStatus _warmupStatus = new();

    /// <summary>
    /// Eviction manager for TTL and idle timeout support
    /// </summary>
    private EvictionManager<T>? _evictionManager;

    /// <summary>
    /// Timer for periodic eviction checks
    /// </summary>
    private Timer? _evictionCheckTimer;

    /// <summary>
    /// Circuit breaker for protecting against cascading failures
    /// </summary>
    private CircuitBreaker.CircuitBreaker? _circuitBreaker;

    /// <summary>
    /// Lifecycle hook manager for object lifecycle events
    /// </summary>
    private LifecycleHookManager<T>? _lifecycleHookManager;

    /// <summary>
    /// The constructor for the queryable object pool
    /// </summary>
    /// <param name="initialObjects">the initial objects</param>
    public DynamicObjectPool(List<T> initialObjects) : base(initialObjects)
    {
        InitializeEviction();
        InitializeCircuitBreaker();
        InitializeLifecycleHooks();
    }

    /// <summary>
    /// The constructor for the dynamic object pool
    /// </summary>
    /// <param name="factory">creation function for new objects</param>
    public DynamicObjectPool(Func<T> factory) : base([])
    {
        this._factory = factory;
        InitializeEviction();
        InitializeCircuitBreaker();
        InitializeLifecycleHooks();
    }

    /// <summary>
    /// The constructor for the dynamic object pool
    /// </summary>
    /// <param name="factory">creation function for new objects</param>
    /// <param name="initialObjects">list of initial objects</param>
    public DynamicObjectPool(Func<T> factory, List<T> initialObjects) : base(initialObjects)
    {
        this._factory = factory;
        InitializeEviction();
        InitializeCircuitBreaker();
        InitializeLifecycleHooks();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicObjectPool{T}"/> class with a specified factory method,
    /// an initial collection of objects, optional pool configuration, and an optional logger.
    /// </summary>
    /// <remarks>The <paramref name="factory"/> parameter is required and must not be null. The
    /// <paramref name="initialObjects"/> list can be empty, but it must not be null. If <paramref
    /// name="configuration"/> is provided, it will override the default pool behavior. The logger can be used to
    /// monitor pool activity, such as object creation and disposal.</remarks>
    /// <param name="factory">A function that creates new instances of the pooled object. This function is invoked when the pool needs to
    /// create additional objects.</param>
    /// <param name="initialObjects">A list of pre-created objects to populate the pool initially. These objects will be available for reuse
    /// immediately.</param>
    /// <param name="configuration">Optional configuration settings for the object pool, such as maximum pool size and eviction policies. If
    /// null, default settings are used.</param>
    /// <param name="logger">An optional logger instance for logging pool-related events. If null, no logging will be performed.</param>
    public DynamicObjectPool(Func<T> factory, List<T> initialObjects, PoolConfiguration<T>? configuration, ILogger<ObjectPool<T>>? logger = null) : base(initialObjects, configuration, logger)
    {
        this._factory = factory;
        InitializeEviction();
        InitializeCircuitBreaker();
        InitializeLifecycleHooks();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicObjectPool{T}"/> class with the specified factory
    /// method, configuration, and optional logger.
    /// </summary>
    /// <param name="factory">A function that creates new instances of the pooled object. This function is called when the pool needs to
    /// allocate a new object.</param>
    /// <param name="configuration">An optional <see cref="PoolConfiguration{T}"/> object that specifies the settings for the object pool, such as
    /// maximum size and eviction policies. If null, default settings are used.</param>
    /// <param name="logger">An optional <see cref="ILogger{TCategoryName}"/> instance used to log diagnostic information about the
    /// pool's behavior. If null, no logging is performed.</param>
    public DynamicObjectPool(Func<T> factory, PoolConfiguration<T>? configuration, ILogger<ObjectPool<T>>? logger = null) : base([], configuration, logger)
    {
        this._factory = factory;
        InitializeEviction();
        InitializeCircuitBreaker();
        InitializeLifecycleHooks();
    }

    private void InitializeCircuitBreaker()
    {
        if (Configuration.CircuitBreakerConfiguration != null)
        {
            _circuitBreaker = new CircuitBreaker.CircuitBreaker(
                Configuration.CircuitBreakerConfiguration,
                Logger);

            Logger?.LogInformation(
                "Circuit breaker enabled: FailureThreshold={Threshold}, OpenDuration={Duration}",
                Configuration.CircuitBreakerConfiguration.FailureThreshold,
                Configuration.CircuitBreakerConfiguration.OpenDuration);
        }
    }

    private void InitializeLifecycleHooks()
    {
        if (Configuration.LifecycleHooks is LifecycleHooks<T> hooks)
        {
            _lifecycleHookManager = new LifecycleHookManager<T>(
                hooks,
                Configuration.ContinueOnLifecycleHookError,
                (ex, hookName) => Logger?.LogError(ex, "Error executing lifecycle hook: {HookName}", hookName));

            Logger?.LogInformation("Lifecycle hooks enabled");
        }
    }

    private void InitializeEviction()
    {
        if (Configuration.EvictionConfiguration != null &&
            Configuration.EvictionConfiguration.Policy != EvictionPolicy.None)
        {
            _evictionManager = new EvictionManager<T>(Configuration.EvictionConfiguration, Logger);

            // Track initial objects
            foreach (var obj in AvailableObjects)
            {
                _evictionManager.TrackObject(obj);
            }

            // Set up periodic eviction checks
            if (Configuration.EvictionConfiguration.EnableBackgroundEviction)
            {
                _evictionCheckTimer = new Timer(
                    PerformEvictionCheck,
                    null,
                    Configuration.EvictionConfiguration.EvictionInterval,
                    Configuration.EvictionConfiguration.EvictionInterval);

                Logger?.LogInformation(
                    "Eviction enabled with policy: {Policy}, TTL: {TTL}, Idle: {Idle}",
                    Configuration.EvictionConfiguration.Policy,
                    Configuration.EvictionConfiguration.TimeToLive,
                    Configuration.EvictionConfiguration.IdleTimeout);
            }
        }


    }

    private void PerformEvictionCheck(object? state)
    {
        if (_evictionManager == null || Disposed) return;

        try
        {
            var objectsToCheck = AvailableObjects.ToArray();
            _evictionManager.RunEviction(objectsToCheck, obj =>
            {
                // Execute eviction hook before removing
                _lifecycleHookManager?.ExecuteOnEvict(obj, EvictionReason.TimeToLive);

                // Remove from available stack
                if (AvailableObjects.TryPop(out var removed) && EqualityComparer<T>.Default.Equals(removed, obj))
                {
                    Logger?.LogDebug("Evicted object from pool");

                    // Execute dispose hook if object will be disposed
                    if (Configuration.EvictionConfiguration?.DisposeEvictedObjects == true &&
                        obj is IDisposable)
                    {
                        _lifecycleHookManager?.ExecuteOnDispose(obj);
                    }
                }
                else
                {
                    // Put it back if it wasn't the one we wanted to remove
                    if (removed != null)
                    {
                        AvailableObjects.Push(removed);
                    }
                }
            });
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Logger?.LogError(ex, "Error during eviction check");
        }
    }

    /// <summary>
    /// Returns an object from the pool. If no objects are available, an exception is thrown.
    /// </summary>
    /// <returns>A Result wrapping the pooled object</returns>
    public override ExtendedResult<PoolModel<T>, Error> GetObject()
    {
        if (Disposed)
        {
            return Error.New("Object has been disposed");
        }

        if (_circuitBreaker is not null)
        {
            try
            {
                return _circuitBreaker.Execute(GetObjectInternal);
            }
            catch (CircuitBreakerOpenException)
            {
                return Error.New("Circuit breaker is open");
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Error creating new object");
                return Error.New(PoolConstants.Messages.CannotCreateObject);
            }
        }

        try
        {
            return GetObjectInternal();
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Error creating new object");
            return Error.New(PoolConstants.Messages.CannotCreateObject);
        }
    }

    private ExtendedResult<PoolModel<T>, Error> GetObjectInternal()
    {
        if (this.ActiveObjects.Count >= Configuration.MaxActiveObjects)
        {
            return Error.New(String.Format(PoolConstants.Messages.MaxActiveLimitFormat,
                Configuration.MaxActiveObjects));
        }

        T? result = null;
        bool found = false;

        while (this.AvailableObjects.TryPop(out result))
        {
            if (_evictionManager is not null && _evictionManager.ShouldEvict(result))
            {
                Logger?.LogDebug("Object expired, trying to evict");

                _lifecycleHookManager?.ExecuteOnEvict(result, EvictionReason.TimeToLive);

                _evictionManager.UntrackObject(result);

                if (Configuration.EvictionConfiguration?.DisposeEvictedObjects == true && result is IDisposable disposable)
                {
                    try
                    {
                        _lifecycleHookManager?.ExecuteOnDispose(result);
                        disposable.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Logger?.LogError(ex, "Error disposing object");
                    }
                }

                continue;
            }

            found = true;
            break;
        }

        if (found && result is not null)
        {
            this.ActiveObjects.TryAdd(result, 0);
            _evictionManager?.RecordAccess(result);

            _lifecycleHookManager?.ExecuteOnAcquire(result);

            statistics.IncrementRetrieved();
            statistics.CurrentActiveObjects = this.ActiveObjects.Count;
            statistics.CurrentAvailableObjects = this.AvailableObjects.Count;
            statistics.UpdatePeakIfHigher(statistics.CurrentActiveObjects);
            return new PoolModel<T>(result, this);

        }

        if (this._factory is null)
        {
            statistics.IncrementPoolEmpty();
            Logger?.LogWarning(PoolConstants.Messages.CannotCreateObject);
            return Error.New(PoolConstants.Messages.CannotCreateObject);
        }

        T? newObject = this._factory.Invoke();

        if (newObject is null)
        {
            return Error.New(PoolConstants.Messages.CannotCreateObject);
        }

        _lifecycleHookManager?.ExecuteOnCreate(newObject);

        // Track the new object for eviction
        _evictionManager?.TrackObject(newObject);
        _evictionManager?.RecordAccess(newObject);

        // Execute acquire hook
        _lifecycleHookManager?.ExecuteOnAcquire(newObject);

        // Add directly to active objects without pushing to available first
        this.ActiveObjects.TryAdd(newObject, 0);
        statistics.IncrementRetrieved();
        statistics.CurrentActiveObjects = this.ActiveObjects.Count;
        statistics.CurrentAvailableObjects = this.AvailableObjects.Count;
        statistics.UpdatePeakIfHigher(statistics.CurrentActiveObjects);

        Logger?.LogDebug("Created new object dynamically. Active: {Active}, Available: {Available}",
            ActiveObjects.Count, AvailableObjects.Count);

        return new PoolModel<T>(newObject, this);
    }


    /// <summary>
    /// Gets lifecycle hook statistics
    /// </summary>
    public LifecycleHookStatistics? GetLifecycleHookStatistics()
    {
        return _lifecycleHookManager?.GetStatistics();
    }
    /// <summary>
    /// Returns an object to the pool
    /// </summary>
    public new ExtendedResult<Unit, Error> ReturnObject(PoolModel<T> obj)
    {
        if (Disposed)
        {
            return Error.New("Object has been disposed");
        }

        var unwrapped = obj.Unwrap();

        if (!this.ActiveObjects.TryRemove(unwrapped, out _))
        {
            Logger?.LogWarning(PoolConstants.Messages.ObjectNotInActiveList);
            return Error.New(PoolConstants.Messages.ObjectNotInPool);
        }

        _lifecycleHookManager?.ExecuteOnReturn(unwrapped);

        // Validate object if configured
        if (Configuration is { ValidateOnReturn: true, ValidationFunction: not null })
        {
            ExtendedResult<Unit, Error> valid;
            try
            {
                valid = Configuration.ValidationFunction(unwrapped);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Logger?.LogError(ex, "Validation function threw; discarding object");
                valid = ExtendedResult<Unit, Error>.Err(Error.New(PoolConstants.Messages.ValidationFailed));
            }

            if (valid.IsFailure)
            {
                Logger?.LogWarning(PoolConstants.Messages.ValidationFailed);
                _lifecycleHookManager?.ExecuteOnValidationFailed(unwrapped);
                statistics.IncrementReturned();
                statistics.CurrentActiveObjects = this.ActiveObjects.Count;
                statistics.CurrentAvailableObjects = this.AvailableObjects.Count;
                return Error.New(PoolConstants.Messages.ValidationFailed);
            }
        }

        // Check if we're exceeding pool size limit
        if (this.AvailableObjects.Count >= Configuration.MaxPoolSize)
        {
            Logger?.LogDebug(PoolConstants.Messages.PoolAtMaxSize);
            statistics.IncrementReturned();
            statistics.CurrentActiveObjects = this.ActiveObjects.Count;
            statistics.CurrentAvailableObjects = this.AvailableObjects.Count;
            return Error.New(PoolConstants.Messages.PoolAtMaxSize);
        }

        // Record return for eviction tracking
        _evictionManager?.RecordReturn(unwrapped);

        this.AvailableObjects.Push(unwrapped);
        _availabilitySignal.Release();

        statistics.IncrementReturned();
        statistics.CurrentActiveObjects = this.ActiveObjects.Count;
        statistics.CurrentAvailableObjects = this.AvailableObjects.Count;

        Logger?.LogDebug(PoolConstants.Messages.ObjectReturnedToPoolActiveAvailable,
            ActiveObjects.Count, AvailableObjects.Count);
        
        return Unit.Value;

    }

    /// <summary>
    /// Gets eviction statistics
    /// </summary>
    public EvictionStatistics? GetEvictionStatistics()
    {
        return _evictionManager?.GetStatistics();
    }

    /// <summary>
    /// Gets circuit breaker statistics
    /// </summary>
    public CircuitBreakerStatistics? GetCircuitBreakerStatistics()
    {
        return _circuitBreaker?.GetStatistics();
    }

    /// <summary>
    /// Manually triggers an eviction check
    /// </summary>
    public void TriggerEviction()
    {
        PerformEvictionCheck(null);
    }


    /// <summary>
    /// Manually resets the circuit breaker
    /// </summary>
    public void ResetCircuitBreaker()
    {
        _circuitBreaker?.Reset();
        Logger?.LogInformation("Circuit breaker manually reset");
    }

    /// <summary>
    /// Manually trips (opens) the circuit breaker
    /// </summary>
    public void TripCircuitBreaker()
    {
        _circuitBreaker?.Trip();
        Logger?.LogWarning("Circuit breaker manually tripped");
    }

    /// <summary>
    /// Warms up the pool by creating objects up to the target size.
    /// </summary>
    /// <param name="targetSize">The desired number of objects in the pool after warm-up.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An <see cref="ExtendedResult{Unit, Error}"/> indicating success or failure.</returns>
    public async Task<ExtendedResult<Unit,Error>> WarmUpAsync(int targetSize, CancellationToken cancellationToken = default)
    {
        if (_factory is null)
        {
            Logger?.LogWarning("Cannot warm up pool: no factory method provided");
            _warmupStatus.Errors.Add("No factory method available");
            return Error.New("No factory method available");
        }

        var stopwatch = Stopwatch.StartNew();
        var currentSize = AvailableObjects.Count;
        var objectsToCreate = Math.Min(targetSize - currentSize, Configuration.MaxPoolSize - currentSize);

        if (objectsToCreate <= 0)
        {
            Logger?.LogInformation("Pool already at target size: {CurrentSize}/{TargetSize}", currentSize, targetSize);
            _warmupStatus.IsWarmedUp = true;
            _warmupStatus.ObjectsCreated = 0;
            _warmupStatus.TargetSize = targetSize;
            return Error.New("Pool already at target size");
        }

        _warmupStatus = new WarmupStatus
        {
            TargetSize = targetSize,
            ObjectsCreated = 0
        };

        Logger?.LogInformation("Starting pool warm-up: creating {Count} objects", objectsToCreate);

        var batchSize = Math.Min(Environment.ProcessorCount * 2, objectsToCreate);
        var tasks = new List<Task<T?>>();

        try
        {
            for (int i = 0; i < objectsToCreate && !cancellationToken.IsCancellationRequested; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        // Use circuit breaker protection during warm-up
                        if (_circuitBreaker != null)
                        {
                            return _circuitBreaker.Execute(() => _factory.Invoke());
                        }
                        return _factory.Invoke();
                    }
                    catch (Exception ex) when (ex is not OutOfMemoryException)
                    {
                        Logger?.LogError(ex, "Error creating object during warm-up");
                        _warmupStatus.Errors.Add($"Factory error: {ex.Message}");
                        return null;
                    }
                }, cancellationToken));

                // Process in batches to avoid overwhelming the system
                if (tasks.Count >= batchSize || i == objectsToCreate - 1)
                {
                    var results = await Task.WhenAll(tasks).ConfigureAwait(false);

                    foreach (var newObj in results.Where(o => o != null))
                    {
                        AvailableObjects.Push(newObj!);
                        _evictionManager?.TrackObject(newObj!);
                        _warmupStatus.ObjectsCreated++;
                    }

                    tasks.Clear();
                }
            }
        }
        catch (OperationCanceledException)
        {
            Logger?.LogInformation("Pool warm-up cancelled after creating {Created} objects", _warmupStatus.ObjectsCreated);
            return Error.New("Pool warm-up cancelled");
        }
        catch (CircuitBreakerOpenException ex)
        {
            Logger?.LogWarning("Pool warm-up interrupted by circuit breaker: {Message}", ex.Message);
            _warmupStatus.Errors.Add($"Circuit breaker open: {ex.Message}");
            return Error.New($"Circuit breaker open: {ex.Message}");
        }

        stopwatch.Stop();
        _warmupStatus.WarmupDuration = stopwatch.Elapsed;
        _warmupStatus.IsWarmedUp = true;
        _warmupStatus.CompletedAt = DateTime.UtcNow;

        Logger?.LogInformation(
            "Pool warm-up completed: created {Created}/{Target} objects in {Duration}ms",
            _warmupStatus.ObjectsCreated,
            targetSize,
            stopwatch.ElapsedMilliseconds);
        return Unit.Value;
    }

    /// <summary>
    /// Warms up the pool to a percentage of maximum capacity
    /// </summary>
    public async Task<ExtendedResult<Unit,Error>> WarmUpToPercentageAsync(double targetPercentage, CancellationToken cancellationToken = default)
    {
        if (targetPercentage < 0 || targetPercentage > 100)
        {
            return Error.New("Target percentage must be between 0 and 100");
        }

        var maxSize = Configuration.MaxPoolSize == int.MaxValue
            ? 100  // Default to 100 if unlimited
            : Configuration.MaxPoolSize;

        var targetSize = (int)Math.Ceiling(maxSize * (targetPercentage / 100.0));
        var warmUpResult = await WarmUpAsync(targetSize, cancellationToken);
        return warmUpResult;
    }

    /// <summary>
    /// Gets the current warm-up status
    /// </summary>
    public WarmupStatus GetWarmupStatus()
    {
        return _warmupStatus;
    }

    /// <summary>
    /// Disposes the pool and releases resources
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing && !Disposed)
        {
            _evictionCheckTimer?.Dispose();
            _evictionManager?.Dispose();
            _circuitBreaker?.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Asynchronously disposes the pool, including eviction timer and supporting services.
    /// </summary>
    public override async ValueTask DisposeAsync()
    {
        if (!Disposed)
        {
            _evictionCheckTimer?.Dispose();
            _evictionManager?.Dispose();
            _circuitBreaker?.Dispose();
        }

        await base.DisposeAsync().ConfigureAwait(false);
    }
}


