using AmbientServices.Utilities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices;

/// <summary>
/// A class that provides caching using the local cache, falling back to the shared cache if not found, and storing/deleting from both.
/// </summary>
/// <remarks>
/// <pitch>Two-tier caching in one call: reads prefer the fast in-process tier while stores and removals are applied to both the local and shared tiers, so repeated nearby lookups stay cheap and other servers can still see the value.  Only serializable values belong here, since everything is also written to the shared tier.</pitch>
/// <pledge>
/// Stores and removals are applied to the local tier and then the shared tier; the two writes are not transactional, so a failure partway can leave the tiers different.
/// Retrieval prefers the local tier and falls back to the shared tier on a local miss (or when no local cache is in effect).
/// All keys are prefixed with the owner type's name (or the supplied prefix) before reaching either tier.
/// When neither tier's service exists, every operation quietly succeeds without caching.
/// Clearing clears both underlying caches in their entirety, not merely this owner's entries.
/// </pledge>
/// <plan>
/// A stateless composition over two <see cref="AmbientService{T}"/> accessors (local and shared) with optional explicit overrides captured at construction; operations resolve each tier per call, prefix the key, and forward sequentially — local then shared — with no cross-tier coordination, retry, or rollback.
/// Local stores pass dispose-on-discard as false because anything cached here must also survive serialization to the shared tier.
/// Cost and durability per tier are exactly those of the underlying caches.
/// </plan>
/// </remarks>
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
    public async ValueTask<T?> Retrieve<T>(string itemKey, TimeSpan? refresh = null, CancellationToken cancel = default) where T : class
    {
        string key = _cacheKeyPrefix + itemKey;
        IAmbientLocalCache? localCache = _explicitLocalCache ?? _LocalCache.Local;
        IAmbientSharedCache? sharedCache = _explicitSharedCache ?? _SharedCache.Local;
        // prefer the local tier
        if (localCache != null)
        {
            T? local = await localCache.Retrieve<T>(key, refresh, cancel);
            if (local != null) return local;
        }
        // fall back to the shared tier on a local miss (or when no local cache is in effect)
        if (sharedCache != null) return await sharedCache.Retrieve<T>(key, refresh, cancel);
        return null;
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
        if (localCache != null) await localCache.Store(key, item, false, maxCacheDuration, expiration, cancel);
        IAmbientSharedCache? sharedCache = _explicitSharedCache ?? _SharedCache.Local;
        if (sharedCache != null) await sharedCache.Store(key, item, maxCacheDuration, expiration, cancel);
    }
    /// <summary>
    /// Removes the specified item from the cache.
    /// </summary>
    /// <typeparam name="T">The type of the item to be cached.</typeparam>
    /// <param name="itemKey">A string that uniquely identifies the item being cached.</param>
    /// <param name="cancel">The optional <see cref="CancellationToken"/>.</param>
    public async ValueTask Remove<T>(string itemKey, CancellationToken cancel = default) where T : class
    {
        string key = _cacheKeyPrefix + itemKey;
        IAmbientLocalCache? localCache = _explicitLocalCache ?? _LocalCache.Local;
        if (localCache != null) await localCache.Remove<T>(key, cancel);
        IAmbientSharedCache? sharedCache = _explicitSharedCache ?? _SharedCache.Local;
        if (sharedCache != null) await sharedCache.Remove<T>(key, cancel);
    }
    /// <summary>
    /// Flushes everything from the cache.
    /// </summary>
    /// <param name="cancel">The optional <see cref="CancellationToken"/>.</param>
    public async ValueTask Clear(CancellationToken cancel = default)
    {
        IAmbientLocalCache? localCache = _explicitLocalCache ?? _LocalCache.Local;
        if (localCache != null) await localCache.Clear(cancel);
        IAmbientSharedCache? sharedCache = _explicitSharedCache ?? _SharedCache.Local;
        if (sharedCache != null) await sharedCache.Clear(cancel);
    }
}

/// <summary>
/// A generic type-specific two-stage cache owner class.  The name of the type is prepended to each cache key.
/// </summary>
/// <typeparam name="TOWNER">The type that owns the log messages.</typeparam>
/// <remarks>
/// <pitch>The usual way to declare a two-stage cache: the owner is a type parameter, so the key prefix is derived at compile time and each class gets its own key namespace from a single static field.</pitch>
/// <pledge><see cref="AmbientTwoStageCache"/></pledge>
/// <plan>Passes <c>typeof(TOWNER)</c> to the base class; adds no behavior of its own.</plan>
/// </remarks>
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
