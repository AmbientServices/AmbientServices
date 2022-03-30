using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices
{
    /// <summary>
    /// A class that provides caching using either a specified cache or the ambient cache.
    /// </summary>
    /// <typeparam name="TOWNER">The type that owns the items to be cached.</typeparam>
    public class AmbientLocalCache<TOWNER>
    {
        private static readonly string DefaultCacheKeyPrefix = typeof(TOWNER).Name + "-";
        private static readonly AmbientService<IAmbientLocalCache> _Cache = Ambient.GetService<IAmbientLocalCache>();

        private readonly IAmbientLocalCache? _explicitCache;
        private readonly string _cacheKeyPrefix = DefaultCacheKeyPrefix;

        /// <summary>
        /// Creates the AmbientCache using the ambient cache service.
        /// </summary>
        /// <param name="cacheKeyPrefix">An optional cache key prefix for all items cached through this class.  Uses the type name if not specified.</param>
        public AmbientLocalCache(string? cacheKeyPrefix = null)
            : this (null, cacheKeyPrefix)
        {
        }
        /// <summary>
        /// Creates the AmbientCache using the specified cache service.
        /// </summary>
        /// <param name="cache">An explicit <see cref="IAmbientLocalCache"/> to use.</param>
        /// <param name="cacheKeyPrefix">An optional cache key prefix for all items cached through this class.  Uses the type name if not specified.</param>
        public AmbientLocalCache(IAmbientLocalCache? cache, string? cacheKeyPrefix = null)
        {
            _explicitCache = cache;
            if (cacheKeyPrefix != null) _cacheKeyPrefix = cacheKeyPrefix;
        }
        /// <summary>
        /// Retrieves the item with the specified key from the cache (if possible).
        /// </summary>
        /// <typeparam name="T">The type of the cached object.</typeparam>
        /// <param name="itemKey">The unique key used when the object was cached.</param>
        /// <param name="refresh">An optional <see cref="TimeSpan"/> indicating the length of time to extend the lifespan of the cached item.  Defaults to null, meaning not to update the expiration time.  Some cache implementations may ignore this value.</param>
        /// <param name="cancel">The optional <see cref="CancellationToken"/>.</param>
        /// <returns>The cached object, or null if it was not found in the cache.</returns>
        public ValueTask<T?> Retrieve<T>(string itemKey, TimeSpan? refresh = null, CancellationToken cancel = default) where T : class
        {
            IAmbientLocalCache? cache = _explicitCache ?? _Cache.Local;
            if (cache == null) return TaskExtensions.ValueTaskFromResult<T?>(null);
            return cache.Retrieve<T>(_cacheKeyPrefix + itemKey, refresh, cancel);
        }
        /// <summary>
        /// Stores the specified item in the cache.
        /// </summary>
        /// <typeparam name="T">The type of the item to be cached.</typeparam>
        /// <param name="itemKey">A string that uniquely identifies the item being cached.</param>
        /// <param name="item">The item to be cached.</param>
        /// <param name="disposeWhenDiscarding">Whether or not to dispose <see cref="IDisposable"/> items when discarding items from the cache.  If true, this will result in <see cref="ObjectDisposedException"/>s if the items are still in use when they get discarded, so when true, items will be automatically removed from the cache when they are retrieved.  This results in items only being availble to one client at a time.</param>
        /// <param name="maxCacheDuration">An optional <see cref="TimeSpan"/> indicating the maximum amount of time to keep the item in the cache.</param>
        /// <param name="expiration">An optional <see cref="DateTime"/> indicating a fixed time for when the item should expire from the cache.</param>
        /// <param name="cancel">The optional <see cref="CancellationToken"/>.</param>
        /// 
        /// <remarks>
        /// If both <paramref name="expiration"/> and <paramref name="maxCacheDuration"/> are set, the earlier expiration will be used.
        /// </remarks>
        public ValueTask Store<T>(string itemKey, T item, bool disposeWhenDiscarding = false, TimeSpan? maxCacheDuration = null, DateTime? expiration = null, CancellationToken cancel = default) where T : class
        {
            IAmbientLocalCache? cache = _explicitCache ?? _Cache.Local;
            if (cache == null) return default;
            return cache.Store<T>(_cacheKeyPrefix + itemKey, item, disposeWhenDiscarding, maxCacheDuration, expiration, cancel);
        }
        /// <summary>
        /// Removes the specified item from the cache.
        /// </summary>
        /// <typeparam name="T">The type of the item to be cached.</typeparam>
        /// <param name="itemKey">A string that uniquely identifies the item being cached.</param>
        /// <param name="cancel">The optional <see cref="CancellationToken"/>.</param>
        /// <returns>The item that was removed, or default if the specified item was not found.</returns>
        public async ValueTask<T?> Remove<T>(string itemKey, CancellationToken cancel = default)
        {
            IAmbientLocalCache? cache = _explicitCache ?? _Cache.Local;
            if (cache == null) return default;
            return await cache.Remove<T>(_cacheKeyPrefix + itemKey, cancel).ConfigureAwait(false);
        }
        /// <summary>
        /// Flushes everything from the cache.
        /// </summary>
        /// <param name="cancel">The optional <see cref="CancellationToken"/>.</param>
        /// 
        public ValueTask Clear(CancellationToken cancel = default)
        {
            IAmbientLocalCache? cache = _explicitCache ?? _Cache.Local;
            if (cache == null) return default;
            return cache.Clear(cancel);
        }
    }
}
