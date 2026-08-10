using System;
using System.Collections.Generic;
using System.Text;
using Esox.SharpAndRusty.ObjectPool.Models;
using Esox.SharpAndRusty.Types;

namespace Esox.SharpAndRusty.ObjectPool.Interfaces;

public interface IObjectPool<T>
{
    int AvailableObjectCount { get; }

    Result<PoolModel<T>, Error> GetObject();

    void ReturnObject(PoolModel<T> obj);

    ValueTask ReturnObjectAsync(PoolModel<T> obj);
    
    Task<Result<PoolModel<T>,Error>> GetObjectAsync(TimeSpan timeout=default,CancellationToken cancellationToken=default);
    
}

