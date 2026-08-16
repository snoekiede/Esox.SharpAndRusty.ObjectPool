using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esox.SharpAndRusty.ObjectPool.Policies;

public class LifoPoolingPolicy<T> : IPoolingPolicy<T> where T : notnull
{
    private readonly ConcurrentStack<T> _stack = new();

    /// <inheritdoc/>
    public string PolicyName => "LIFO";

    /// <inheritdoc/>
    public int Count => _stack.Count;

    /// <inheritdoc/>
    public void Add(T item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _stack.Push(item);
    }

    /// <inheritdoc/>
    public bool TryTake(out T? item)
    {
        return _stack.TryPop(out item);
    }

    /// <inheritdoc/>
    public void Clear()
    {
        _stack.Clear();
    }

    /// <inheritdoc/>
    public IEnumerable<T> GetAll()
    {
        return _stack.ToArray();
    }
}


