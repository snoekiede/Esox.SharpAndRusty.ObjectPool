using Esox.SharpAndRusty.ObjectPool.Interfaces;
using Esox.SharpAndRusty.ObjectPool.Models;
using Esox.SharpAndRusty.ObjectPool.Pools;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Esox.SharpAndRusty.Extensions;
using Esox.SharpAndRusty.ObjectPool.DependencyInjection;
using Esox.SharpAndRusty.Types;

namespace Esox.SharpAndRusty.ObjectPool.Tests.Async;

public class AsyncValidationTests
{
    private class ValidatableResource
    {
        public bool IsValid { get; set; } = true;
        public bool WasValidatedAsync { get; set; }
        public int Id { get; set; }
    }

    [Fact]
    public async Task ReturnObjectAsync_WithValidObject_ShouldReturnToPool()
    {
        // Arrange
        var resource = new ValidatableResource { Id = 1, IsValid = true };
        var config = new PoolConfiguration<ValidatableResource>
        {
            ValidateOnReturn = true,
            AsyncValidationFunction = async obj =>
            {
                await Task.Delay(1);
                var res = (ValidatableResource)obj;
                res.WasValidatedAsync = true;
                return res.IsValid ? Unit.Value: Error.New("Not valid");
            }
        };

        var pool = new ObjectPool<ValidatableResource>(new List<ValidatableResource> { resource }, config);
        var pooled = pool.GetObject();

        // Act
        await pool.ReturnObjectAsync(pooled.Unwrap());

        // Assert
        Assert.True(resource.WasValidatedAsync);
        Assert.Equal(1, pool.AvailableObjectCount);
    }

    [Fact]
    public async Task ReturnObjectAsync_WithInvalidObject_ShouldNotReturnToPool()
    {
        // Arrange
        var resource = new ValidatableResource { Id = 1, IsValid = false };
        var config = new PoolConfiguration<ValidatableResource>
        {
            ValidateOnReturn = true,
            AsyncValidationFunction = async obj =>
            {
                await Task.Delay(1);
                var res = (ValidatableResource)obj;
                return res.IsValid ? Unit.Value : Error.New("Not valid");
            }
        };

        var pool = new ObjectPool<ValidatableResource>(new List<ValidatableResource> { resource }, config);
        var pooled = pool.GetObject();

        // Act
        await pool.ReturnObjectAsync(pooled.Unwrap());

        // Assert
        Assert.Equal(0, pool.AvailableObjectCount);
    }

    [Fact]
    public async Task ReturnObjectAsync_WithAsyncValidationAndTaskBased_ShouldWork()
    {
        // Arrange
        var resource = new ValidatableResource { Id = 1 };
        var validationCalled = false;

        var config = new PoolConfiguration<ValidatableResource>
        {
            ValidateOnReturn = true,
            AsyncValidationFunction = async obj =>
            {
                await Task.Delay(10);
                validationCalled = true;
                return Unit.Value;
            }
        };

        var pool = new ObjectPool<ValidatableResource>(new List<ValidatableResource> { resource }, config);
        var pooled = pool.GetObject();

        // Act
        await pool.ReturnObjectAsync(pooled.Unwrap());

        // Assert
        Assert.True(validationCalled);
        Assert.Equal(1, pool.AvailableObjectCount);
    }

    [Fact]
    public async Task WithAsyncValidation_UsingDI_ShouldWork()
    {
        // Arrange
        var services = new ServiceCollection();
        var validationCallCount = 0;

        services.AddObjectPool<ValidatableResource>(builder => builder
            .WithFactory(() => new ValidatableResource { Id = 1 })
            .WithAsyncValidation(async resource =>
            {
                await Task.Delay(1);
                validationCallCount++;
                return resource.IsValid;
            })
            .WithMaxSize(5));

        var provider = services.BuildServiceProvider();
        var pool = provider.GetRequiredService<IObjectPool<ValidatableResource>>();

        // Act
        var pooled1 = pool.GetObject();
        var pooled2 = pool.GetObject();

        await pool.ReturnObjectAsync(pooled1.Unwrap());
        await pool.ReturnObjectAsync(pooled2.Unwrap());

        // Assert
        Assert.Equal(2, validationCallCount);
        Assert.Equal(2, pool.AvailableObjectCount);
    }

    [Fact]
    public async Task AsyncValidation_WithComplexValidation_ShouldWork()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddObjectPool<ValidatableResource>(builder => builder
            .WithFactory(() => new ValidatableResource())
            .WithAsyncValidation(async resource =>
            {
                // Simulate complex async validation (e.g., checking network connection)
                await Task.Delay(5);

                // Simulate checking if connection is still alive
                return resource.IsValid && resource.Id > 0;
            })
            .WithMaxSize(10));

        var provider = services.BuildServiceProvider();
        var pool = provider.GetRequiredService<IObjectPool<ValidatableResource>>();

        // Act
        var pooled = pool.GetObject();
        pooled.Unwrap().Unwrap().Id = 1;
        pooled.Unwrap().Unwrap().IsValid = true;

        await pool.ReturnObjectAsync(pooled.Unwrap());

        // Assert
        Assert.Equal(1, pool.AvailableObjectCount);
    }

    [Fact]
    public async Task AsyncValidation_TakesPrecedenceOver_SyncValidation()
    {
        // Arrange
        var asyncCalled = false;
        var syncCalled = false;

        var config = new PoolConfiguration<ValidatableResource>()
        {
            ValidateOnReturn = true,
            ValidationFunction = obj =>
            {
                syncCalled = true;
                return Unit.Value;
            },
            AsyncValidationFunction = async obj =>
            {
                await Task.Delay(1);
                asyncCalled = true;
                return Unit.Value;
            }
        };

        var resource = new ValidatableResource();
        var pool = new ObjectPool<ValidatableResource>(new List<ValidatableResource> { resource }, config);
        var pooled = pool.GetObject();

        // Act
        await pool.ReturnObjectAsync(pooled.Unwrap());

        // Assert
        Assert.True(asyncCalled);
        Assert.False(syncCalled);
    }

    [Fact]
    public async Task ReturnObjectAsync_FallsBackToSyncValidation_WhenNoAsyncValidation()
    {
        // Arrange
        var syncCalled = false;

        var config = new PoolConfiguration<ValidatableResource>
        {
            ValidateOnReturn = true,
            ValidationFunction = obj =>
            {
                syncCalled = true;
                return Unit.Value;
            }
        };

        var resource = new ValidatableResource();
        var pool = new ObjectPool<ValidatableResource>(new List<ValidatableResource> { resource }, config);
        var pooled = pool.GetObject();

        // Act
        await pool.ReturnObjectAsync(pooled.Unwrap());

        // Assert
        Assert.True(syncCalled);
    }
}



