using System.Collections.Concurrent;


namespace Esox.SharpAndRusty.ObjectPool.Policies;

/// <summary>
/// A pooling policy that follows a Last-In-First-Out (LIFO) strategy.
/// </summary>
/// <typeparam name="T">The type of the objects being pooled</typeparam>
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
        return [.. _stack];
    }
}


