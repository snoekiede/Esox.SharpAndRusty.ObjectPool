
namespace Esox.SharpAndRusty.ObjectPool.DependencyInjection;

/// <summary>
/// Extension methods for configuring async operations in object pools
/// </summary>
public static class AsyncOperationsExtensions
{
    extension<T>(ObjectPoolBuilder<T> builder) where T : class
    {
        /// <summary>
        /// Configures async validation for returned objects
        /// </summary>
        /// <typeparam name="T">The type of object in the pool</typeparam>
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
                        ? Types.ExtendedResult<Types.Unit, Types.Error>.Ok(Types.Unit.Value)
                        : Types.ExtendedResult<Types.Unit, Types.Error>.Err(Types.Error.New("Async validation failed"));
                };
            });

            return builder;
        }

        /// <summary>
        /// Enables async disposal for pooled objects (enabled by default)
        /// </summary>
        /// <typeparam name="T">The type of object in the pool</typeparam>
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

