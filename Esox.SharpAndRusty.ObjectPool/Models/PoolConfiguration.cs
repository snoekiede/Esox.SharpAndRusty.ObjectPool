using System;
using System.Collections.Generic;
using System.Text;
using Esox.SharpAndRusty.ObjectPool.CircuitBreaker;
using Esox.SharpAndRusty.ObjectPool.Eviction;
using Esox.SharpAndRusty.ObjectPool.Policies;
using Esox.SharpAndRusty.Types;

namespace Esox.SharpAndRusty.ObjectPool.Models;

public class PoolConfiguration<T>
{
    public int MaxPoolSize { get; set; } = int.MaxValue;

    public int MaxActiveObjects { get; set; } = int.MaxValue;

    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public bool ValidateOnReturn { get; set; } = false;

    public Func<T,ExtendedResult<Unit, Error>>? ValidationFunction { get; set; }

    public bool EnableDetailedStatistics { get; set; } = true;

    public EvictionConfiguration? EvictionConfiguration { get; set; }

    public CircuitBreakerConfiguration? CircuitBreakerConfiguration { get; set; }

    public object? LifecycleHooks { get; set; }

    public bool ContinueOnLifecycleHookError { get; set; } = true;

    public PoolingPolicyType PoolingPolicyType { get; set; } = PoolingPolicyType.Lifo;
    
    public object? PrioritySelector { get; set; }
    
    public Func<T,ValueTask<ExtendedResult<Unit,Error>>>? AsyncValidationFunction { get; set; }

    public bool UseAsyncDisposal { get; set; } = true;
}