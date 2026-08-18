using Esox.SharpAndRusty.ObjectPool.Models;
using Esox.SharpAndRusty.ObjectPool.Pools;
using Esox.SharpAndRusty.Extensions;
using Esox.SharpAndRusty.Types;

namespace Esox.SharpAndRusty.ObjectPool.Tests;

public class BasicTests
{
    [Fact]
    public void CreatePoolModel()
    {
        var pool = new ObjectPool<int>([1, 2, 3]);
        var model = new PoolModel<int>(1, pool);

        Assert.NotNull(model);
        Assert.Equal(1, model.Unwrap());
    }

    [Fact]
    public void TestGetModel()
    {
        var initialObjects = new List<int> { 1, 2, 3 };
        var objectPool = new ObjectPool<int>(initialObjects);

        var initialCount = objectPool.AvailableObjectCount;
        var model = objectPool.GetObject();
        var afterCount = objectPool.AvailableObjectCount;

        Assert.Equal(3, initialCount);
        Assert.Equal(2, afterCount);
        Assert.NotNull(model);
    }

    [Fact]
    public void TestAutomaticReturn()
    {
        var initialObjects = new List<int> { 1, 2, 3 };
        var objectPool = new ObjectPool<int>(initialObjects);
        var initialCount = objectPool.AvailableObjectCount;
        using (var _ = objectPool.GetObject().Unwrap())
        {
            var afterCount = objectPool.AvailableObjectCount;
            Assert.Equal(3, initialCount);
            Assert.Equal(2, afterCount);
        }
        var afterusingCount = objectPool.AvailableObjectCount;
        Assert.Equal(3, afterusingCount);
    }

    [Fact]
    public void TestMultithreaded()
    {
        var initialObjects = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };
        var objectPool = new ObjectPool<int>(initialObjects);

        var tasks = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                using var model = objectPool.GetObject().Unwrap();
                var value = model.Unwrap();
                Assert.True(value > 0);
            }));
        }
        Task.WaitAll([.. tasks]);
        var afterusingCount = objectPool.AvailableObjectCount;
        Assert.Equal(11, afterusingCount);
    }

    [Fact]
    public void TestEmptyPool()
    {
        // Test getting from an empty pool
        var objectPool = new ObjectPool<int>([]);

        // Should return failure when getting from empty pool
        Assert.True(objectPool.GetObject().IsFailure);
        
    }



    [Fact]
    public void TestAccessDisposedPoolModel()
    {
        // Test accessing a disposed pool model
        var pool = new ObjectPool<int>([1]);
        var model = pool.GetObject().Unwrap();

        // Dispose the model
        model.Dispose();

        // Accessing should throw
        Assert.Throws<ObjectDisposedException>(() => model.Unwrap());

        // Disposing again should be safe
        model.Dispose(); // Should not throw
    }

    [Fact]
    public void TestMaxPoolSizeOnCreation()
    {
        // Test MaxPoolSize enforcement on creation
        var config = new PoolConfiguration<int> { MaxPoolSize = 2 };

        // This should throw because initial objects > max pool size
        Assert.Throws<ArgumentException>(() =>
            new ObjectPool<int>([1, 2, 3], config));
    }

    [Fact]
    public async Task TestAsyncGetObject()
    {
        // Test basic async retrieval
        var pool = new ObjectPool<int>([1, 2, 3]);

        var model = await pool.GetObjectAsync();

        Assert.NotNull(model);
        Assert.True(model.Unwrap().Unwrap() > 0);

        model.Unwrap().Dispose();
    }

    [Fact]
    public void TestMaxActiveObjectsLimit()
    {
        // Test MaxActiveObjects limit
        var config = new PoolConfiguration<int> { MaxActiveObjects = 2 };
        var pool = new ObjectPool<int>([1, 2, 3], config);

        // Get up to the limit
        var obj1 = pool.GetObject();
        var obj2 = pool.GetObject();

        // This should return failure because we reached the max active objects limit
        Assert.True(pool.GetObject().IsFailure);

        // Clean up
        obj1.Unwrap().Dispose();
        obj2.Unwrap().Dispose();
    }

    [Fact]
    public void TestValidationFunction()
    {
        // Test validation function on return
        var validationCalled = false;

        var config = new PoolConfiguration<int>
        {
            ValidateOnReturn = true,
            ValidationFunction = _ => {
                validationCalled = true;
                return Unit.Value;
            }
        };

        var pool = new ObjectPool<int>([1], config);

        // Get and return an object
        var model = pool.GetObject();
        model.Unwrap().Dispose();

        // Validation should have been called
        Assert.True(validationCalled);
    }

    [Fact]
    public void TestReturnInvalidObject()
    {
        // Test returning an object not from this pool
        var pool1 = new ObjectPool<int>([1]);
        var pool2 = new ObjectPool<int>([2]);

        var model = new PoolModel<int>(3, pool1);

        // Returning to wrong pool should cause failure
        Assert.True(pool2.ReturnObject(model).IsFailure);
        
    }
}



