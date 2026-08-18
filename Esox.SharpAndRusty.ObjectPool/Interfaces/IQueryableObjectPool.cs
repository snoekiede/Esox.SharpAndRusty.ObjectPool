using Esox.SharpAndRusty.ObjectPool.Models;
using Esox.SharpAndRusty.Types;

namespace Esox.SharpAndRusty.ObjectPool.Interfaces;

/// <summary>
/// Queryable object pool
/// </summary>
/// <typeparam name="T">The object to be stored in the pool</typeparam>
public interface IQueryableObjectPool<T> : IObjectPool<T>
{
    /// <summary>
    /// Returns an object from the pool. If no objects are available, an exception is thrown.
    /// </summary>
    /// <param name="query">The query to be performed</param>
    /// <returns>an object from the pool</returns>
    public ExtendedResult<PoolModel<T>, Error> GetObject(Func<T, bool> query);

    /// <summary>
    /// Try to get an object from the pool that matches the query without throwing an exception
    /// </summary>
    /// <param name="query">The query to be performed</param>
    /// <param name="poolModel">The pool model if successful</param>
    /// <returns>True if a matching object was retrieved, false otherwise</returns>
    public ExtendedResult<Unit,Error> TryGetObject(Func<T, bool> query, out PoolModel<T>? poolModel);
}


