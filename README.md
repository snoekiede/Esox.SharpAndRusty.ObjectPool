# Esox.SharpAndRusty.ObjectPool

A thread-safe, production-ready generic object pool for **.NET 8, .NET 9, and .NET 10** built on a Result-based API (no unexpected exceptions from pool operations). Supports dependency injection, circuit breaking, eviction, lifecycle hooks, OpenTelemetry metrics, health checks, scoped pools, pooling policies, and automatic warm-up.

> ⚠️ **Disclaimer** — see [Disclaimer](#disclaimer) at the bottom of this file.

---

## Table of Contents

- [Installation](#installation)
- [Quick Start](#quick-start)
- [Pool Types](#pool-types)
- [Dependency Injection](#dependency-injection)
- [Configuration](#configuration)
- [Circuit Breaker](#circuit-breaker)
- [Eviction](#eviction)
- [Lifecycle Hooks](#lifecycle-hooks)
- [Warm-Up](#warm-up)
- [Health Checks](#health-checks)
- [OpenTelemetry Metrics](#opentelemetry-metrics)
- [Scoped Pools](#scoped-pools)
- [Pooling Policies](#pooling-policies)
- [Async Operations](#async-operations)
- [Result-Based API](#result-based-api)
- [Disclaimer](#disclaimer)

---

## Installation

```bash
dotnet add package Esox.SharpAndRusty.ObjectPool
```

Targets: `net8.0` · `net9.0` · `net10.0`

---

## Quick Start

### Standalone (no DI)

```csharp
// Fixed-size pool from a pre-created list
var pool = new ObjectPool<MyResource>(new List<MyResource>
{
    new MyResource(),
    new MyResource(),
    new MyResource()
});

var result = pool.GetObject();

result.Match(
    poolModel =>
    {
        using (poolModel)           // Dispose() returns the object to the pool
        {
            poolModel.Unwrap().DoWork();
        }
    },
    error => Console.WriteLine($"Pool unavailable: {error.Message}")
);
```

### Dynamic pool (objects created on demand)

```csharp
var pool = new DynamicObjectPool<HttpClient>(() => new HttpClient());

var result = pool.GetObject();
if (result.IsSuccess)
{
    using var model = result.Unwrap();
    await model.Unwrap().GetAsync("https://example.com");
}
```

---

## Pool Types

| Type | Description |
|---|---|
| `ObjectPool<T>` | Fixed-size pool. Objects are provided at construction. |
| `DynamicObjectPool<T>` | Grows on demand using a factory. Supports circuit breaking, eviction, and warm-up. |
| `QueryableObjectPool<T>` | Extends `DynamicObjectPool<T>` with predicate-based object selection. |

---

## Dependency Injection

Register pools in your `IServiceCollection`:

```csharp
// Standard pool with fluent builder
services.AddObjectPool<DbConnection>(builder => builder
    .WithFactory(() => new SqlConnection(connectionString))
    .WithMaxSize(50)
    .WithMaxActiveObjects(20)
    .WithDefaultTimeout(TimeSpan.FromSeconds(5)));

// Dynamic pool with IServiceProvider access
services.AddDynamicObjectPool<IDbConnectionFactory>(
    sp => sp.GetRequiredService<IDbConnectionFactory>().Create(),
    config => config.MaxPoolSize = 100);

// Queryable pool
services.AddQueryableObjectPool<Car>(builder => builder
    .WithInitialObjects(carList)
    .AsQueryable());

// Multiple pools at once
services.AddObjectPools(pools =>
{
    pools.AddPool<HttpClient>(b => b.WithFactory(() => new HttpClient()));
    pools.AddDynamicPool<DbConnection>(sp => new SqlConnection(cs));
});
```

Resolve from DI:

```csharp
public class MyService(IObjectPool<DbConnection> pool)
{
    public async Task DoWorkAsync()
    {
        var result = pool.GetObject();
        result.Match(
            model => { using (model) model.Unwrap().Execute(); },
            err   => logger.LogError(err.Message)
        );
    }
}
```

---

## Configuration

```csharp
services.AddObjectPool<MyResource>(builder => builder
    .WithFactory(() => new MyResource())
    .WithMaxSize(100)                               // max objects in pool
    .WithMaxActiveObjects(50)                       // max concurrently checked-out objects
    .WithDefaultTimeout(TimeSpan.FromSeconds(10))   // async wait timeout
    .WithValidation(obj => obj.IsHealthy())         // validate on return
    .WithHealthChecks()                             // register IPoolHealth
    .AsQueryable());                                // use QueryableObjectPool<T>
```

---

## Circuit Breaker

Protect factory calls from cascading failures:

```csharp
services.AddDynamicObjectPool<HttpClient>(sp => CreateClient())
    .WithCircuitBreaker(
        failureThreshold: 5,
        openDuration:     TimeSpan.FromSeconds(30))
    .WithCircuitBreakerPercentage(
        failurePercentageThreshold: 50.0,
        minimumThroughput: 20);
```

Manual control:

```csharp
var pool = provider.GetRequiredService<DynamicObjectPool<HttpClient>>();
pool.TripCircuitBreaker();   // force open
pool.ResetCircuitBreaker();  // force closed

var stats = pool.GetCircuitBreakerStatistics();
Console.WriteLine(stats.State);              // Closed | Open | HalfOpen
Console.WriteLine(stats.FailurePercentage);
```

---

## Eviction

Automatically remove stale or idle objects:

```csharp
// Builder-level (recommended)
services.AddDynamicObjectPool<DbConnection>(sp => CreateConnection())
    .WithTimeToLive(TimeSpan.FromMinutes(30))
    .WithIdleTimeout(TimeSpan.FromMinutes(5))
    .WithEviction(
        timeToLive:       TimeSpan.FromMinutes(30),
        idleTimeout:      TimeSpan.FromMinutes(5),
        evictionInterval: TimeSpan.FromMinutes(1));

// Custom predicate
services.AddDynamicObjectPool<MyResource>(sp => new MyResource())
    .WithCustomEviction((obj, meta) =>
        meta.LastAccessedAt < DateTime.UtcNow.AddMinutes(-10));
```

---

## Lifecycle Hooks

Execute code at each stage of an object's life:

```csharp
services.AddObjectPool<DbConnection>(builder => builder
    .WithFactory(() => new SqlConnection(cs))
    .WithLifecycleHooks(hooks =>
    {
        hooks.OnCreate  = conn => conn.Open();
        hooks.OnReturn  = conn => conn.ClearAllPools();
        hooks.OnDispose = conn => conn.Close();
        hooks.OnAcquire = conn => logger.LogDebug("Connection acquired");
        hooks.OnEvict   = (conn, reason) => logger.LogDebug("Evicted: {Reason}", reason);

        // Async hooks
        hooks.OnCreateAsync = async conn => await conn.OpenAsync();
    }));
```

---

## Warm-Up

Pre-populate the pool before accepting traffic:

```csharp
// Absolute count
services.AddDynamicObjectPool<HttpClient>(sp => new HttpClient())
    .WithAutoWarmup(targetSize: 20);

// Percentage of max capacity
services.AddDynamicObjectPool<HttpClient>(sp => new HttpClient())
    .WithAutoWarmupPercentage(targetPercentage: 75);

// Multiple pools
services.ConfigurePoolWarmup(warmup =>
{
    warmup.WarmupPool<HttpClient>(targetSize: 20);
    warmup.WarmupPool<DbConnection>(percentage: 50);
});
```

Manual warm-up:

```csharp
var warmer = provider.GetRequiredService<IObjectPoolWarmer<HttpClient>>();
await warmer.WarmUpAsync(20, cancellationToken);
await warmer.WarmUpToPercentageAsync(75, cancellationToken);

var status = warmer.GetWarmupStatus();
Console.WriteLine($"Created {status.ObjectsCreated} in {status.WarmupDuration.TotalMilliseconds}ms");
```

---

## Health Checks

```csharp
// ASP.NET Core health checks
builder.Services
    .AddHealthChecks()
    .AddObjectPoolHealthCheck<DbConnection>("db-pool")
    .AddObjectPoolHealthCheck<HttpClient>("http-pool", tags: ["ready"]);
```

Direct query:

```csharp
var pool = provider.GetRequiredService<IObjectPool<DbConnection>>();
var health = ((IPoolHealth)pool).GetHealthStatus();

Console.WriteLine(health.IsHealthy);
Console.WriteLine(health.UtilizationPercentage);
health.Warnings.ForEach(w => Console.WriteLine(w));
```

---

## OpenTelemetry Metrics

```csharp
services.AddDynamicObjectPool<HttpClient>(sp => new HttpClient())
    .WithTelemetry(meterName: "MyApp.Pools");

// Exposes meters:
// pool.retrieved.total   (Counter)
// pool.returned.total    (Counter)
// pool.active.current    (Gauge)
// pool.available.current (Gauge)
// pool.empty.events      (Counter)
// pool.utilization       (Gauge)
```

Prometheus export (no additional dependency):

```csharp
var pool = provider.GetRequiredService<IPoolMetrics>();
string prometheusText = pool.ExportMetricsPrometheus();
```

---

## Scoped Pools

Different object sets per logical scope (tenant, user, region):

```csharp
services.AddScopedObjectPool<DbConnection>(
    factory: sp => new SqlConnection(cs),
    config: cfg =>
    {
        cfg.MaxScopes = 50;
        cfg.ScopeIdleTimeout = TimeSpan.FromMinutes(15);
        cfg.ResolutionStrategy = ScopeResolutionStrategy.PerTenant;
    });

// Resolve
var scopedPool = provider.GetRequiredService<ScopedPoolManager<DbConnection>>();
var conn = scopedPool.GetForScope("tenant-42");
```

---

## Pooling Policies

Control object selection order:

```csharp
services.AddObjectPool<MyResource>(builder => builder
    .WithFactory(() => new MyResource())
    .WithPoolingPolicy(PoolingPolicyType.Lifo)       // default — Last In First Out (stack)
    .WithPoolingPolicy(PoolingPolicyType.Fifo)        // First In First Out (queue)
    .WithPoolingPolicy(PoolingPolicyType.Priority));  // priority-based selection
```

---

## Async Operations

```csharp
// Async get with timeout
var result = await pool.GetObjectAsync(timeout: TimeSpan.FromSeconds(2), cancellationToken);

result.Match(
    model => { /* use model */ },
    err   => logger.LogWarning("Timed out: {Msg}", err.Message));

// Queryable async get
var result = await queryablePool.GetObjectAsync(
    car => car.Make == "Ford",
    timeout: TimeSpan.FromSeconds(5));

// Async validation on return
services.AddObjectPool<DbConnection>(builder => builder
    .WithAsyncValidation(async conn =>
    {
        return await conn.PingAsync() == PingResult.Ok;
    })
    .WithAsyncDisposal());

// Async return
await pool.ReturnObjectAsync(model);
```

---

## Result-Based API

All pool operations return `ExtendedResult<T, Error>` from `Esox.SharpAndRusty` — they never throw unexpectedly. Use `Match` for clean control flow:

```csharp
pool.GetObject().Match(
    model => DoWork(model),
    error => HandleError(error.Message));

// Or imperative style
var result = pool.GetObject();
if (result.IsSuccess)
{
    using var model = result.Unwrap();
    model.Unwrap().DoWork();
}
```

**Cancellation** is the only case that throws — `OperationCanceledException` propagates as per the standard .NET cooperative cancellation protocol.

---

## Disclaimer

> **This library is provided "as is", without warranty of any kind, express or implied, including but not limited to the warranties of merchantability, fitness for a particular purpose, and non-infringement. In no event shall the authors or copyright holders be liable for any claim, damages, or other liability, whether in an action of contract, tort, or otherwise, arising from, out of, or in connection with the software or the use or other dealings in the software.**
>
> This project is independently developed and is not affiliated with, endorsed by, or supported by Microsoft or any other organisation. Use in production environments is at your own risk. Always validate the behaviour of pooled resources in your specific use case, particularly when using circuit breakers, eviction policies, and lifecycle hooks, as incorrect configuration can lead to resource exhaustion, data corruption, or connection leaks.
>
> Issues and contributions are welcome via [GitHub](https://github.com/snoekiede/Esox.SharpAndRusty.ObjectPool).

---

## License

MIT — see [LICENSE](LICENSE)
