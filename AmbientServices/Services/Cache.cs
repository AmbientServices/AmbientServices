using System;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices;

/// <summary>
/// An interface that abstracts an ambient caching service.
/// </summary>
/// <remarks>
/// This interface works with serializable objects.
/// Objects that contain pointers or are disposable should not be used with this interface.
/// For that kind of object, see <see cref="IAmbientLocalCache"/>.
/// </remarks>
public interface IAmbientCache
{
    /// <summary>
    /// Retrieves the item with the specified key from the cache (if possible).
    /// </summary>
    /// <typeparam name="T">The type of the cached object.</typeparam>
    /// <param name="itemKey">The unique key used when the object was cached.</param>
    /// <param name="refresh">An optional <see cref="TimeSpan"/> indicating the length of time to extend the lifespan of the cached item.  Defaults to null, meaning not to update the expiration time.  Some implementations may ignore this value.</param>
    /// <param name="cancel">The optional <see cref="CancellationToken"/>.</param>
    /// <returns>The cached object, or null if it was not found in the cache.</returns>
    ValueTask<T?> Retrieve<T>(string itemKey, TimeSpan? refresh = null, CancellationToken cancel = default) where T : class;
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
    ValueTask Store<T>(string itemKey, T item, TimeSpan? maxCacheDuration = null, DateTime? expiration = null, CancellationToken cancel = default) where T : class;
    /// <summary>
    /// Removes the specified item from the cache.
    /// </summary>
    /// <typeparam name="T">The type of the item to be cached.</typeparam>
    /// <param name="itemKey">A string that uniquely identifies the item being cached.</param>
    /// <param name="cancel">The optional <see cref="CancellationToken"/>.</param>
    ValueTask Remove<T>(string itemKey, CancellationToken cancel = default);
    /// <summary>
    /// Flushes everything from the cache.
    /// </summary>
    /// <param name="cancel">The optional <see cref="CancellationToken"/>.</param>
    ValueTask Clear(CancellationToken cancel = default);
}
