using Esox.SharpAndRusty.ObjectPool.Models;
using Esox.SharpAndRusty.Types;

namespace Esox.SharpAndRusty.ObjectPool.Interfaces;

/// <summary>
/// Interface for an object pool
/// </summary>
/// <typeparam name="T">The type of objects managed by the pool</typeparam>
public interface IObjectPool<T>
{
    /// <summary>
    /// Gets the number of available objects in the pool
    /// </summary>
    int AvailableObjectCount { get; }

    /// <summary>
    /// Retrieves an object from the pool
    /// </summary>
    ExtendedResult<PoolModel<T>, Error> GetObject();

    /// <summary>
    /// Returns an object to the pool
    /// </summary>
    ExtendedResult<Unit, Error> ReturnObject(PoolModel<T> obj);

    /// <summary>
    /// Asynchronously returns an object to the pool
    /// </summary>
    ValueTask<ExtendedResult<Unit, Error>> ReturnObjectAsync(PoolModel<T> obj);

    /// <summary>
    /// Asynchronously retrieves an object from the pool
    /// </summary>
    /// <param name="timeout">The maximum time to wait for an object to become available</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests</param>
    /// <returns>A task that represents the asynchronous operation, containing the result of the retrieval</returns>
    Task<ExtendedResult<PoolModel<T>,Error>> GetObjectAsync(TimeSpan timeout=default,CancellationToken cancellationToken=default);
    
}

