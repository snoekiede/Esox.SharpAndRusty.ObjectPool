using Esox.SharpAndRusty.ObjectPool.Models;
using Esox.SharpAndRusty.ObjectPool.Pools;
using Esox.SharpAndRusty.ObjectPool.Tests.Models;
using Esox.SharpAndRusty.Extensions;
using Esox.SharpAndRusty.Types;

namespace Esox.SharpAndRusty.ObjectPool.Tests;

public class ProductionFeaturesTests
{
    [Fact]
    public void TestPoolConfiguration()
    {
        var config = new PoolConfiguration<int>
        {
            MaxPoolSize = 5,
            MaxActiveObjects = 3,
            ValidateOnReturn = true,
            ValidationFunction = _ => Unit.Value
        };

        var initialObjects = new List<int> { 1, 2, 3, 4, 5 };
        var pool = new ObjectPool<int>(initialObjects, config);

        Assert.Equal(5, pool.AvailableObjectCount);
    }

    [Fact]
    public void TestHealthMonitoring()
    {
        var initialObjects = new List<int> { 1, 2, 3 };
        var pool = new ObjectPool<int>(initialObjects);

        // Pool should be healthy initially
        Assert.True(pool.IsHealthy);

        var healthStatus = pool.GetHealthStatus();
        Assert.True(healthStatus.IsHealthy);
        Assert.True(healthStatus.WarningCount == 0);
        Assert.Contains("healthy", healthStatus.HealthMessage?.ToLower() ?? "");

        // Get all objects to make pool unhealthy
        var obj1 = pool.GetObject();
        var obj2 = pool.GetObject();
        var obj3 = pool.GetObject();

        // Pool should now be unhealthy (no available objects)
        Assert.False(pool.IsHealthy);

        var unhealthyStatus = pool.GetHealthStatus();
        Assert.False(unhealthyStatus.IsHealthy);
        Assert.True(unhealthyStatus.WarningCount > 0);

        // Clean up
        obj1.Unwrap().Dispose();
        obj2.Unwrap().Dispose();
        obj3.Unwrap().Dispose();
    }

    [Fact]
    public void TestMetricsExport()
    {
        var initialObjects = new List<int> { 1, 2, 3 };
        var pool = new ObjectPool<int>(initialObjects);

        // Perform some operations
        using var obj1 = pool.GetObject().Unwrap();
        using var obj2 = pool.GetObject().Unwrap();

        var metrics = pool.ExportMetrics();

        Assert.Contains("pool_objects_retrieved_total", metrics.Keys);
        Assert.Contains("pool_objects_active_current", metrics.Keys);
        Assert.Contains("pool_objects_available_current", metrics.Keys);
        Assert.Contains("pool_health_status", metrics.Keys);

        Assert.Equal(2L, metrics["pool_objects_retrieved_total"]);
        Assert.Equal(2, metrics["pool_objects_active_current"]);
        Assert.Equal(1, metrics["pool_objects_available_current"]);
    }



    [Fact]
    public void TestMetricsReset()
    {
        var initialObjects = new List<int> { 1, 2, 3 };
        var pool = new ObjectPool<int>(initialObjects);

        // Perform operations
        using var obj1 = pool.GetObject().Unwrap();
        using var obj2 = pool.GetObject().Unwrap();

        var metrics = pool.ExportMetrics();
        Assert.Equal(2L, metrics["pool_objects_retrieved_total"]);

        // Reset metrics
        pool.ResetMetrics();

        var resetMetrics = pool.ExportMetrics();
        Assert.Equal(0L, resetMetrics["pool_objects_retrieved_total"]);
    }

    [Fact]
    public void TestPoolConfigurationLimits()
    {
        var config = new PoolConfiguration<int>
        {
            MaxActiveObjects = 2
        };

        var initialObjects = new List<int> { 1, 2, 3 };
        var pool = new ObjectPool<int>(initialObjects, config);

        // Should be able to get 2 objects
        var obj1 = pool.GetObject();
        var obj2 = pool.GetObject();

        // Third attempt should fail due to limit
        Assert.True(pool.GetObject().IsFailure);

        // Clean up
        obj1.Unwrap().Dispose();
        obj2.Unwrap().Dispose();
    }



    [Fact]
    public void TestUtilizationPercentage()
    {
        var config = new PoolConfiguration<int>
        {
            MaxActiveObjects = 4,
            MaxPoolSize = 4
        };

        var initialObjects = new List<int> { 1, 2, 3, 4 };
        var pool = new ObjectPool<int>(initialObjects, config);

        // Initially 0% utilization
        Assert.Equal(0.0, pool.UtilizationPercentage);

        // Get 2 objects = 50% utilization
        var obj1 = pool.GetObject();
        var obj2 = pool.GetObject();

        Assert.Equal(50.0, pool.UtilizationPercentage);

        // Clean up
        obj1.Unwrap().Dispose();
        obj2.Unwrap().Dispose();
    }

    [Fact]
    public void TestDisposal()
    {
        var initialObjects = new List<int> { 1, 2, 3 };
        var pool = new ObjectPool<int>(initialObjects);

        pool.Dispose();

        // Should throw after disposal
        Assert.True(pool.GetObject().IsFailure);
    }

    [Fact]
    public async Task TestAsyncTimeoutWithConfiguration()
    {
        var config = new PoolConfiguration<int>
        {
            DefaultTimeout = TimeSpan.FromMilliseconds(100)
        };

        var initialObjects = new List<int> { 1 };
        var pool = new ObjectPool<int>(initialObjects, config);

        // Get the only object
        using var obj1 = pool.GetObject().Unwrap();

        // This should time out quickly due to configuration
        Assert.True((await pool.GetObjectAsync()).IsFailure); // Uses default timeout from config
        
    }

    [Fact]
    public void TestQueryablePoolEmptyResults()
    {
        // Test QueryableObjectPool returning no results
        var initialObjects = Car.GetInitialCars();
        var pool = new QueryableObjectPool<Car>(initialObjects);

        // Try to get a car that doesn't exist
        Assert.True(pool.GetObject(car => car.Make == "Toyota").IsFailure);
    }

    [Fact]
    public void TestTryQueryablePoolEmptyResults()
    {
        // Test TryGetObject with query returning false
        var initialObjects = Car.GetInitialCars();
        var pool = new QueryableObjectPool<Car>(initialObjects);

        var result = pool.TryGetObject(car => car.Make == "Toyota", out var model);

        Assert.True(result.IsFailure);
        Assert.Null(model);
    }

    [Fact]
    public void TestDynamicPoolFactoryCreation()
    {
        // Test that DynamicObjectPool creates objects when needed
        int factoryCallCount = 0;
        var factory = new Func<Car>(() => {
            factoryCallCount++;
            return new Car("Ford", "DynamicCar");
        });

        var pool = new DynamicObjectPool<Car>(factory);
        Assert.Equal(0, pool.AvailableObjectCount); // Empty at start

        // Getting an object should use the factory
        using var obj = pool.GetObject().Unwrap();
        Assert.Equal("Ford", obj.Unwrap().Make);
        Assert.Equal("DynamicCar", obj.Unwrap().Model);
        Assert.Equal(1, factoryCallCount);
    }

    [Fact]
    public void TestMaxPoolSizeLimit()
    {
        // Test MaxPoolSize limiting returned objects
        var config = new PoolConfiguration<int>
        {
            MaxPoolSize = 2
        };

        // Create a pool with initial count = MaxPoolSize
        var initialObjects = new List<int> { 1, 2 };
        var pool = new ObjectPool<int>(initialObjects, config);

        // First, empty the pool
        var obj1 = pool.GetObject();
        var obj2 = pool.GetObject();

        // Pool should be empty now
        Assert.Equal(0, pool.AvailableObjectCount);

        // Return obj1 to the pool
        obj1.Unwrap().Dispose();

        // Pool should have 1 object now
        Assert.Equal(1, pool.AvailableObjectCount);

        // Get a new object from the pool
        var obj3 = pool.GetObject();

        // Pool should be empty again
        Assert.Equal(0, pool.AvailableObjectCount);

        // Return both objects
        obj2.Unwrap().Dispose();
        obj3.Unwrap().Dispose();

        // Pool should have MaxPoolSize objects
        Assert.Equal(2, pool.AvailableObjectCount);

        // Get and dispose objects multiple times to ensure pool size remains limited
        for (int i = 0; i < 5; i++)
        {
            var tempObj = pool.GetObject();
            tempObj.Unwrap().Dispose();
        }

        // Pool size should still be MaxPoolSize
        Assert.Equal(2, pool.AvailableObjectCount);
    }

    [Fact]
    public void TestValidationOnReturn()
    {
        // Test validation function on return
        var config = new PoolConfiguration<int>
        {
            ValidateOnReturn = true,
            ValidationFunction = obj => obj > 5 ? Unit.Value : Error.New("Value must be greater than 5") // Only allow values > 5
        };

        var initialObjects = new List<int> { 10 }; // Valid object
        var pool = new ObjectPool<int>(initialObjects, config);

        // Get the object and return it
        var obj = pool.GetObject();
        obj.Unwrap().Dispose();

        // Object should pass validation and be returned to pool
        Assert.Equal(1, pool.AvailableObjectCount);

        // Use GetObject to get a valid object from the pool
        var validObj = pool.GetObject();

        // Make sure we have the expected object
        Assert.Equal(10, validObj.Unwrap().Unwrap());

        // Return it
        validObj.Unwrap().Dispose();

        // Object should pass validation and be returned to pool
        Assert.Equal(1, pool.AvailableObjectCount);
    }

    [Fact]
    public void TestExportMetricsWithTags()
    {
        // Test exporting metrics with tags
        var pool = new ObjectPool<int>([1, 2, 3]);

        var tags = new Dictionary<string, string>
        {
            ["environment"] = "test",
            ["service"] = "unit-tests"
        };

        var metrics = pool.ExportMetrics(tags);

        // Check tags were included
        Assert.Equal("test", metrics["tag_environment"]);
        Assert.Equal("unit-tests", metrics["tag_service"]);
    }

    [Fact]
    public async Task TestAsyncCancellation()
    {
        // Test cancellation in GetObjectAsync
        var pool = new ObjectPool<int>([1]);

        // Get the only object to make pool empty
        using var obj = pool.GetObject().Unwrap();

        // Create a cancellation token and cancel it
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // This should throw due to cancellation
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await pool.GetObjectAsync(cancellationToken: cts.Token));
    }

    [Fact]
    public async Task TestAsyncEventualSuccess()
    {
        // Test GetObjectAsync eventually succeeding
        var pool = new ObjectPool<int>([1]);

        // Get the only object
        var obj = pool.GetObject();

        // Start a task to get an object with enough timeout
        var getTask = Task.Run(async () => await pool.GetObjectAsync(TimeSpan.FromSeconds(2)));

        // Wait a bit then return the first object
        await Task.Delay(100);
        obj.Unwrap().Dispose();

        // The task should complete successfully now
        var result = await getTask;
        Assert.NotNull(result);
        result.Unwrap().Dispose();
    }

    [Fact]
    public void TestPoolHealthStatusDetails()
    {
        // Test health status diagnostic details
        var pool = new ObjectPool<int>([1, 2, 3]);

        // Get all objects to generate warnings
        var obj1 = pool.GetObject();
        var obj2 = pool.GetObject();
        var obj3 = pool.GetObject();

        var status = pool.GetHealthStatus();

        // Check diagnostics contains expected data
        Assert.Contains("CurrentActive", status.Diagnostics.Keys);
        Assert.Contains("CurrentAvailable", status.Diagnostics.Keys);
        Assert.Contains("TotalRetrieved", status.Diagnostics.Keys);

        // Check warnings are populated
        Assert.True(status.WarningCount > 0);
        Assert.NotEmpty(status.Warnings);

        // Clean up
        obj1.Unwrap().Dispose();
        obj2.Unwrap().Dispose();
        obj3.Unwrap().Dispose();
    }
}


