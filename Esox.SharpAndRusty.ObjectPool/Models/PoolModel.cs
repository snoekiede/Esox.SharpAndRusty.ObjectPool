using System;
using System.Collections.Generic;
using System.Text;
using Esox.SharpAndRusty.ObjectPool.Interfaces;

namespace Esox.SharpAndRusty.ObjectPool.Models;

public sealed class PoolModel<T> : IDisposable
{
    private readonly T _value;

    private readonly IObjectPool<T> _pool;

    private int _returnGuard;

    private int _disposed;

    /// <summary>
    /// Constructor for the pool model
    /// </summary>
    /// <param name="value">The value to be wrapped</param>
    /// <param name="pool">The object pool to which this PoolModel belongs</param>
    public PoolModel(T value, IObjectPool<T> pool)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(pool);

        this._value = value;
        this._pool = pool;
    }

    /// <summary>
    /// Unwraps the value
    /// </summary>
    /// <returns>The value</returns>
    /// <exception cref="ObjectDisposedException">Thrown when trying to access a disposed object</exception>
    public T Unwrap()
    {
        ObjectDisposedException.ThrowIf(_disposed == 1, this);
        return this._value;
    }

    /// <summary>
    /// Returns the poolmodel to the pool. Thread-safe: exactly one caller will perform the return.
    /// </summary>
    public void Dispose()
    {
        // Only the thread that flips _returnGuard from 0→1 performs the actual return.
        // _disposed is set to 1 afterwards so that Unwrap() prevents further use.
        if (Interlocked.CompareExchange(ref _returnGuard, 1, 0) == 0)
        {
            this._pool.ReturnObject(this); // Unwrap() is safe here: _disposed is still 0
            Volatile.Write(ref _disposed, 1);
        }
    }

}

