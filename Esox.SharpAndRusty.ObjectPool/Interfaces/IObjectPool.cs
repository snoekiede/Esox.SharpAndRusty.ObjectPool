using System;
using System.Collections.Generic;
using System.Text;
using Esox.SharpAndRusty.ObjectPool.Models;
using Esox.SharpAndRusty.Types;

namespace Esox.SharpAndRusty.ObjectPool.Interfaces;

public interface IObjectPool<T>
{
    int AvailableObjectCount { get; }

    ExtendedResult<PoolModel<T>, Error> GetObject();

    ExtendedResult<Unit, Error> ReturnObject(PoolModel<T> obj);

    ValueTask<ExtendedResult<Unit, Error>> ReturnObjectAsync(PoolModel<T> obj);
    
    Task<ExtendedResult<PoolModel<T>,Error>> GetObjectAsync(TimeSpan timeout=default,CancellationToken cancellationToken=default);
    
}

