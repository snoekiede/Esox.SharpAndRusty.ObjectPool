using Esox.SharpAndRusty.ObjectPool.Pools;
using Esox.SharpAndRusty.Extensions;

namespace Esox.SharpAndRusty.ObjectPool.Tests;

public class AsyncPoolTests
{
    [Fact]
    public async Task TestAsyncRetrieval()
    {
        // Arrange
        var initialObjects = new List<int> { 1, 2, 3 };
        var pool = new ObjectPool<int>(initialObjects);

        // Act
        var model = await pool.GetObjectAsync();

        // Assert
        Assert.NotNull(model);

        // Cleanup
        model.Unwrap().Dispose();
    }

    [Fact]
    public async Task TestAsyncWithTimeout()
    {
        // Arrange
        var initialObjects = new List<int> { 1 };
        var pool = new ObjectPool<int>(initialObjects);

        // Get the only object so the pool is empty
        using var obj = pool.GetObject().Unwrap();

        // Act
        var result = await pool.GetObjectAsync(TimeSpan.FromMilliseconds(50));

        // Assert - timeout surfaces as a failure result, not an exception
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task TestAsyncCancellation()
    {
        // Arrange
        var initialObjects = new List<int> { 1 };
        var pool = new ObjectPool<int>(initialObjects);

        // Get the only object so the pool is empty
        using var obj = pool.GetObject().Unwrap();

        // Act & Assert
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await pool.GetObjectAsync(cancellationToken: cts.Token));
    }
}