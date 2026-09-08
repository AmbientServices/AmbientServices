using System;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices;

/// <summary>
/// An interface that abstracts an ambient caching service.
/// </summary>
/// <remarks>
/// This interface works with serializable objects.
/// Objects that contain pointers, and objects that are disposable (<see cref="IDisposable"/> or <see cref="IAsyncDisposable"/>), must never be used with this interface — the Pledge below says why, and states the one exception.
/// For that kind of object, see <see cref="IAmbientLocalCache"/>.
/// <pitch>
/// A cache for serializable objects that may live outside the process — in another process or on another machine — so that every server sharing the backing store sees the same entries.
/// Because entries may cross a process boundary, it only suits values that are fully serializable and carry no object references or dispose responsibilities; for those, use <see cref="IAmbientLocalCache"/>.
/// </pitch>
/// <pledge>
/// A string-keyed item store: storing under a key replaces whatever that key held, and retrieval returns the most recent unexpired value stored under the key (possibly a deserialized copy rather than the original instance), or null when the key is missing, expired, or evicted.
/// Entries are cache entries, not durable storage — an implementation may discard any entry at any time under capacity or memory pressure, so a miss is always a legal answer and callers must be able to rebuild the value from its inputs.
/// Expiration may be given as a relative duration, a fixed instant, or both, in which case the earlier applies; retrieval may optionally extend an entry's lifespan, though implementations may ignore the extension.
/// Entries must be serializable, and disposable entries — anything implementing <see cref="IDisposable"/> or <see cref="IAsyncDisposable"/> whose disposal actually matters — must never be stored, because dispose ownership cannot be established for an entry that may leave the process.
/// Storing serializes the item, so a later retrieval may hand back a fresh copy, in this process or in another one, and neither side can name an owner for those copies: the store cannot know what a handle inside a blob refers to, and the caller never learns how many copies exist or when the last reader finished with one.
/// This interface therefore promises nothing in either direction — a caller may neither rely on the cache disposing an entry nor assume it will leave one alone, since an in-process realization may well dispose what it evicts — so with no owner on either side a live handle either leaks or is disposed out from under a reader.
/// The signatures say the same thing: <see cref="Store{T}"/> offers no dispose-on-discard option and <see cref="Remove{T}"/> hands nothing back, so ownership never moves and the caller keeps, and must itself dispose, the instance it stored.
/// Nothing enforces the rule — storing a disposable item raises no error — so it fails later instead, as a leaked handle, a copy that deserialized into a useless one, or an <see cref="ObjectDisposedException"/> from a realization that disposed what it evicted.
/// The one exception is a type that implements a disposal interface without owning anything whose release matters (nothing unmanaged, pooled, or external) and that survives a serialization round trip intact, such as a <see cref="System.IO.MemoryStream"/>-backed payload: leaving such an entry undisposed costs nothing, so caching it is safe.  Items with real dispose responsibilities belong in <see cref="IAmbientLocalCache"/>, which can transfer ownership precisely because its entries never leave the process.
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
    /// <paramref name="item"/> must be serializable and must not be disposable: storing never transfers ownership, so the caller keeps its instance and remains responsible for disposing it.  See the type's remarks for why disposable entries cannot be cached here, and for the one exception.
    /// </remarks>
    ValueTask Store<T>(string itemKey, T item, TimeSpan? maxCacheDuration = null, DateTime? expiration = null, CancellationToken cancel = default) where T : class;
    /// <summary>
    /// Removes the specified item from the cache.
    /// </summary>
    /// <typeparam name="T">The type of the item to be cached.</typeparam>
    /// <param name="itemKey">A string that uniquely identifies the item being cached.</param>
    /// <param name="cancel">The optional <see cref="CancellationToken"/>.</param>
    /// <remarks>
    /// Nothing is handed back, unlike <see cref="IAmbientLocalCache.Remove{T}"/>, because a shared cache never holds ownership of an entry.
    /// </remarks>
    ValueTask Remove<T>(string itemKey, CancellationToken cancel = default);
    /// <summary>
    /// Flushes everything from the cache.
    /// </summary>
    /// <param name="cancel">The optional <see cref="CancellationToken"/>.</param>
    ValueTask Clear(CancellationToken cancel = default);
}
