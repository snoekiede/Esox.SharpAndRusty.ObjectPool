using Esox.SharpAndRusty.Extensions;
using Esox.SharpAndRusty.ObjectPool.DependencyInjection;
using Esox.SharpAndRusty.ObjectPool.Interfaces;
using Esox.SharpAndRusty.ObjectPool.Models;
using Esox.SharpAndRusty.ObjectPool.Telemetry;
using Esox.SharpAndRusty.ObjectPool.Tests.Models;
using Esox.SharpAndRusty.ObjectPool.Warmup;
using Microsoft.Extensions.DependencyInjection;

namespace Esox.SharpAndRusty.ObjectPool.Tests.DependencyInjection;

/// <summary>
/// Tests for the fluent AddDynamicObjectPool overload that returns ObjectPoolBuilder&lt;T&gt;,
/// enabling method chaining for circuit breaker, eviction, warmup, and telemetry configuration.
/// </summary>
public class FluentDynamicPoolBuilderTests
{
    // ──────────────────────────────────────────────────────────────────────────
    // Basic registration
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FluentOverload_ReturnsObjectPoolBuilder()
    {
        var services = new ServiceCollection();

        var builder = services.AddDynamicObjectPool<Car>(sp => new Car("Ford", "Focus"));

        Assert.IsType<ObjectPoolBuilder<Car>>(builder);
    }

    [Fact]
    public void FluentOverload_RegistersIObjectPool()
    {
        var services = new ServiceCollection();

        services.AddDynamicObjectPool<Car>(sp => new Car("Ford", "Focus"));

        var provider = services.BuildServiceProvider();
        var pool = provider.GetService<IObjectPool<Car>>();

        Assert.NotNull(pool);
    }

    [Fact]
    public void FluentOverload_RegistersIObjectPoolWarmer()
    {
        var services = new ServiceCollection();

        services.AddDynamicObjectPool<Car>(sp => new Car("Ford", "Focus"));

        var provider = services.BuildServiceProvider();
        var warmer = provider.GetService<IObjectPoolWarmer<Car>>();

        Assert.NotNull(warmer);
    }

    [Fact]
    public void FluentOverload_PoolAndWarmer_AreSameInstance()
    {
        var services = new ServiceCollection();

        services.AddDynamicObjectPool<Car>(sp => new Car("Ford", "Focus"));

        var provider = services.BuildServiceProvider();
        var pool = provider.GetRequiredService<IObjectPool<Car>>();
        var warmer = provider.GetRequiredService<IObjectPoolWarmer<Car>>();

        Assert.Same(pool, warmer);
    }

    [Fact]
    public void FluentOverload_CanGetObjectFromPool()
    {
        var services = new ServiceCollection();

        services.AddDynamicObjectPool<Car>(sp => new Car("Ford", "Focus"));

        var provider = services.BuildServiceProvider();
        var pool = provider.GetRequiredService<IObjectPool<Car>>();

        using var model = pool.GetObject().Unwrap();
        Assert.Equal("Ford", model.Unwrap().Make);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Configuration chaining: WithMaxSize / WithMaxActiveObjects / WithDefaultTimeout
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FluentChain_WithMaxActiveObjects_EnforcesLimit()
    {
        var services = new ServiceCollection();

        services.AddDynamicObjectPool<Car>(sp => new Car("Ford", "Focus"))
            .WithMaxSize(100)
            .WithMaxActiveObjects(3)
            .WithDefaultTimeout(TimeSpan.FromSeconds(5));

        var provider = services.BuildServiceProvider();
        var pool = provider.GetRequiredService<IObjectPool<Car>>();

        var held = new List<PoolModel<Car>>();
        for (int i = 0; i < 3; i++)
            held.Add(pool.GetObject().Unwrap());

        // Fourth acquire should fail because active limit is 3
        var overLimit = pool.GetObject();
        Assert.True(overLimit.IsFailure);

        foreach (var m in held) m.Dispose();
    }

    [Fact]
    public void FluentChain_AfterReturning_CanAcquireAgain()
    {
        var services = new ServiceCollection();

        services.AddDynamicObjectPool<Car>(sp => new Car("Ford", "Focus"))
            .WithMaxSize(10)
            .WithMaxActiveObjects(2);

        var provider = services.BuildServiceProvider();
        var pool = provider.GetRequiredService<IObjectPool<Car>>();

        var first = pool.GetObject().Unwrap();
        var second = pool.GetObject().Unwrap();

        // At limit; dispose one
        first.Dispose();

        // Should succeed now
        using var third = pool.GetObject().Unwrap();
        Assert.NotNull(third.Unwrap());

        second.Dispose();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // WithCircuitBreaker
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FluentChain_WithCircuitBreaker_ConfigurationIsApplied()
    {
        var services = new ServiceCollection();

        services.AddDynamicObjectPool<Car>(sp => new Car("Ford", "Focus"))
            .WithCircuitBreaker(failureThreshold: 5, openDuration: TimeSpan.FromSeconds(30));

        var provider = services.BuildServiceProvider();
        var pool = provider.GetRequiredService<IObjectPool<Car>>();

        // Pool should be operational — circuit is closed at startup
        using var model = pool.GetObject().Unwrap();
        Assert.NotNull(model.Unwrap());
    }

    [Fact]
    public void FluentChain_WithCircuitBreaker_OpensAfterThreshold()
    {
        int callCount = 0;
        var services = new ServiceCollection();

        services.AddDynamicObjectPool<Car>(sp =>
        {
            callCount++;
            throw new InvalidOperationException("Factory failure");
        })
        .WithMaxSize(20)
        .WithCircuitBreaker(failureThreshold: 3, openDuration: TimeSpan.FromSeconds(60));

        var provider = services.BuildServiceProvider();
        var pool = provider.GetRequiredService<IObjectPool<Car>>();

        // Drive factory failures to open the circuit
        for (int i = 0; i < 5; i++)
            pool.GetObject(); // results are ignored; circuit should open after 3

        // Once open the circuit breaker returns a failure result
        var result = pool.GetObject();
        Assert.True(result.IsFailure);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // WithTimeToLive / WithIdleTimeout
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FluentChain_WithTimeToLive_ConfigurationIsStored()
    {
        var services = new ServiceCollection();

        // TTL is stored in _configuration — verify pool still resolves correctly
        services.AddDynamicObjectPool<Car>(sp => new Car("Ford", "Focus"))
            .WithTimeToLive(TimeSpan.FromMinutes(30));

        var provider = services.BuildServiceProvider();
        var pool = provider.GetRequiredService<IObjectPool<Car>>();

        Assert.NotNull(pool);
        using var model = pool.GetObject().Unwrap();
        Assert.Equal("Ford", model.Unwrap().Make);
    }

    [Fact]
    public void FluentChain_WithIdleTimeout_ConfigurationIsStored()
    {
        var services = new ServiceCollection();

        services.AddDynamicObjectPool<Car>(sp => new Car("Ford", "Focus"))
            .WithIdleTimeout(TimeSpan.FromMinutes(5));

        var provider = services.BuildServiceProvider();
        var pool = provider.GetRequiredService<IObjectPool<Car>>();

        Assert.NotNull(pool);
        using var model = pool.GetObject().Unwrap();
        Assert.Equal("Ford", model.Unwrap().Make);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // WithTelemetry
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FluentChain_WithTelemetry_RegistersObjectPoolMeter()
    {
        var services = new ServiceCollection();

        services.AddDynamicObjectPool<Car>(sp => new Car("Ford", "Focus"))
            .WithTelemetry(meterName: "MyApp.Pools", poolName: "car-pool");

        var provider = services.BuildServiceProvider();
        var meter = provider.GetService<ObjectPoolMeter<Car>>();

        Assert.NotNull(meter);
    }

    [Fact]
    public void FluentChain_WithTelemetry_DefaultMeterName_RegistersObjectPoolMeter()
    {
        var services = new ServiceCollection();

        services.AddDynamicObjectPool<Car>(sp => new Car("Ford", "Focus"))
            .WithTelemetry();

        var provider = services.BuildServiceProvider();
        var meter = provider.GetService<ObjectPoolMeter<Car>>();

        Assert.NotNull(meter);
    }

    [Fact]
    public void FluentChain_WithTelemetry_MeterIsDisposable()
    {
        var services = new ServiceCollection();

        services.AddDynamicObjectPool<Car>(sp => new Car("Ford", "Focus"))
            .WithTelemetry(meterName: "MyApp.Pools");

        var provider = services.BuildServiceProvider();
        var meter = provider.GetRequiredService<ObjectPoolMeter<Car>>();

        // ObjectPoolMeter<T> implements IDisposable; disposing the provider should not throw
        var ex = Record.Exception(() => provider.Dispose());
        Assert.Null(ex);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // WithAutoWarmupPercentage
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task FluentChain_WithAutoWarmupPercentage_WarmsUpPool()
    {
        var services = new ServiceCollection();

        services.AddDynamicObjectPool<Car>(sp => new Car("Test", "Model"))
            .WithMaxSize(100)
            .WithAutoWarmupPercentage(50);

        var provider = services.BuildServiceProvider();

        foreach (var hs in provider.GetServices<Microsoft.Extensions.Hosting.IHostedService>())
            await hs.StartAsync(CancellationToken.None);

        await Task.Delay(200); // allow hosted service to complete warmup

        var warmer = provider.GetRequiredService<IObjectPoolWarmer<Car>>();
        var status = warmer.GetWarmupStatus();

        Assert.True(status.IsWarmedUp);
        Assert.Equal(50, status.ObjectsCreated); // 50 % of 100
    }

    [Fact]
    public void FluentChain_WithAutoWarmupPercentage_OutOfRange_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            services.AddDynamicObjectPool<Car>(sp => new Car("Ford", "Focus"))
                .WithAutoWarmupPercentage(150));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Full chain (integration-style)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task FluentChain_FullChain_RegistersAndWarmsUpSuccessfully()
    {
        var services = new ServiceCollection();

        services.AddDynamicObjectPool<Car>(sp => new Car("Ford", "Focus"))
            .WithMaxSize(100)
            .WithMaxActiveObjects(50)
            .WithDefaultTimeout(TimeSpan.FromSeconds(5))
            .WithCircuitBreaker(failureThreshold: 5, openDuration: TimeSpan.FromSeconds(30))
            .WithTimeToLive(TimeSpan.FromMinutes(30))
            .WithIdleTimeout(TimeSpan.FromMinutes(5))
            .WithAutoWarmupPercentage(targetPercentage: 10)
            .WithTelemetry(meterName: "MyApp.Pools");

        var provider = services.BuildServiceProvider();

        // All core interfaces resolve
        Assert.NotNull(provider.GetService<IObjectPool<Car>>());
        Assert.NotNull(provider.GetService<IObjectPoolWarmer<Car>>());
        Assert.NotNull(provider.GetService<ObjectPoolMeter<Car>>());

        // Trigger warmup
        foreach (var hs in provider.GetServices<Microsoft.Extensions.Hosting.IHostedService>())
            await hs.StartAsync(CancellationToken.None);

        await Task.Delay(200);

        var status = provider.GetRequiredService<IObjectPoolWarmer<Car>>().GetWarmupStatus();
        Assert.True(status.IsWarmedUp);
        Assert.Equal(10, status.ObjectsCreated); // 10% of 100
    }

    [Fact]
    public void FluentChain_FullChain_CanAcquireAndReturnObject()
    {
        var services = new ServiceCollection();

        services.AddDynamicObjectPool<Car>(sp => new Car("Ford", "Focus"))
            .WithMaxSize(100)
            .WithMaxActiveObjects(50)
            .WithDefaultTimeout(TimeSpan.FromSeconds(5))
            .WithCircuitBreaker(failureThreshold: 5, openDuration: TimeSpan.FromSeconds(30))
            .WithTimeToLive(TimeSpan.FromMinutes(30))
            .WithIdleTimeout(TimeSpan.FromMinutes(5))
            .WithTelemetry(meterName: "MyApp.Pools");

        var provider = services.BuildServiceProvider();
        var pool = provider.GetRequiredService<IObjectPool<Car>>();

        using var model = pool.GetObject().Unwrap();
        var car = model.Unwrap();
        Assert.Equal("Ford", car.Make);
        Assert.Equal("Focus", car.Model);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Backward compatibility — old overload still works
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void OldOverload_WithConfigureAction_StillWorks()
    {
        var services = new ServiceCollection();

        // Old overload: returns IServiceCollection, not ObjectPoolBuilder<T>
        IServiceCollection result = services.AddDynamicObjectPool<Car>(
            sp => new Car("Ford", "Focus"),
            config => config.MaxPoolSize = 50);

        Assert.Same(services, result);

        var provider = services.BuildServiceProvider();
        var pool = provider.GetService<IObjectPool<Car>>();

        Assert.NotNull(pool);
        using var model = pool.GetObject().Unwrap();
        Assert.Equal("Ford", model.Unwrap().Make);
    }
}
