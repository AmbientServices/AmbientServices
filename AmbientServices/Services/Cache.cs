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
/// <pitch>
/// A cache for serializable objects that may live outside the process — in another process or on another machine — so that every server sharing the backing store sees the same entries.
/// Because entries may cross a process boundary, it only suits values that are fully serializable and carry no object references or dispose responsibilities; for those, use <see cref="IAmbientLocalCache"/>.
/// </pitch>
/// <pledge>
/// A string-keyed item store: storing under a key replaces whatever that key held, and retrieval returns the most recent unexpired value stored under the key (possibly a deserialized copy rather than the original instance), or null when the key is missing, expired, or evicted.
/// Entries are cache entries, not durable storage — an implementation may discard any entry at any time under capacity or memory pressure, so a miss is always a legal answer and callers must be able to rebuild the value from its inputs.
/// Expiration may be given as a relative duration, a fixed instant, or both, in which case the earlier applies; retrieval may optionally extend an entry's lifespan, though implementations may ignore the extension.
/// All operations are asynchronous and honor cooperative cancellation.  Clearing flushes every entry in the cache, not just the caller's.
/// </pledge>
/// <priority>
/// 1. Being shareable over what it can hold: values must be fully serializable, carrying no object references and no dispose responsibilities, because an entry may cross a process boundary and come back as a copy rather than the original instance.  <see cref="IAmbientLocalCache"/> makes the opposite trade, and the pair exists so the caller picks rather than the library. (public)
/// 2. A legal miss over a guaranteed hit: any entry may be discarded at any time under capacity or memory pressure, and callers must always be able to rebuild a value from its inputs.  This is what keeps a cache a cache — a sibling that promised retention would be storage, and would owe callers answers about durability, capacity, and eviction that this interface deliberately refuses to give. (public)
/// </priority>
/// </remarks>
public interface IAmbientSharedCache
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
