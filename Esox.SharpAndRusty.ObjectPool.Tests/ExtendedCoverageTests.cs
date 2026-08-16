using Esox.SharpAndRusty.ObjectPool.Models;
using Esox.SharpAndRusty.ObjectPool.Pools;
using Esox.SharpAndRusty.ObjectPool.Tests.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Esox.SharpAndRusty.Extensions;
using Esox.SharpAndRusty.Types;
using Moq;

namespace Esox.SharpAndRusty.ObjectPool.Tests;

public class ExtendedCoverageTests
{
    [Fact]
    public async Task TestAsyncOperationsWithCancellation()
    {
        // Arrange
        var initialObjects = new List<int> { 1 };
        var pool = new ObjectPool<int>(initialObjects);

        // Get the only object so the pool is empty
        using var obj = pool.GetObject().Unwrap();

        // Act & Assert - Create a cancellation token and cancel it
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // This should throw due to cancellation
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await pool.GetObjectAsync(cancellationToken: cts.Token));
    }

    [Fact]
    public async Task TestAsyncGetObjectWithTimeout()
    {
        // Arrange
        var initialObjects = new List<int> { 1 };
        var pool = new ObjectPool<int>(initialObjects);

        // Get the only object so the pool is empty
        using var obj = pool.GetObject().Unwrap();

        // Act & Assert - Try to get another with a short timeout
        var timeout = TimeSpan.FromMilliseconds(100);
        var result = await pool.GetObjectAsync(timeout);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task TestAsyncGetObjectEventualSuccess()
    {
        // Arrange
        var initialObjects = new List<int> { 1 };
        var pool = new ObjectPool<int>(initialObjects);

        // Get the only object
        var obj = pool.GetObject();

        // Start a task to get another object (will wait)
        var getTask = Task.Run(async () => await pool.GetObjectAsync(TimeSpan.FromSeconds(2)));

        // Return the first object after a delay
        await Task.Delay(100);
        obj.Unwrap().Dispose();

        // The getTask should now complete
        var result = await getTask;
        Assert.NotNull(result);

        // Clean up
        result.Unwrap().Dispose();
    }

    [Fact]
    public void TestValidationFunction()
    {
        // Arrange
        var validationCalled = false;
        var config = new PoolConfiguration<int>
        {
            ValidateOnReturn = true,
            ValidationFunction = obj =>
            {
                validationCalled = true;
                return (int)obj > 5 ? Unit.Value : Error.New("Only values > 5 are valid"); // Only values > 5 are valid
            }
        };

        var initialObjects = new List<int> { 10 };
        var pool = new ObjectPool<int>(initialObjects, config);

        // Act - Get object and return
        using (pool.GetObject().Unwrap())
        {
            // No validation happens on get
        }

        // Assert
        Assert.True(validationCalled);
        Assert.Equal(1, pool.AvailableObjectCount); // Object passed validation and was returned
    }

    [Fact]
    public void TestValidationFailure()
    {
        // Arrange
        var config = new PoolConfiguration<int>
        {
            ValidateOnReturn = true,
            ValidationFunction = obj => (int)obj > 5 ? Unit.Value : Error.New("Only values > 5 are valid") // Only values > 5 are valid
        };

        var initialObjects = new List<int> { 3 }; // Invalid from the start
        var pool = new ObjectPool<int>(initialObjects, config);

        // Act - Get object and return
        using (pool.GetObject().Unwrap())
        {
            // No validation happens on get
        }

        // Assert - Object failed validation on return and was discarded
        Assert.Equal(0, pool.AvailableObjectCount);
    }

    [Fact]
    public void TestLoggingInObjectPool()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<ObjectPool<int>>>();
        var initialObjects = new List<int> { 1, 2, 3 };

        // Act
        var pool = new ObjectPool<int>(initialObjects, null, loggerMock.Object);
        using var obj = pool.GetObject().Unwrap();

        // Assert - Verify log was called for initialization
        loggerMock.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("ObjectPool created")),
                It.IsAny<Exception?>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public void TestMaxActiveObjectsLimit()
    {
        // Arrange
        var config = new PoolConfiguration<int>
        {
            MaxActiveObjects = 2 // Limit to 2 active objects
        };

        var initialObjects = new List<int> { 1, 2, 3 };
        var pool = new ObjectPool<int>(initialObjects, config);

        // Act
        var obj1 = pool.GetObject();
        var obj2 = pool.GetObject();

        // Assert - Getting a third should fail
        Assert.True(pool.GetObject().IsFailure);

        // Clean up
        obj1.Unwrap().Dispose();
        obj2.Unwrap().Dispose();
    }

    

    [Fact]
    public void TestResetMetrics()
    {
        // Arrange
        var initialObjects = new List<int> { 1, 2, 3 };
        var pool = new ObjectPool<int>(initialObjects);

        // Get and return some objects to generate metrics
        for (int i = 0; i < 5; i++)
        {
            using var obj = pool.GetObject().Unwrap();
        }

        var metricsBeforeReset = pool.ExportMetrics();
        Assert.Equal(5L, metricsBeforeReset["pool_objects_retrieved_total"]);
        Assert.Equal(5L, metricsBeforeReset["pool_objects_returned_total"]);

        // Act
        pool.ResetMetrics();

        // Assert
        var metricsAfterReset = pool.ExportMetrics();
        Assert.Equal(0L, metricsAfterReset["pool_objects_retrieved_total"]);
        Assert.Equal(0L, metricsAfterReset["pool_objects_returned_total"]);
    }

    [Fact]
    public void TestMaxPoolSizeLimit()
    {
        // Arrange
        var config = new PoolConfiguration<int>
        {
            MaxPoolSize = 3 // Limit pool size to 3
        };

        var initialObjects = new List<int> { 1, 2, 3 };
        var pool = new ObjectPool<int>(initialObjects, config);

        // Get and return objects in a cycle to test MaxPoolSize enforcement
        var objects = new List<PoolModel<int>>();

        // First, get all objects from the pool
        for (int i = 0; i < 3; i++)
        {
            objects.Add(pool.GetObject().Unwrap());
        }

        // Now pool is empty, return all objects
        foreach (var obj in objects)
        {
            obj.Dispose();
        }
        objects.Clear();

        // Pool should now have 3 objects again
        Assert.Equal(3, pool.AvailableObjectCount);

        // Now get and return objects multiple times to ensure the pool
        // doesn't grow beyond MaxPoolSize even with reuse
        for (int cycle = 0; cycle < 3; cycle++)
        {
            // Get all objects
            for (int i = 0; i < 3; i++)
            {
                objects.Add(pool.GetObject().Unwrap());
            }

            // Return all objects
            foreach (var obj in objects)
            {
                obj.Dispose();
            }
            objects.Clear();

            // Pool should still be at MaxPoolSize
            Assert.Equal(3, pool.AvailableObjectCount);
        }
    }

    [Fact]
    public void TestObjectPoolDisposal()
    {
        // Arrange
        var initialObjects = new List<int> { 1, 2, 3 };
        var pool = new ObjectPool<int>(initialObjects);

        // Act
        pool.Dispose();

        // Assert
        Assert.True(pool.GetObject().IsFailure);
        
    }

    [Fact]
    public void TestDynamicPoolWithNoFactory()
    {
        // Arrange
        var initialObjects = Car.GetInitialCars();
        var pool = new DynamicObjectPool<Car>(initialObjects);

        // Get all objects so pool is empty
        var objects = new List<PoolModel<Car>>();
        for (int i = 0; i < initialObjects.Count; i++)
        {
            objects.Add(pool.GetObject().Unwrap());
        }

        // Act & Assert - No factory, so getting another should throw
        Assert.True(pool.GetObject().IsFailure);

        // Clean up
        foreach (var obj in objects)
        {
            obj.Dispose();
        }
    }

    [Fact]
    public void TestQueryablePoolWithNoMatches()
    {
        // Arrange
        var initialObjects = Car.GetInitialCars();
        var pool = new QueryableObjectPool<Car>(initialObjects);

        // Act & Assert - Query for non-existent model
        Assert.True(pool.GetObject(car => car.Make == "Toyota").IsFailure);
    }

    [Fact]
    public void TestTryQueryablePoolWithNoMatches()
    {
        // Arrange
        var initialObjects = Car.GetInitialCars();
        var pool = new QueryableObjectPool<Car>(initialObjects);

        // Act
        var success = pool.TryGetObject(car => car.Make == "Toyota", out var result);

        // Assert
        Assert.False(success.IsSuccess);
        Assert.Null(result);
    }



    [Fact]
    public void TestExportMetricsWithTags()
    {
        // Arrange
        var initialObjects = new List<int> { 1, 2, 3 };
        var pool = new ObjectPool<int>(initialObjects);

        // Act
        var tags = new Dictionary<string, string>
        {
            ["service"] = "test-service",
            ["environment"] = "testing"
        };

        var metrics = pool.ExportMetrics(tags);

        // Assert
        Assert.Equal("test-service", metrics["tag_service"]);
        Assert.Equal("testing", metrics["tag_environment"]);
    }

    [Fact]
    public void TestEmptyInitialObjectsList()
    {
        // Arrange & Act
        var pool = new ObjectPool<int>([]);

        // Assert
        Assert.Equal(0, pool.AvailableObjectCount);
        Assert.True(!pool.GetObject().IsSuccess);
    }

    [Fact]
    public void TestPoolModelDisposalBehavior()
    {
        // Arrange
        var pool = new ObjectPool<int>([1]);
        var poolModel = pool.GetObject().Unwrap();

        // Act
        poolModel.Dispose();

        // Assert - Should throw when accessing disposed model
        Assert.Throws<ObjectDisposedException>(() => poolModel.Unwrap());

        // Disposing twice should be safe
        poolModel.Dispose();
    }
}
