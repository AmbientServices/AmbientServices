using AmbientServices.Utilities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices;

/// <summary>
/// A class that provides caching using either a specified cache or the ambient shared cache (<see cref="IAmbientSharedCache"/>).
/// </summary>
public class AmbientSharedCache
{
    private static readonly AmbientService<IAmbientSharedCache> _Cache = Ambient.GetService<IAmbientSharedCache>();

    private readonly Type _type;
    private readonly string _defaultCachePrefix;
    private readonly IAmbientSharedCache? _explicitCache;
    private readonly string _cacheKeyPrefix;

    /// <summary>
    /// Creates the AmbientSharedCache using the ambient cache service.
    /// </summary>
    /// <param name="ownerType">The <see cref="Type"/> for the owner.</param>
    /// <param name="cacheKeyPrefix">An optional cache key prefix for all items cached through this class.  Uses the type name if not specified.</param>
    public AmbientSharedCache(Type ownerType, string? cacheKeyPrefix = null)
        : this(ownerType, null, cacheKeyPrefix)
    {
    }
    /// <summary>
    /// Creates the AmbientSharedCache using the specified cache service.
    /// </summary>
    /// <param name="ownerType">The <see cref="Type"/> for the owner.</param>
    /// <param name="cache">An explicit <see cref="IAmbientSharedCache"/> to use.</param>
    /// <param name="cacheKeyPrefix">An optional cache key prefix for all items cached through this class.  Uses the type name if not specified.</param>
    public AmbientSharedCache(Type ownerType, IAmbientSharedCache? cache, string? cacheKeyPrefix = null)
    {
        _type = ownerType;
        _defaultCachePrefix = $"{_type.Name}-";
        _explicitCache = cache;
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
        IAmbientSharedCache? cache = _explicitCache ?? _Cache.Local;
        if (cache == null) return TaskUtilities.ValueTaskFromResult<T?>(null);
        return cache.Retrieve<T>(_cacheKeyPrefix + itemKey, refresh, cancel);
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
    public ValueTask Store<T>(string itemKey, T item, TimeSpan? maxCacheDuration = null, DateTime? expiration = null, CancellationToken cancel = default) where T : class
    {
        IAmbientSharedCache? cache = _explicitCache ?? _Cache.Local;
        if (cache == null) return default;
        return cache.Store<T>(_cacheKeyPrefix + itemKey, item, maxCacheDuration, expiration, cancel);
    }
    /// <summary>
    /// Removes the specified item from the cache.
    /// </summary>
    /// <typeparam name="T">The type of the item to be cached.</typeparam>
    /// <param name="itemKey">A string that uniquely identifies the item being cached.</param>
    /// <param name="cancel">The optional <see cref="CancellationToken"/>.</param>
    public ValueTask Remove<T>(string itemKey, CancellationToken cancel = default)
    {
        IAmbientSharedCache? cache = _explicitCache ?? _Cache.Local;
        if (cache == null) return default;
        return cache.Remove<T>(_cacheKeyPrefix + itemKey, cancel);
    }
    /// <summary>
    /// Flushes everything from the cache.
    /// </summary>
    /// <param name="cancel">The optional <see cref="CancellationToken"/>.</param>
    public ValueTask Clear(CancellationToken cancel = default)
    {
        IAmbientSharedCache? cache = _explicitCache ?? _Cache.Local;
        if (cache == null) return default;
        return cache.Clear(cancel);
    }
}

/// <summary>
/// A generic type-specific shared cache owner class.  The name of the type is prepended to each cache key.
/// </summary>
/// <typeparam name="TOWNER">The type that owns the log messages.</typeparam>
public class AmbientSharedCache<TOWNER> : AmbientSharedCache
{
    /// <summary>
    /// Creates the AmbientSharedCache using the ambient cache service.
    /// </summary>
    /// <param name="cacheKeyPrefix">An optional cache key prefix for all items cached through this class.  Uses the type name if not specified.</param>
    public AmbientSharedCache(string? cacheKeyPrefix = null)
        : this(null, cacheKeyPrefix)
    {
    }
    /// <summary>
    /// Creates the AmbientSharedCache using the specified cache service.
    /// </summary>
    /// <param name="cache">An explicit <see cref="IAmbientSharedCache"/> to use.</param>
    /// <param name="cacheKeyPrefix">An optional cache key prefix for all items cached through this class.  Uses the type name if not specified.</param>
    public AmbientSharedCache(IAmbientSharedCache? cache, string? cacheKeyPrefix = null) : base(typeof(TOWNER), cache, cacheKeyPrefix)
    {
    }
}


// Remove these obsolete classes in 2026
/// <summary>
/// A class that provides caching using either a specified cache or the ambient shared cache (<see cref="IAmbientSharedCache"/>).
/// </summary>
[Obsolete("Rename all references to AmbientSharedCache before 2026")]
public class AmbientCache : AmbientSharedCache
{
    /// <summary>
    /// Creates the AmbientSharedCache using the ambient cache service.
    /// </summary>
    /// <param name="ownerType">The <see cref="Type"/> for the owner.</param>
    /// <param name="cacheKeyPrefix">An optional cache key prefix for all items cached through this class.  Uses the type name if not specified.</param>
    public AmbientCache(Type ownerType, string? cacheKeyPrefix = null)
        : this(ownerType, null, cacheKeyPrefix)
    {
    }
    /// <summary>
    /// Creates the AmbientSharedCache using the specified cache service.
    /// </summary>
    /// <param name="ownerType">The <see cref="Type"/> for the owner.</param>
    /// <param name="cache">An explicit <see cref="IAmbientSharedCache"/> to use.</param>
    /// <param name="cacheKeyPrefix">An optional cache key prefix for all items cached through this class.  Uses the type name if not specified.</param>
    public AmbientCache(Type ownerType, IAmbientSharedCache? cache, string? cacheKeyPrefix = null)
        : base (ownerType, cache, cacheKeyPrefix)
    {
    }
}
/// <summary>
/// A generic type-specific shared cache owner .  The name of the type is prepended to each cache key.
/// </summary>
/// <typeparam name="TOWNER">The type that owns the log messages.</typeparam>
[Obsolete("Rename all references to AmbientSharedCache before 2026")]
public class AmbientCache<TOWNER> : AmbientSharedCache
{
    /// <summary>
    /// Creates the AmbientCache using the ambient cache service.
    /// </summary>
    /// <param name="cacheKeyPrefix">An optional cache key prefix for all items cached through this class.  Uses the type name if not specified.</param>
    public AmbientCache(string? cacheKeyPrefix = null)
        : this(null, cacheKeyPrefix)
    {
    }
    /// <summary>
    /// Creates the AmbientCache using the specified cache service.
    /// </summary>
    /// <param name="cache">An explicit <see cref="IAmbientSharedCache"/> to use.</param>
    /// <param name="cacheKeyPrefix">An optional cache key prefix for all items cached through this class.  Uses the type name if not specified.</param>
    public AmbientCache(IAmbientSharedCache? cache, string? cacheKeyPrefix = null) : base(typeof(TOWNER), cache, cacheKeyPrefix)
    {
    }
}
