using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices;

/// <summary>
/// Default in-process implementation of <see cref="IAmbientAtomicCache"/> using concurrent dictionaries and optimistic compare-and-swap style retries.
/// </summary>
/// <remarks>
/// <para>Bounded size is enforced by ejecting timed and untimed bookkeeping rows on a configurable cadence.  Settings use the prefix <c>BasicAmbientAtomicCache-</c> with keys <c>EjectFrequency</c>, <c>MaximumItemCount</c>, and <c>MinimumItemCount</c> (see <see cref="AmbientSettings"/>).</para>
/// <para>Expiration comparisons and optimistic retry deadlines use <see cref="AmbientClock"/> so tests can pause or skip virtual time deterministically.</para>
/// </remarks>
[DefaultAmbientService]
internal class BasicAmbientAtomicCache : IAmbientAtomicCache
{
    /// <summary>Distinct one-char prefix so unversioned and versioned logical keys never share one dictionary slot.</summary>
    private const string UnversionedStorageKeyPrefix = "N";

    /// <summary>Distinct one-char prefix so unversioned and versioned logical keys never share one dictionary slot.</summary>
    private const string VersionedStorageKeyPrefix = "V";

    /// <summary>Wall-clock budget for optimistic <see cref="GetOrAdd{T}"/> / <see cref="AddOrUpdate{T}"/> CAS-style retries (uses <see cref="AmbientClock"/>).</summary>
    private static readonly TimeSpan MaxOptimisticRetryDuration = TimeSpan.FromSeconds(30);

    private const string OptimisticRetryBudgetExceededMessage = "The atomic cache optimistic retry budget was exceeded.";

    private static readonly AmbientService<IAmbientSettingsSet> _Settings = Ambient.GetService<IAmbientSettingsSet>();

    private readonly IAmbientSetting<int> _callFrequencyToEject;
    private readonly IAmbientSetting<int> _countToEject;
    private readonly IAmbientSetting<int> _minCacheEntries;
    private int _expireCount;
    private ConcurrentQueue<TimedQueueEntry> _timedQueue = new();   // interlocked (make readonly when we no longer support frameworks without Clear())
    private ConcurrentQueue<string> _untimedQueue = new();          // interlocked (make readonly when we no longer support frameworks without Clear())
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly ConcurrentDictionary<string, VersionCounter> _versionCounters = new();

    private sealed class VersionCounter
    {
        internal long Last;
    }

    public BasicAmbientAtomicCache()
        : this(_Settings.Local)
    {
    }

    public BasicAmbientAtomicCache(IAmbientSettingsSet? settings)
    {
        _callFrequencyToEject = AmbientSettings.GetSetting<int>(settings, nameof(BasicAmbientAtomicCache) + "-EjectFrequency", "The number of cache calls between cache ejections where at least one timed and one untimed entry is ejected from the cache.", s => int.Parse(s!, System.Globalization.CultureInfo.InvariantCulture), "100");
        _countToEject = AmbientSettings.GetSetting<int>(settings, nameof(BasicAmbientAtomicCache) + "-MaximumItemCount", "The maximum number of both timed and untimed items to allow in the cache before ejecting items.", s => int.Parse(s!, System.Globalization.CultureInfo.InvariantCulture), "1000");
        _minCacheEntries = AmbientSettings.GetSetting<int>(settings, nameof(BasicAmbientAtomicCache) + "-MinimumItemCount", "The minimum number of unexpired both timed and untimed items to keep in the cache at all times.", s => int.Parse(s!, System.Globalization.CultureInfo.InvariantCulture), "1");
    }

    private static string GetUnversionedStorageKey(string itemKey) => UnversionedStorageKeyPrefix + itemKey;

    private static string GetVersionedStorageKey(string itemKey) => VersionedStorageKeyPrefix + itemKey;

    private struct TimedQueueEntry
    {
        public string Key;
        public DateTime Expiration;
    }

    private static bool ShouldDisposeWhenDiscarding(object entry)
    {
#if NET5_0_OR_GREATER
        return entry is IAsyncDisposable || entry is IDisposable;
#else
        return entry is IDisposable;
#endif
    }

    private readonly struct TimeoutCancellationRegistration : IDisposable
    {
        public CancellationToken Token { get; }
        private readonly IDisposable? _cleanup;

        public static TimeoutCancellationRegistration Create(TimeSpan? timeout, CancellationToken cancel)
        {
            if (timeout is not TimeSpan to)
            {
                return new TimeoutCancellationRegistration(null, cancel);
            }

            // System.Timers.Timer rejects non-positive intervals; use an already-cancelled CTS for "elapsed now" budgets.
            if (to <= TimeSpan.Zero)
            {
#pragma warning disable CA2000 // dispose ownership transferred to TimeoutCancellationRegistration or linked CTS
                CancellationTokenSource immediateTimeout = new();
#pragma warning restore CA2000
                immediateTimeout.Cancel();
                if (!cancel.CanBeCanceled)
                {
                    return new TimeoutCancellationRegistration(immediateTimeout, immediateTimeout.Token);
                }

                CancellationTokenSource linkedImmediateTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancel, immediateTimeout.Token);
                return new TimeoutCancellationRegistration(linkedImmediateTimeout, linkedImmediateTimeout.Token);
            }

            AmbientCancellationTokenSource ambientTimeout = new(to);
            if (!cancel.CanBeCanceled)
            {
                return new TimeoutCancellationRegistration(ambientTimeout, ambientTimeout.Token);
            }

            CancellationTokenSource linkedAmbientTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancel, ambientTimeout.Token);
            return new TimeoutCancellationRegistration(new TimeoutCleanup(linkedAmbientTimeout, ambientTimeout), linkedAmbientTimeout.Token);
        }

        private TimeoutCancellationRegistration(IDisposable? cleanup, CancellationToken token)
        {
            Token = token;
            _cleanup = cleanup;
        }

        public void Dispose() => _cleanup?.Dispose();
    }

    private sealed class TimeoutCleanup : IDisposable
    {
        private readonly CancellationTokenSource _linked;
        private readonly AmbientCancellationTokenSource _ambient;

        public TimeoutCleanup(CancellationTokenSource linked, AmbientCancellationTokenSource ambient)
        {
            _linked = linked;
            _ambient = ambient;
        }

        public void Dispose()
        {
            _linked.Dispose();
            _ambient.Dispose();
        }
    }

    private class CacheEntry
    {
        public bool DisposeWhenDiscarding { get; private set; }
        public string Key;
        public DateTime? Expiration;
        public object Entry;
        /// <summary>Monotonic revision for versioned storage keys; 0 for non-versioned atomic entries.</summary>
        public long MonotonicRevision;

        public CacheEntry(string key, DateTime? expiration, object entry, bool disposeWhenDiscarding, long monotonicRevision = 0)
        {
            DisposeWhenDiscarding = disposeWhenDiscarding;
            Key = key;
            Expiration = expiration;
            Entry = entry;
            MonotonicRevision = monotonicRevision;
        }
#if NET5_0_OR_GREATER
        public async ValueTask Dispose()
        {
            if (DisposeWhenDiscarding)
            {
                // if the entry is disposable, dispose it after removing it
                if (Entry is IAsyncDisposable asyncDisposable) await asyncDisposable.DisposeAsync();
                if (Entry is IDisposable disposable) disposable.Dispose();
            }
        }
#else
        public async ValueTask Dispose()
        {
            if (DisposeWhenDiscarding)
            {
                // if the entry is disposable, dispose it after removing it
                if (Entry is IDisposable disposable) disposable.Dispose();
                await Task.CompletedTask;
            }
        }
#endif
    }

    private static DateTime? NormalizeExpiresInstant(DateTime? expires)
    {
        if (expires == null) return null;
        DateTime e = expires.Value;
        if (e.Kind == DateTimeKind.Local) return e.ToUniversalTime();
        if (e.Kind == DateTimeKind.Unspecified) return DateTime.SpecifyKind(e, DateTimeKind.Utc);
        return e;
    }

    private static bool IsExpired(CacheEntry entry, DateTime nowUtc) => entry.Expiration < nowUtc;

    private static DateTime? ComputeActualExpiration(TimeSpan? maxCacheDuration, DateTime? expiration, DateTime utcNow)
    {
        if (maxCacheDuration < TimeSpan.FromTicks(0)) return null;
        DateTime? actualExpiration = null;
        if (maxCacheDuration != null) actualExpiration = utcNow.Add(maxCacheDuration.Value);
        if (expiration != null)
        {
            DateTime exp = expiration.Value;
            if (exp.Kind == DateTimeKind.Local) exp = exp.ToUniversalTime();
            else if (exp.Kind == DateTimeKind.Unspecified) exp = DateTime.SpecifyKind(exp, DateTimeKind.Utc);
            if (actualExpiration == null || exp < actualExpiration) actualExpiration = exp;
        }
        return actualExpiration;
    }

    private static DateTime ComputeOptimisticRetryDeadlineUtc(TimeSpan? operationTimeout)
    {
        DateTime utcNow = AmbientClock.UtcNow;
        DateTime deadline = utcNow.Add(MaxOptimisticRetryDuration);
        if (operationTimeout is TimeSpan to)
        {
            DateTime byCallerTimeout = utcNow.Add(to);
            if (byCallerTimeout < deadline)
            {
                deadline = byCallerTimeout;
            }
        }

        return deadline;
    }

    private static void ThrowIfOptimisticRetryDeadlineExceeded(DateTime deadlineUtc)
    {
        if (AmbientClock.UtcNow >= deadlineUtc)
            throw new InvalidOperationException(OptimisticRetryBudgetExceededMessage);
    }

    /// <summary>
    /// If the token is already canceled only because the optimistic-retry window elapsed (ambient clock),
    /// throw <see cref="InvalidOperationException"/> instead of <see cref="OperationCanceledException"/> so callers
    /// do not treat policy timeout as cooperative cancellation.
    /// </summary>
    private static void ThrowIfCancellationUnlessRetryDeadlineExceeded(DateTime optimisticRetryDeadlineUtc, CancellationToken effectiveCancel)
    {
        if (!effectiveCancel.IsCancellationRequested) return;
        ThrowIfOptimisticRetryDeadlineExceeded(optimisticRetryDeadlineUtc);
        effectiveCancel.ThrowIfCancellationRequested();
    }

    private void EnqueueExpiration(string storageKey, DateTime? actualExpiration)
    {
        if (actualExpiration == null)
        {
            _untimedQueue.Enqueue(storageKey);
        }
        else
        {
            _timedQueue.Enqueue(new TimedQueueEntry { Key = storageKey, Expiration = actualExpiration.Value });
        }
    }

    private static async ValueTask DisposeDiscardedValue(object value)
    {
#if NET5_0_OR_GREATER
        if (value is IAsyncDisposable asyncDisposable) await asyncDisposable.DisposeAsync();
        else if (value is IDisposable disposable) disposable.Dispose();
#else
        if (value is IDisposable disposable) disposable.Dispose();
        await Task.CompletedTask;
#endif
    }

    public async ValueTask<T> GetOrAdd<T>(string itemKey, Func<ValueTask<(T Item, DateTime? Expires)>> create, TimeSpan? refresh = null, TimeSpan? timeout = null, CancellationToken cancel = default) where T : class
    {
        using TimeoutCancellationRegistration timeoutRegistration = TimeoutCancellationRegistration.Create(timeout, cancel);
        CancellationToken effectiveCancel = timeoutRegistration.Token;
        string storageKey = GetUnversionedStorageKey(itemKey);
        DateTime optimisticRetryDeadlineUtc = ComputeOptimisticRetryDeadlineUtc(timeout);
        while (AmbientClock.UtcNow < optimisticRetryDeadlineUtc)
        {
            ThrowIfOptimisticRetryDeadlineExceeded(optimisticRetryDeadlineUtc);
            ThrowIfCancellationUnlessRetryDeadlineExceeded(optimisticRetryDeadlineUtc, effectiveCancel);
            DateTime now = AmbientClock.UtcNow;
            if (_cache.TryGetValue(storageKey, out CacheEntry? entry))
            {
                if (!IsExpired(entry, now))
                {
                    if (refresh != null)
                    {
                        DateTime newExpiration = now.Add(refresh.Value);
                        entry.Expiration = newExpiration;
                        _timedQueue.Enqueue(new TimedQueueEntry { Key = storageKey, Expiration = newExpiration });
                    }
                    await EjectIfNeeded();
                    return (T)entry.Entry;
                }
                await EjectEntry(entry);
                ThrowIfOptimisticRetryDeadlineExceeded(optimisticRetryDeadlineUtc);
            }
            else
            {
                await EjectIfNeeded();
                ThrowIfOptimisticRetryDeadlineExceeded(optimisticRetryDeadlineUtc);
            }

            (T created, DateTime? expires) = await create();
            ThrowIfOptimisticRetryDeadlineExceeded(optimisticRetryDeadlineUtc);
            DateTime? actualExpiration = NormalizeExpiresInstant(expires);
            DateTime nowAfterCreate = AmbientClock.UtcNow;
            if (actualExpiration < nowAfterCreate)
            {
                await DisposeDiscardedValue(created);
                ThrowIfOptimisticRetryDeadlineExceeded(optimisticRetryDeadlineUtc);
                ThrowIfCancellationUnlessRetryDeadlineExceeded(optimisticRetryDeadlineUtc, effectiveCancel);
                continue;
            }

            CacheEntry newEntry = new(storageKey, actualExpiration, created, ShouldDisposeWhenDiscarding(created));
            if (_cache.TryAdd(storageKey, newEntry))
            {
                EnqueueExpiration(storageKey, actualExpiration);
                await EjectIfNeeded();
                return created;
            }

            await DisposeDiscardedValue(created);
        }

        throw new InvalidOperationException(OptimisticRetryBudgetExceededMessage);
    }

    public async ValueTask<T> AddOrUpdate<T>(string itemKey, Func<ValueTask<(T Item, DateTime? Expires)>> create, Func<T, ValueTask<(T UpdatedItem, DateTime? Expires)>> update, TimeSpan? timeout = null, CancellationToken cancel = default) where T : class
    {
        using TimeoutCancellationRegistration timeoutRegistration = TimeoutCancellationRegistration.Create(timeout, cancel);
        CancellationToken effectiveCancel = timeoutRegistration.Token;
        string storageKey = GetUnversionedStorageKey(itemKey);
        DateTime optimisticRetryDeadlineUtc = ComputeOptimisticRetryDeadlineUtc(timeout);
        while (AmbientClock.UtcNow < optimisticRetryDeadlineUtc)
        {
            ThrowIfOptimisticRetryDeadlineExceeded(optimisticRetryDeadlineUtc);
            ThrowIfCancellationUnlessRetryDeadlineExceeded(optimisticRetryDeadlineUtc, effectiveCancel);
            DateTime now = AmbientClock.UtcNow;
            if (_cache.TryGetValue(storageKey, out CacheEntry? existing))
            {
                if (IsExpired(existing, now))
                {
                    await EjectEntry(existing);
                    ThrowIfOptimisticRetryDeadlineExceeded(optimisticRetryDeadlineUtc);
                }
                else
                {
                    T current = (T)existing.Entry;
                    (T updatedItem, DateTime? expires) = await update(current);
                    ThrowIfOptimisticRetryDeadlineExceeded(optimisticRetryDeadlineUtc);
                    DateTime? actualExpiration = NormalizeExpiresInstant(expires);
                    DateTime nowAfterUpdate = AmbientClock.UtcNow;
                    if (actualExpiration < nowAfterUpdate)
                    {
                        await DisposeDiscardedValue(updatedItem);
                        await EjectEntry(existing);
                        ThrowIfOptimisticRetryDeadlineExceeded(optimisticRetryDeadlineUtc);
                        ThrowIfCancellationUnlessRetryDeadlineExceeded(optimisticRetryDeadlineUtc, effectiveCancel);
                        continue;
                    }

                    CacheEntry replacement = new(storageKey, actualExpiration, updatedItem, ShouldDisposeWhenDiscarding(updatedItem));
                    if (_cache.TryUpdate(storageKey, replacement, existing))
                    {
                        EnqueueExpiration(storageKey, actualExpiration);
                        await EjectIfNeeded();
                        return updatedItem;
                    }
                    await DisposeDiscardedValue(updatedItem);
                    continue;
                }
            }

            (T created, DateTime? createExpires) = await create();
            ThrowIfOptimisticRetryDeadlineExceeded(optimisticRetryDeadlineUtc);
            DateTime? createActualExpiration = NormalizeExpiresInstant(createExpires);
            DateTime nowAfterCreate = AmbientClock.UtcNow;
            if (createActualExpiration < nowAfterCreate)
            {
                await DisposeDiscardedValue(created);
                ThrowIfOptimisticRetryDeadlineExceeded(optimisticRetryDeadlineUtc);
                ThrowIfCancellationUnlessRetryDeadlineExceeded(optimisticRetryDeadlineUtc, effectiveCancel);
                continue;
            }

            CacheEntry newEntry = new(storageKey, createActualExpiration, created, ShouldDisposeWhenDiscarding(created));
            if (_cache.TryAdd(storageKey, newEntry))
            {
                EnqueueExpiration(storageKey, createActualExpiration);
                await EjectIfNeeded();
                return created;
            }

            await DisposeDiscardedValue(created);
        }

        throw new InvalidOperationException(OptimisticRetryBudgetExceededMessage);
    }

    public async ValueTask Remove<T>(string itemKey, CancellationToken cancel = default)
    {
        if (_cache.TryRemove(GetUnversionedStorageKey(itemKey), out CacheEntry? disposeEntry))
        {
            await disposeEntry!.Dispose();
        }
    }

    public async ValueTask<(T? Value, long Version)> VersionedGet<T>(string itemKey, long minVersion = -1, TimeSpan? refresh = null, TimeSpan? timeout = null, CancellationToken cancel = default) where T : class
    {
        string storageKey = GetVersionedStorageKey(itemKey);
        using TimeoutCancellationRegistration timeoutRegistration = TimeoutCancellationRegistration.Create(timeout, cancel);
        CancellationToken effectiveCancel = timeoutRegistration.Token;
        if (!_cache.TryGetValue(storageKey, out CacheEntry? entry))
        {
            return (default, 0);
        }

        effectiveCancel.ThrowIfCancellationRequested();
        DateTime now = AmbientClock.UtcNow;
        if (IsExpired(entry, now))
        {
            await EjectEntry(entry);
            return (default, 0);
        }

        if (entry.MonotonicRevision < minVersion)
        {
            return (default, entry.MonotonicRevision);
        }

        if (refresh != null)
        {
            DateTime newExpiration = now.Add(refresh.Value);
            entry.Expiration = newExpiration;
            _timedQueue.Enqueue(new TimedQueueEntry { Key = storageKey, Expiration = newExpiration });
        }
        await EjectIfNeeded();
        return ((T)entry.Entry, entry.MonotonicRevision);
    }

    public async ValueTask<long> VersionedPut<T>(string itemKey, T value, TimeSpan? maxCacheDuration = null, DateTime? expiration = null, TimeSpan? timeout = null, CancellationToken cancel = default) where T : class
    {
        if (maxCacheDuration < TimeSpan.FromTicks(0))
        {
            if (value is IDisposable d) d.Dispose();
            return 0;
        }

        string storageKey = GetVersionedStorageKey(itemKey);
        using TimeoutCancellationRegistration timeoutRegistration = TimeoutCancellationRegistration.Create(timeout, cancel);
        CancellationToken effectiveCancel = timeoutRegistration.Token;
        DateTime utcNow = AmbientClock.UtcNow;
        DateTime? actualExpiration = ComputeActualExpiration(maxCacheDuration, expiration, utcNow);
        if (actualExpiration < utcNow)
        {
            if (value is IDisposable d) d.Dispose();
            return 0;
        }

        effectiveCancel.ThrowIfCancellationRequested();

        VersionCounter vc = _versionCounters.GetOrAdd(itemKey, _ => new VersionCounter());
        long revision = Interlocked.Increment(ref vc.Last);
        CacheEntry newEntry = new(storageKey, actualExpiration, value, ShouldDisposeWhenDiscarding(value!), revision);
        _cache.AddOrUpdate(storageKey, newEntry, (_, old) =>
        {
            old.Dispose().AsTask().GetAwaiter().GetResult();
            return newEntry;
        });
        EnqueueExpiration(storageKey, actualExpiration);
        await EjectIfNeeded();
        return revision;
    }

    private async ValueTask EjectIfNeeded()
    {
        int callFrequencyToEject = _callFrequencyToEject.Value;
        int countToEject = _countToEject.Value;
        // time to eject?
        while ((Interlocked.Increment(ref _expireCount) % callFrequencyToEject) == 0 || (_untimedQueue.Count + _timedQueue.Count) > countToEject)
        {
            await EjectOneTimed();
            await EjectOneUntimed();
        }
    }

    private async ValueTask EjectOneTimed()
    {
        // have we hit the minimum number of items?
        if (_timedQueue.Count <= _minCacheEntries.Value) return;
        // removing at least one timed item (as well as any expired items we come across)
        bool unexpiredItemEjected = false;
        while (_timedQueue.TryDequeue(out TimedQueueEntry qEntry))
        {
            // Eject only when the cache still has this key with the same expiration as when the row was enqueued (otherwise the row is stale after a refresh, or orphaned after removal).
            if (_cache.TryGetValue(qEntry.Key, out CacheEntry? entry) && qEntry.Expiration == entry.Expiration)
            {
                // remove it from the cache, even though it may not have expired yet because it's time to eject something
                await EjectEntry(entry);
                // fall through and check to wee if the next item is already expired
                unexpiredItemEjected = true;
            }
            // stale queue row or missing cache entry: if we have already ejected an unexpired item, check for another expired item; otherwise dequeue the next row immediately
            else if (!unexpiredItemEjected)
            {
                continue;
            }
            // peek at the next entry
            if (_timedQueue.TryPeek(out qEntry))
            {
                // has this entry expired? continue looping so that we remove this one too, even though we didn't *have* to
                if (qEntry.Expiration < AmbientClock.UtcNow) continue;
                // else the entry hasn't expired and we either removed an entry above or skipped this code, so we can just fall through and exit the loop
            }
            // if we get here, there is no reason to look at another timed entry
            break;
        }
    }

    private async ValueTask EjectOneUntimed()
    {
        // have we hit the minimum number of items?
        if (_untimedQueue.Count <= _minCacheEntries.Value) return;
        // remove one untimed entry
        while (_untimedQueue.TryDequeue(out string? key))
        {
            // can we find this item in the cache?
            if (_cache.TryGetValue(key, out CacheEntry? entry))
            {
                // is the expiration still the same (ie. untimed)?
                if (entry.Expiration == null)
                {
                    // remove it from the cache
                    await EjectEntry(entry);
                    // fall through and stop looping
                }
                else // else the item was refreshed, so we should ignore this entry and go around again to remove another entry
                {
                    continue;
                }
            }
            // else we couldn't find the entry in the cache, so just move to the next entry
            else
            {
                continue;
            }
            // if we get here, there is no reason to look at another untimed entry
            break;
        }
    }

    private async ValueTask EjectEntry(CacheEntry entry)
    {
        // race to remove the item from the cache--did we win the race?
        if (_cache.TryRemove(entry.Key, out CacheEntry? disposeEntry))
        {
            await disposeEntry.Dispose();
        }
    }

    public async ValueTask Clear(CancellationToken cancel = default)
    {
        Interlocked.Exchange(ref _untimedQueue, new ConcurrentQueue<string>());
        Interlocked.Exchange(ref _timedQueue, new ConcurrentQueue<TimedQueueEntry>());
        _versionCounters.Clear();
        while (!_cache.IsEmpty)
        {
            foreach (CacheEntry entry in _cache.Values)
            {
                await EjectEntry(entry);
            }
        }
    }
}
