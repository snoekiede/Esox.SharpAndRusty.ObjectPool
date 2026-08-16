using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esox.SharpAndRusty.ObjectPool.DependencyInjection;

public static class AsyncOperationsExtensions
{
    extension<T>(ObjectPoolBuilder<T> builder) where T : class
    {
        /// <summary>
        /// Configures async validation for returned objects
        /// </summary>
        /// <typeparam name="T">The type of object in the pool</typeparam>
        /// <param name="builder">The object pool builder</param>
        /// <param name="asyncValidationFunction">Async function to validate objects when returned to pool</param>
        /// <returns>The builder for method chaining</returns>
        public ObjectPoolBuilder<T> WithAsyncValidation(Func<T, ValueTask<bool>> asyncValidationFunction)
        {
            if (asyncValidationFunction == null)
                throw new ArgumentNullException(nameof(asyncValidationFunction));

            builder.Configure(config =>
            {
                config.ValidateOnReturn = true;
                config.AsyncValidationFunction = async obj =>
                {
                    var isValid = await asyncValidationFunction(obj).ConfigureAwait(false);
                    return isValid
                        ? Esox.SharpAndRusty.Types.ExtendedResult<Esox.SharpAndRusty.Types.Unit, Esox.SharpAndRusty.Types.Error>.Ok(Esox.SharpAndRusty.Types.Unit.Value)
                        : Esox.SharpAndRusty.Types.ExtendedResult<Esox.SharpAndRusty.Types.Unit, Esox.SharpAndRusty.Types.Error>.Err(Esox.SharpAndRusty.Types.Error.New("Async validation failed"));
                };
            });

            return builder;
        }

        /// <summary>
        /// Enables async disposal for pooled objects (enabled by default)
        /// </summary>
        /// <typeparam name="T">The type of object in the pool</typeparam>
        /// <param name="builder">The object pool builder</param>
        /// <param name="enable">Whether to enable async disposal</param>
        /// <returns>The builder for method chaining</returns>
        public ObjectPoolBuilder<T> WithAsyncDisposal(bool enable = true)
        {
            builder.Configure(config => config.UseAsyncDisposal = enable);
            return builder;
        }

        /// <summary>
        /// Configures the pool to use async lifecycle hooks
        /// </summary>
        /// <typeparam name="T">The type of object in the pool</typeparam>
        /// <param name="builder">The object pool builder</param>
        /// <param name="configureHooks">Action to configure async lifecycle hooks</param>
        /// <returns>The builder for method chaining</returns>
        public ObjectPoolBuilder<T> WithAsyncLifecycleHooks(
            Action<Lifecycle.LifecycleHooks<T>> configureHooks)
        {
            if (configureHooks == null)
                throw new ArgumentNullException(nameof(configureHooks));

            var hooks = new Lifecycle.LifecycleHooks<T>();
            configureHooks(hooks);

            builder.Configure(config => config.LifecycleHooks = hooks);
            return builder;
        }
    }
}

