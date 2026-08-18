using Esox.SharpAndRusty.ObjectPool.Models;
using Esox.SharpAndRusty.ObjectPool.Pools;
using Esox.SharpAndRusty.Extensions;
using Esox.SharpAndRusty.Types;

namespace Esox.SharpAndRusty.ObjectPool.Tests;

public class PoolConfigurationTests
{
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

        // Act - Create and return more objects than the pool size
        using (pool.GetObject().Unwrap())
        using (pool.GetObject().Unwrap())
        {
            // Remove 2 objects, leaving 1 in pool
        }

        // Add more than max pool size
        var obj3 = pool.GetObject();
        var obj4 = pool.GetObject();
        var obj5 = pool.GetObject();

        // Return them all
        obj3.Unwrap().Dispose();
        obj4.Unwrap().Dispose();
        obj5.Unwrap().Dispose();

        // Assert - Pool should be limited to MaxPoolSize
        Assert.Equal(3, pool.AvailableObjectCount);
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

        // Assert - Getting a third should throw
        Assert.True(pool.GetObject().IsFailure);

        // Clean up
        obj1.Unwrap().Dispose();
        obj2.Unwrap().Dispose();
    }

    [Fact]
    public void TestValidationFunction()
    {
        // Arrange
        var config = new PoolConfiguration<int>
        {
            ValidateOnReturn = true,
            ValidationFunction = obj => obj > 5 ? Unit.Value : Error.New("Only values > 5 are valid") // Only values > 5 are valid
        };

        var initialObjects = new List<int> { 10, 3 };
        var pool = new ObjectPool<int>(initialObjects, config);

        // Act - Get both objects and return them
        var obj1 = pool.GetObject();
        var obj2 = pool.GetObject();

        obj1.Unwrap().Dispose(); // 10 should pass validation
        obj2.Unwrap().Dispose(); // 3 should fail validation

        // Assert - Only valid object should be in the pool
        Assert.Equal(1, pool.AvailableObjectCount);
    }

    [Fact]
    public void TestEmptyInitialObjectsList()
    {
        // Arrange & Act
        var pool = new ObjectPool<int>([]);

        // Assert
        Assert.Equal(0, pool.AvailableObjectCount);
    }
}
