# Changelog

All notable changes to **Esox.SharpAndRusty.ObjectPool** are documented here.

This project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html) and the [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) format.

---

## [1.0.2] — 2026

### Added

#### Core pool types
- `ObjectPool<T>` — fixed-size, thread-safe generic pool backed by a `SemaphoreSlim` and `ConcurrentStack<T>`.
- `DynamicObjectPool<T>` — grows on demand via a factory delegate; supports circuit breaking, eviction, lifecycle hooks, and warm-up.
- `QueryableObjectPool<T>` — extends `DynamicObjectPool<T>` with predicate-based synchronous and asynchronous object selection.

#### Result-based API
- All `GetObject()` and `ReturnObject()` calls return `ExtendedResult<PoolModel<T>, Error>` or `Result<Unit, Error>` from `Esox.SharpAndRusty` — pool operations never throw unexpectedly.
- `GetObjectAsync()` returns `Task<ExtendedResult<PoolModel<T>, Error>>` with timeout expressed as a failure result; only cancellation propagates `OperationCanceledException`.
- `Match`-friendly design throughout.

#### Dependency injection
- `AddObjectPool<T>(Action<ObjectPoolBuilder<T>>)` — standard fluent pool registration.
- `AddDynamicObjectPool<T>(Func<IServiceProvider, T>)` — **new fluent overload** returning `ObjectPoolBuilder<T>` that supports a full configuration chain:
  ```csharp
  services.AddDynamicObjectPool<DbConnection>(sp => CreateConnection())
	  .WithMaxSize(100)
	  .WithMaxActiveObjects(50)
	  .WithDefaultTimeout(TimeSpan.FromSeconds(5))
	  .WithCircuitBreaker(failureThreshold: 5, openDuration: TimeSpan.FromSeconds(30))
	  .WithTimeToLive(TimeSpan.FromMinutes(30))
	  .WithIdleTimeout(TimeSpan.FromMinutes(5))
	  .WithAutoWarmupPercentage(targetPercentage: 50)
	  .WithTelemetry(meterName: "MyApp.Pools");
  ```
- `AddDynamicObjectPool<T>(Func<IServiceProvider, T>, Action<PoolConfiguration<T>>?)` — legacy overload retained for backwards compatibility; returns `IServiceCollection`.
- `AddQueryableObjectPool<T>`, `AddObjectPoolWithObjects<T>`, `AddObjectPools` registration helpers.
- Automatic registration of `IObjectPool<T>`, `IObjectPoolWarmer<T>`, `DynamicObjectPool<T>` as singletons.

#### ObjectPoolBuilder
- `WithMaxSize(int)`, `WithMaxActiveObjects(int)`, `WithDefaultTimeout(TimeSpan)` — core size and timing constraints.
- `WithValidation(Func<T,bool>)` — per-object validation on return, wired to `ExtendedResult`.
- `WithHealthChecks()`, `AsQueryable()`, `Configure(Action<PoolConfiguration<T>>)`.
- `WithCircuitBreaker(...)` — delegates to `CircuitBreakerExtensions`.
- `WithTimeToLive(TimeSpan)`, `WithIdleTimeout(TimeSpan)`, `WithEviction(...)`, `WithCustomEviction(...)` — delegates to `EvictionBuilderExtensions`.
- `WithAutoWarmupPercentage(double)` — registers a `PoolWarmupHostedService<T>` via the attached `IServiceCollection`.
- `WithTelemetry(string?, string?)` — registers `ObjectPoolMeter<T>` via the attached `IServiceCollection`.

#### Circuit breaker
- `CircuitBreaker` — standalone thread-safe circuit breaker with `Closed → Open → HalfOpen` state machine.
- `Execute<T>()`, `ExecuteAsync<T>()` — throw `CircuitBreakerOpenException` when the circuit is open.
- `TryExecute<T>()` — returns `bool`; safe non-throwing variant.
- Configurable `failureThreshold`, `openDuration`, `successThreshold`, and `enableAutomaticRecovery`.
- `WithCircuitBreaker(...)` and `WithCircuitBreakerPercentage(...)` builder extensions.
- Double-lock bug fixed; `RejectedOperations` counter is correctly incremented in all execution paths.

#### Eviction
- `EvictionConfiguration` — `TimeToLive`, `IdleTimeout`, `EvictionInterval`, `EnableBackgroundEviction`, custom predicate support.
- `EvictionManager<T>` — background timer that removes expired or idle objects.
- Builder extensions: `WithTimeToLive`, `WithIdleTimeout`, `WithEviction`, `WithCustomEviction`, `WithEvictionConfiguration`.
- Service-collection extensions mirror the builder extensions for post-registration configuration.

#### Lifecycle hooks
- `LifecycleHookManager<T>` — `OnCreate`, `OnAcquire`, `OnReturn`, `OnDispose`, `OnEvict` hooks.
- Both synchronous and asynchronous hook variants.
- Configurable `continueOnError`; when `false`, exceptions are rethrown from both sync and async paths.

#### Warm-up
- `IObjectPoolWarmer<T>` — `WarmUpAsync(int)`, `WarmUpToPercentageAsync(double)`.
- `WarmupStatus` — `IsWarmedUp`, `ObjectsCreated`, `WarmupDuration`.
- `PoolWarmupHostedService<T>` — `IHostedService` that warms up on application startup.
- DI extensions: `WithAutoWarmup<T>(int)`, `WithAutoWarmupPercentage<T>(double)`, `ConfigurePoolWarmup(Action<PoolWarmupBuilder>)`.

#### Health checks & metrics
- `IPoolHealth` — `GetHealthStatus()` reporting pool state and saturation.
- `IPoolMetrics` — `GetMetrics()`, `ExportMetrics(IDictionary<string,string>?)`, `ExportMetricsPrometheus()`, `ResetMetrics()`.
- `PoolStatistics` — retrieved, returned, created, failed, active, available counters.
- `ObjectPoolMeter<T>` — OpenTelemetry `Meter`-based instrumentation exposing `pool.retrieved.total`, `pool.returned.total`, `pool.active.current`, `pool.available.current`, `pool.empty.events`, `pool.utilization`.
- `AddObjectPoolMetrics<T>(string?, string?)` DI extension; also accessible via `.WithTelemetry(...)` on the builder.

#### Scoped pools
- `ScopedPoolManager<T>` — per-scope (tenant, user, region) object sets.
- `AddScopedObjectPool<T>` DI extension with `MaxScopes`, `ScopeIdleTimeout`, and `ScopeResolutionStrategy`.

#### Pooling policies
- `PoolingPolicyType` — `Lifo` (default), `Fifo`, `Priority`.
- `WithPoolingPolicy(PoolingPolicyType)` builder extension; priority pools accept a `PrioritySelector` delegate.

#### Multi-targeting
- Targets `net8.0`, `net9.0`, and `net10.0`.
- Language version set to C# 14 (`<LangVersion>14.0</LangVersion>`) enabling `extension` block syntax and unbound-generic `nameof`.
- `System.Threading.Lock` used on .NET 9+ via `#if NET9_0_OR_GREATER`; falls back to `object` on .NET 8.

#### NuGet packaging
- `<PackageReadmeFile>README.md</PackageReadmeFile>` wired in the `.csproj`; `README.md` is embedded in the `.nupkg`.
- `LICENSE` (MIT) included in the repository root.
- Strong-name signing configured via `AssemblyOriginatorKeyFile`.

### Fixed
- `DynamicObjectPool.GetObjectInternal` — removed duplicate `IncrementRetrieved()` call that double-counted retrieval statistics.
- `DynamicObjectPool.GetObject` — factory exceptions now propagate to the circuit breaker before being caught, allowing correct state transitions.
- `CircuitBreaker.ExecuteAsync` / `TryExecute` — `RejectedOperations++` is now protected under the state lock in all branches.
- `LifecycleHookManager` — synchronous hook path now rethrows when `continueOnError` is `false` (previously returned an error result instead of throwing).
- `ServiceCollectionExtensions` — `ObjectPoolBuilder<T>` is registered as a singleton so eviction and other post-registration extensions can locate the builder instance.
- `QueryableObjectPool.GetObjectAsync` — pre-cancelled `CancellationToken` now throws `OperationCanceledException` immediately rather than entering the timeout loop.
- `AsyncPoolTests.TestAsyncWithTimeout` / `QueryableObjectPoolExtendedTests` — test assertions corrected to expect failure results (not exceptions) for timeout paths; cancellation tests still expect `OperationCanceledException`.

### Tests
- 759 tests across `net8.0`, `net9.0`, and `net10.0` — all passing.
- New `FluentDynamicPoolBuilderTests` (18 tests) covering the full `AddDynamicObjectPool` fluent chain: basic registration, interface resolution, `WithMaxActiveObjects`, circuit breaker open/close, TTL/idle timeout, `WithTelemetry` meter registration, `WithAutoWarmupPercentage` with hosted-service startup, full chain integration, and backwards-compatibility of the legacy overload.
- `WarmupDIIntegrationTests` extended with two additional tests exercising the new fluent overload.

---

*For issues and contributions see [GitHub](https://github.com/snoekiede/Esox.SharpAndRusty.ObjectPool).*
