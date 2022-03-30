using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices
{
    /// <summary>
    /// An interface that abstracts a local ambient caching service.
    /// </summary>
    /// <remarks>
    /// Note that a local cache differs from a shared/remote cache in that it can properly cache objects that contain pointers as well as disposable objects.
    /// For non-local cache, see <see cref="IAmbientLocalCache"/>.
    /// </remarks>
    public interface IAmbientLocalCache
    {
        /// <summary>
        /// Retrieves the item with the specified key from the cache (if possible).
        /// If the item is <see cref="IDisposable"/> and disposeWhenDiscarding was specified when the item was added to the cache, the item will be removed from the cache when retrieved.
        /// This ensures that retrieved items are not disposed while still in use by callers.
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
        /// <param name="disposeWhenDiscarding">Whether or not to dispose <see cref="IDisposable"/> items when discarding items from the cache.  If true, this will result in <see cref="ObjectDisposedException"/>s if the items are still in use when they get discarded, so when true, items will be automatically removed from the cache when they are retrieved.  This results in items only being availble to one client at a time.</param>
        /// <param name="maxCacheDuration">An optional <see cref="TimeSpan"/> indicating the maximum amount of time to keep the item in the cache.</param>
        /// <param name="expiration">An optional <see cref="DateTime"/> indicating a fixed time for when the item should expire from the cache.</param>
        /// <param name="cancel">The optional <see cref="CancellationToken"/>.</param>
        /// <remarks>
        /// If both <paramref name="expiration"/> and <paramref name="maxCacheDuration"/> are set, the earlier expiration will be used.
        /// </remarks>
        ValueTask Store<T>(string itemKey, T item, bool disposeWhenDiscarding = false, TimeSpan? maxCacheDuration = null, DateTime? expiration = null, CancellationToken cancel = default) where T : class;
        /// <summary>
        /// Removes the specified item from the cache and return it.  If disposable, ownership is transferred to the caller.
        /// </summary>
        /// <typeparam name="T">The type of the item to be cached.</typeparam>
        /// <param name="itemKey">A string that uniquely identifies the item being cached.</param>
        /// <param name="cancel">The optional <see cref="CancellationToken"/>.</param>
        /// <returns>The removed item, or default if the item was not found.</returns>
        ValueTask<T?> Remove<T>(string itemKey, CancellationToken cancel = default);
        /// <summary>
        /// Flushes everything from the cache.
        /// </summary>
        /// <param name="cancel">The optional <see cref="CancellationToken"/>.</param>
        ValueTask Clear(CancellationToken cancel = default);
    }
}
