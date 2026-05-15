using System;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices;

/// <summary>
/// An interface that abstracts an in-memory cache with atomic add-or-update semantics and optional monotonic versioned entries for tiered or split-cache scenarios.
/// </summary>
/// <remarks>
/// <para>The default ambient implementation is <see cref="BasicAmbientAtomicCache"/>.  It is thread-safe and uses optimistic concurrency: <see cref="GetOrAdd{T}"/> and <see cref="AddOrUpdate{T}"/> may invoke factories more than once under contention, and implementations may discard a created or updated value if it loses a race to install the entry.</para>
/// <para>Unversioned operations (<see cref="GetOrAdd{T}"/>, <see cref="AddOrUpdate{T}"/>, <see cref="Remove{T}"/>) and versioned operations (<see cref="VersionedGet{T}"/>, <see cref="VersionedPut{T}"/>) use distinct storage for the same logical key string, so callers may use both families side by side without colliding.</para>
/// <para>Optional <c>timeout</c> and <c>cancel</c> parameters combine cooperative cancellation with an ambient-scoped time budget.  When <c>timeout</c> is null, no ambient timeout source is linked.  When <c>timeout</c> is zero or negative, the effective token is cancelled immediately (without using a zero-interval system timer).  When both are supplied and <c>cancel</c> can be canceled, the implementation links the caller token to that budget.  For <see cref="GetOrAdd{T}"/> and <see cref="AddOrUpdate{T}"/>, implementations also cap optimistic retries using <see cref="AmbientClock"/>; exceeding that budget throws <see cref="InvalidOperationException"/> rather than <see cref="OperationCanceledException"/> so policy timeouts are not mistaken for caller-initiated cancellation.</para>
/// <para>Monotonic split-cache helpers (versioned head plus unversioned payload per revision) live on <see cref="AmbientAtomicSplitCacheExtensions"/> so every target framework, including .NET Standard 2.0, exposes the same API without default interface members.</para>
/// </remarks>
public interface IAmbientAtomicCache
{
    /// <summary>
    /// Retrieves the item with the specified key from the cache when it is present and not expired; otherwise creates it using <paramref name="create"/> and returns the stored instance.
    /// </summary>
    /// <typeparam name="T">The type of the cached object.</typeparam>
    /// <param name="itemKey">The unique key used when the object was cached.</param>
    /// <param name="create">A factory that returns the new item and an optional UTC expiration instant (or null for no fixed expiration).  May run even when another thread wins the race to add the entry; in that case the implementation disposes the discarded instance when appropriate.</param>
    /// <param name="refresh">An optional <see cref="TimeSpan"/> indicating how long to extend the lifespan of the cached item when it is returned from the cache.  Defaults to null, meaning the expiration time is left unchanged.  Some implementations may ignore this value.</param>
    /// <param name="timeout">An optional <see cref="TimeSpan"/> for optimistic-retry and linked ambient cancellation.  Null skips linking an ambient timeout.  Zero or negative values cancel the linked token immediately and shorten the ambient-clock retry budget so the call can fail fast without relying on a wall-clock timer interval.</param>
    /// <param name="cancel">The optional <see cref="CancellationToken"/>.</param>
    /// <returns>The cached or created object.</returns>
    /// <exception cref="InvalidOperationException">The implementation exhausted its optimistic retry time budget.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled via <paramref name="cancel"/> or the linked timeout token.</exception>
    /// <remarks>
    /// On a cache hit with <paramref name="refresh"/> set, implementations typically enqueue an additional timed bookkeeping row; ejection logic may ignore superseded rows when the in-cache expiration no longer matches.
    /// </remarks>
    ValueTask<T> GetOrAdd<T>(string itemKey, Func<ValueTask<(T Item, DateTime? Expires)>> create, TimeSpan? refresh = null, TimeSpan? timeout = null, CancellationToken cancel = default) where T : class;
    /// <summary>
    /// If the item is not in the cache, creates it using <paramref name="create"/> and adds it; if the item is present and not expired, replaces it using <paramref name="update"/>.
    /// </summary>
    /// <typeparam name="T">The type of the cached object.</typeparam>
    /// <param name="itemKey">The unique key used when the object was cached.</param>
    /// <param name="create">A factory used when the key is missing.  During a race to add the item to the cache, may be called even though the returned item is not the one ultimately retained.</param>
    /// <param name="update">A factory that receives the current cached instance and returns the replacement and an optional UTC expiration.  May be invoked more than once when updates race, but each call receives a consistent snapshot of the value being replaced.</param>
    /// <param name="timeout">An optional <see cref="TimeSpan"/> for optimistic-retry and linked ambient cancellation.  Null skips linking an ambient timeout.  Zero or negative values cancel the linked token immediately and shorten the ambient-clock retry budget so the call can fail fast without relying on a wall-clock timer interval.</param>
    /// <param name="cancel">The optional <see cref="CancellationToken"/>.</param>
    /// <returns>The created or updated object that ended up in the cache.</returns>
    /// <exception cref="InvalidOperationException">The implementation exhausted its optimistic retry time budget.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled via <paramref name="cancel"/> or the linked timeout token.</exception>
    ValueTask<T> AddOrUpdate<T>(string itemKey, Func<ValueTask<(T Item, DateTime? Expires)>> create, Func<T, ValueTask<(T UpdatedItem, DateTime? Expires)>> update, TimeSpan? timeout = null, CancellationToken cancel = default) where T : class;
    /// <summary>
    /// Removes the specified unversioned item from the cache if present.
    /// </summary>
    /// <typeparam name="T">The type of the item being removed (used only for API symmetry).</typeparam>
    /// <param name="itemKey">A string that uniquely identifies the item being cached.</param>
    /// <param name="cancel">The optional <see cref="CancellationToken"/>.</param>
    /// <remarks>
    /// Implementations remove the live cache entry but may leave older timed bookkeeping rows in internal queues until they are skipped or drained during ejection.
    /// </remarks>
    ValueTask Remove<T>(string itemKey, CancellationToken cancel = default);
    /// <summary>
    /// Reads a monotonic-versioned cache entry: returns a payload only when the stored revision is at least <paramref name="minVersion"/>.
    /// Slightly stale reads are allowed; callers pass the largest revision they have already observed so they never accept an older payload.
    /// </summary>
    /// <typeparam name="T">The type of the cached object.</typeparam>
    /// <param name="itemKey">The unique key used when the object was cached.</param>
    /// <param name="minVersion">Minimum acceptable stored revision (use <c>-1</c> to accept any non-expired entry).</param>
    /// <param name="refresh">Optional extension of expiration when the entry is returned (file- and Redis-backed implementations apply the same sliding refresh rules as their non-versioned retrieve paths).</param>
    /// <param name="timeout">Optional linked ambient cancellation.  Null skips ambient timeout.  Zero or negative values cancel the linked token immediately.</param>
    /// <param name="cancel">The optional <see cref="CancellationToken"/>.</param>
    /// <returns>The cached object and its version, or default with the latest observed version when the key is missing, expired, or the stored revision is below <paramref name="minVersion"/>.</returns>
    /// <exception cref="OperationCanceledException">The operation was canceled via <paramref name="cancel"/> or the linked timeout token after a matching entry was found.</exception>
    ValueTask<(T? Value, long Version)> VersionedGet<T>(string itemKey, long minVersion = -1, TimeSpan? refresh = null, TimeSpan? timeout = null, CancellationToken cancel = default) where T : class;
    /// <summary>
    /// Writes a monotonic-versioned cache entry and returns its new revision (strictly greater than any revision previously stored for this key).
    /// </summary>
    /// <remarks>
    /// <para>Versioned entries use storage separate from legacy <see cref="GetOrAdd{T}"/> and other non-versioned paths for the same <paramref name="itemKey"/> (for example a <c>V/</c> path segment on disk or a <c>V:</c> key prefix in Redis), so the two mechanisms do not share one serialized blob.</para>
    /// </remarks>
    /// <typeparam name="T">The type of the cached object.</typeparam>
    /// <param name="itemKey">The unique key used when the object was cached.</param>
    /// <param name="value">The value to store.</param>
    /// <param name="maxCacheDuration">Optional maximum time to retain the entry.</param>
    /// <param name="expiration">Optional fixed UTC expiration instant.</param>
    /// <param name="timeout">Optional linked ambient cancellation.  Null skips ambient timeout.  Zero or negative values cancel the linked token immediately.</param>
    /// <param name="cancel">The optional <see cref="CancellationToken"/>.</param>
    /// <returns>The new monotonic revision of the stored entry, or zero when the value is discarded because duration or expiration arguments rule out caching.</returns>
    /// <exception cref="OperationCanceledException">The operation was canceled via <paramref name="cancel"/> or the linked timeout token.</exception>
    ValueTask<long> VersionedPut<T>(string itemKey, T value, TimeSpan? maxCacheDuration = null, DateTime? expiration = null, TimeSpan? timeout = null, CancellationToken cancel = default) where T : class;
    /// <summary>
    /// Removes every unversioned and versioned entry from the cache.
    /// </summary>
    /// <param name="cancel">The optional <see cref="CancellationToken"/>.</param>
    ValueTask Clear(CancellationToken cancel = default);
}
