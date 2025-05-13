using AmbientServices.Utilities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices;

/// <summary>
/// A class that provides caching using the local cache, falling back to the shared cache if not found, and storing/deleting from both.
/// </summary>
public class AmbientTwoStageCache
{
    private static readonly AmbientService<IAmbientLocalCache> _LocalCache = Ambient.GetService<IAmbientLocalCache>();
    private static readonly AmbientService<IAmbientSharedCache> _SharedCache = Ambient.GetService<IAmbientSharedCache>();

    private readonly Type _type;
    private readonly string _defaultCachePrefix;
    private readonly IAmbientLocalCache? _explicitLocalCache;
    private readonly IAmbientSharedCache? _explicitSharedCache;
    private readonly string _cacheKeyPrefix;

    /// <summary>
    /// Creates the AmbientTwoStageCache using the ambient cache service.
    /// </summary>
    /// <param name="ownerType">The <see cref="Type"/> for the owner.</param>
    /// <param name="cacheKeyPrefix">An optional cache key prefix for all items cached through this class.  Uses the type name if not specified.</param>
    public AmbientTwoStageCache(Type ownerType, string? cacheKeyPrefix = null)
        : this(ownerType, null, null, cacheKeyPrefix)
    {
    }
    /// <summary>
    /// Creates the AmbientTwoStageCache using the specified cache service.
    /// </summary>
    /// <param name="ownerType">The <see cref="Type"/> for the owner.</param>
    /// <param name="localCache">An explicit <see cref="IAmbientLocalCache"/> to use.</param>
    /// <param name="sharedCache">An explicit <see cref="IAmbientSharedCache"/> to use.</param>
    /// <param name="cacheKeyPrefix">An optional cache key prefix for all items cached through this class.  Uses the type name if not specified.</param>
    public AmbientTwoStageCache(Type ownerType, IAmbientLocalCache? localCache, IAmbientSharedCache? sharedCache, string? cacheKeyPrefix = null)
    {
        _type = ownerType;
        _defaultCachePrefix = $"{_type.Name}-";
        _explicitLocalCache = localCache;
        _explicitSharedCache = sharedCache;
        _cacheKeyPrefix = cacheKeyPrefix ?? _defaultCachePrefix;
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
        string key = _cacheKeyPrefix + itemKey;
        IAmbientLocalCache? localCache = _explicitLocalCache ?? _LocalCache.Local;
        if (localCache != null) return localCache.Retrieve<T>(key, refresh, cancel);
        // fall back to the shared cache
        IAmbientSharedCache? sharedCache = _explicitSharedCache ?? _SharedCache.Local;
        if (sharedCache != null) return sharedCache.Retrieve<T>(key, refresh, cancel);
        return TaskUtilities.ValueTaskFromResult<T?>(null);
    }
    /// <summary>
    /// Stores the specified item in the cache.
    /// </summary>
    /// <typeparam name="T">The type of the item to be cached.</typeparam>
    /// <param name="itemKey">A string that uniquely identifies the item being cached.</param>
    /// <param name="item">The item to be cached.</param>
    /// <param name="maxCacheDuration">An optional <see cref="TimeSpan"/> indicating the maximum amount of time to keep the item in the cache.</param>
    /// <param name="expiration">An optional <see cref="DateTime"/> indicating a fixed time for when the item should expire from the cache.</param>
    /// <param name="cancel">The optional <see cref="CancellationToken"/>.</param>
    /// <remarks>
    /// If both <paramref name="expiration"/> and <paramref name="maxCacheDuration"/> are set, the earlier expiration will be used.
    /// </remarks>
    public async ValueTask Store<T>(string itemKey, T item, TimeSpan? maxCacheDuration = null, DateTime? expiration = null, CancellationToken cancel = default) where T : class
    {
        string key = _cacheKeyPrefix + itemKey;
        IAmbientLocalCache? localCache = _explicitLocalCache ?? _LocalCache.Local;
        if (localCache != null) await localCache.Store(key, item, false, maxCacheDuration, expiration, cancel).ConfigureAwait(false);
        IAmbientSharedCache? sharedCache = _explicitSharedCache ?? _SharedCache.Local;
        if (sharedCache != null) await sharedCache.Store(key, item, maxCacheDuration, expiration, cancel).ConfigureAwait(false);
    }
    /// <summary>
    /// Removes the specified item from the cache.
    /// </summary>
    /// <typeparam name="T">The type of the item to be cached.</typeparam>
    /// <param name="itemKey">A string that uniquely identifies the item being cached.</param>
    /// <param name="cancel">The optional <see cref="CancellationToken"/>.</param>
    public async ValueTask Remove<T>(string itemKey, CancellationToken cancel = default)
    {
        string key = _cacheKeyPrefix + itemKey;
        IAmbientLocalCache? localCache = _explicitLocalCache ?? _LocalCache.Local;
        if (localCache != null) await localCache.Remove<T>(key, cancel).ConfigureAwait(false);
        IAmbientSharedCache? sharedCache = _explicitSharedCache ?? _SharedCache.Local;
        if (sharedCache != null) await sharedCache.Remove<T>(key, cancel).ConfigureAwait(false);
    }
    /// <summary>
    /// Flushes everything from the cache.
    /// </summary>
    /// <param name="cancel">The optional <see cref="CancellationToken"/>.</param>
    public async ValueTask Clear(CancellationToken cancel = default)
    {
        IAmbientLocalCache? localCache = _explicitLocalCache ?? _LocalCache.Local;
        if (localCache != null) await localCache.Clear(cancel).ConfigureAwait(false);
        IAmbientSharedCache? sharedCache = _explicitSharedCache ?? _SharedCache.Local;
        if (sharedCache != null) await sharedCache.Clear(cancel).ConfigureAwait(false);
    }
}

/// <summary>
/// A generic type-specific two-stage cache owner class.  The name of the type is prepended to each cache key.
/// </summary>
/// <typeparam name="TOWNER">The type that owns the log messages.</typeparam>
public class AmbientTwoStageCache<TOWNER> : AmbientTwoStageCache
{
    /// <summary>
    /// Creates the AmbientTwoStageCache using the ambient cache service.
    /// </summary>
    /// <param name="cacheKeyPrefix">An optional cache key prefix for all items cached through this class.  Uses the type name if not specified.</param>
    public AmbientTwoStageCache(string? cacheKeyPrefix = null)
        : this(null, null, cacheKeyPrefix)
    {
    }
    /// <summary>
    /// Creates the AmbientTwoStageCache using the specified cache service.
    /// </summary>
    /// <param name="localCache">An explicit <see cref="IAmbientLocalCache"/> to use.</param>
    /// <param name="sharedCache">An explicit <see cref="IAmbientSharedCache"/> to use.</param>
    /// <param name="cacheKeyPrefix">An optional cache key prefix for all items cached through this class.  Uses the type name if not specified.</param>
    public AmbientTwoStageCache(IAmbientLocalCache? localCache, IAmbientSharedCache? sharedCache, string? cacheKeyPrefix = null) : base(typeof(TOWNER), localCache, sharedCache, cacheKeyPrefix)
    {
    }
}
