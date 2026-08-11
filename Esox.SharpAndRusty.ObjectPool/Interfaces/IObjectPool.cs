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

    Result<Unit, Error> ReturnObject(PoolModel<T> obj);

    ValueTask<Result<Unit, Error>> ReturnObjectAsync(PoolModel<T> obj);
    
    Task<Result<PoolModel<T>,Error>> GetObjectAsync(TimeSpan timeout=default,CancellationToken cancellationToken=default);
    
}

