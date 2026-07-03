using AmbientServices.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices;

/// <summary>
/// A basic default implementation of <see cref="IAmbientLocalCache"/> providing a small, bounded, in-process cache.
/// </summary>
/// <remarks>
/// <pitch>The zero-configuration local cache used unless overridden: a small in-process store with bounded bookkeeping, suitable for smoothing repeated lookups within a single process.  It ejects on a call-count cadence rather than tracking memory, so it is a convenience cache, not a capacity-managed one.</pitch>
/// <pledge><see cref="IAmbientLocalCache"/></pledge>
/// <plan>
/// Entries live in a <see cref="ConcurrentDictionary{TKey,TValue}"/>, with two <see cref="ConcurrentQueue{T}"/>s of bookkeeping rows — one for timed entries carrying their expiration, one for untimed keys — enqueued on every store or refresh (a refresh enqueues a superseding row; stale rows are recognized later because their recorded expiration no longer matches the entry's).
/// Every cache call increments an <see cref="Interlocked"/> counter, and ejection runs when that counter hits the configured cadence or the queues exceed the configured capacity, removing at least one timed and one untimed entry per round plus any already-expired neighbors, with hard caps on rounds and per-round queue-drain steps so a pathological queue cannot spin one async continuation unbounded.
/// Expiration comparisons use <see cref="AmbientClock"/> so tests control time deterministically.  The ejection cadence, capacity, and minimum retained count come from <see cref="AmbientSettings"/> under the <c>BasicAmbientLocalCache-</c> prefix.
/// Entries stored with dispose-on-discard are disposed on ejection, replacement, and clear; <see cref="Clear"/> swaps in fresh queues and snapshot-ejects in a bounded number of passes without blocking concurrent stores.
/// Trade-offs: constant-time operations and no background threads, in exchange for approximate size bounds (queue counts are approximate), insertion-order rather than least-recently-used ejection, and no reaction to actual memory pressure.
/// </plan>
/// </remarks>
[DefaultAmbientService]
internal class BasicAmbientLocalCache : IAmbientLocalCache
{
    /// <summary>Caps stale-queue draining per eject call so a pathological queue cannot spin unbounded in one async continuation.</summary>
    private const int MaxEjectQueueDrainSteps = 65536;

    /// <summary>Maximum snapshot-and-eject passes in <see cref="Clear"/> before giving up on reaching an empty cache.</summary>
    private const int MaxClearPasses = 8;

    private static readonly AmbientService<IAmbientSettingsSet> _Settings = Ambient.GetService<IAmbientSettingsSet>();

    private readonly IAmbientSetting<int> _callFrequencyToEject;
    private readonly IAmbientSetting<int> _countToEject;
    private readonly IAmbientSetting<int> _minCacheEntries;
    private int _expireCount;
    private ConcurrentQueue<TimedQueueEntry> _timedQueue = new();   // interlocked (make readonly when we no longer support frameworks without Clear())
    private ConcurrentQueue<string> _untimedQueue = new();          // interlocked (make readonly when we no longer support frameworks without Clear())
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();

    public BasicAmbientLocalCache()
        : this(_Settings.Local)
    {
    }

    public BasicAmbientLocalCache(IAmbientSettingsSet? settings)
    {
        _callFrequencyToEject = AmbientSettings.GetSetting<int>(settings, nameof(BasicAmbientLocalCache) + "-EjectFrequency", "The number of cache calls between cache ejections where at least one timed and one untimed entry is ejected from the cache.", s => int.Parse(s!, System.Globalization.CultureInfo.InvariantCulture), "100");
        _countToEject = AmbientSettings.GetSetting<int>(settings, nameof(BasicAmbientLocalCache) + "-MaximumItemCount", "The maximum number of both timed and untimed items to allow in the cache before ejecting items.", s => int.Parse(s!, System.Globalization.CultureInfo.InvariantCulture), "1000");
        _minCacheEntries = AmbientSettings.GetSetting<int>(settings, nameof(BasicAmbientLocalCache) + "-MinimumItemCount", "The minimum number of unexpired both timed and untimed items to keep in the cache at all times.", s => int.Parse(s!, System.Globalization.CultureInfo.InvariantCulture), "1");
    }

    private struct TimedQueueEntry
    {
        public string Key;
        public DateTime Expiration;
    }

    private class CacheEntry
    {
        public bool DisposeWhenDiscarding { get; private set; }
        public string Key;
        public DateTime? Expiration;
        public object Entry;

        public CacheEntry(string key, DateTime? expiration, object entry, bool disposeWhenDiscarding)
        {
            DisposeWhenDiscarding = disposeWhenDiscarding;
            Key = key;
            Expiration = expiration;
            Entry = entry;
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
    public async ValueTask<T?> Retrieve<T>(string key, TimeSpan? refresh = null, CancellationToken cancel = default) where T : class
    {
        CacheEntry? entry;
        if (_cache.TryGetValue(key, out entry))
        {
            DateTime now = AmbientClock.UtcNow;
            // refresh expiration?
            if (refresh != null)
            {
                // update the expiration time in the cache entry and add a NEW timed queue entry (we'll ignore the other one when we dequeue it)
                DateTime newExpiration = now.Add(refresh.Value);
                entry.Expiration = newExpiration;
                _timedQueue.Enqueue(new TimedQueueEntry { Key = key, Expiration = newExpiration });
            }
            await EjectIfNeeded();
            // no expiration or NOT expired? return the item now
            if (!(entry.Expiration < now))
            {
                // dispose-on-discard items are handed off to a single consumer, so remove (without disposing--ownership transfers to the caller) on retrieval; the KeyValuePair overload avoids removing an entry a concurrent Store just replaced
                if (entry.DisposeWhenDiscarding) ((ICollection<KeyValuePair<string, CacheEntry>>)_cache).Remove(new KeyValuePair<string, CacheEntry>(key, entry));
                return entry.Entry as T;
            }
            // else this item is expired so remove it from the cache
            await EjectEntry(entry, cancel);
        }
        else
        {
            await EjectIfNeeded();
        }
        return null;
    }

    public async ValueTask Store<T>(string itemKey, T item, bool disposeWhenDiscarding, TimeSpan? maxCacheDuration = null, DateTime? expiration = null, CancellationToken cancel = default) where T : class
    {
        // does this entry *not* expire in the past?
        if (!(maxCacheDuration < TimeSpan.FromTicks(0)))
        {
            DateTime? actualExpiration = null;
            DateTime now = AmbientClock.UtcNow;
            if (maxCacheDuration != null) actualExpiration = now.Add(maxCacheDuration.Value);
            if (expiration != null && expiration.Value.Kind == DateTimeKind.Local) expiration = expiration.Value.ToUniversalTime();
            if (expiration < actualExpiration) actualExpiration = expiration;
            CacheEntry entry = new(itemKey, actualExpiration, item, disposeWhenDiscarding);
            // ConcurrentDictionary has no async update delegate, and its update delegate may run more than
            // once under contention — so a disposing side effect there can double-dispose. Do the compare-and-swap
            // ourselves: this displaces exactly one entry, which we then dispose once, awaited, outside the dictionary.
            CacheEntry? displaced = null;
            while (true)
            {
                if (_cache.TryGetValue(itemKey, out CacheEntry? old))
                {
                    if (_cache.TryUpdate(itemKey, entry, old))
                    {
                        displaced = old;
                        break;
                    }
                }
                else if (_cache.TryAdd(itemKey, entry))
                {
                    break;
                }
                // another writer changed the slot first; re-read and retry
            }
            if (displaced != null) await displaced.Dispose();
            if (actualExpiration == null)
            {
                _untimedQueue.Enqueue(itemKey);
            }
            else
            {
                _timedQueue.Enqueue(new TimedQueueEntry { Key = itemKey, Expiration = actualExpiration.Value });
            }
        }
        else
        {
            // else this item is expired so dispose of it as if we had put it into the cache and then it expired
            if (item is IDisposable disposable) disposable.Dispose();
        }
        await EjectIfNeeded();
    }

    private async ValueTask EjectIfNeeded()
    {
        int callFrequencyToEject = _callFrequencyToEject.Value;
        if (callFrequencyToEject <= 0)
            callFrequencyToEject = 1;

        int countToEject = _countToEject.Value;
        int opSerial = Interlocked.Increment(ref _expireCount);
        bool onCadence = (opSerial % callFrequencyToEject) == 0;
        int queueSum = _untimedQueue.Count + _timedQueue.Count;
        bool overCapacity = queueSum > countToEject;
        if (!onCadence && !overCapacity)
            return;

        int maxRounds = Math.Max(32, Math.Min(131072, 4 + 2 * Math.Max(queueSum, countToEject + 1)));
        for (int round = 0; round < maxRounds; round++)
        {
            await EjectOneTimed();
            await EjectOneUntimed();

            queueSum = _untimedQueue.Count + _timedQueue.Count;
            if (queueSum <= countToEject)
                break;
        }
    }

    private async ValueTask EjectOneTimed(CancellationToken cancel = default)
    {
        // have we hit the minimum number of items?
        if (_timedQueue.Count <= _minCacheEntries.Value) return;
        // removing at least one timed item (as well as any expired items we come across)
        bool unexpiredItemEjected = false;
        int steps = 0;
        while (steps++ < MaxEjectQueueDrainSteps && _timedQueue.TryDequeue(out TimedQueueEntry qEntry))
        {
            // can we find this item in the cache?
            CacheEntry? entry;
            if (_cache.TryGetValue(qEntry.Key, out entry))
            {
                // is the expiration still the same?
                if (qEntry.Expiration == entry.Expiration)
                {
                    // remove it from the cache, even though it may not have expired yet because it's time to eject something
                    await EjectEntry(entry, cancel);
                    // fall through and check to see if the next item is already expired
                    unexpiredItemEjected = true;
                }
                // the item was refreshed, so we should ignore this entry-- if we have already ejected an unexpired item, we need to check for another expired item, otherwise we still haven't ejected anything, so go around again immediately
                else if (!unexpiredItemEjected)
                {
                    continue;
                }
            }
            // else we couldn't find the entry in the cache, so just move to the next entry (unless we've already ejected an unexpired item, in which case we should just check for another expired item and bail if there's not)
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

    private async ValueTask EjectOneUntimed(CancellationToken cancel = default)
    {
        // have we hit the minimum number of items?
        if (_untimedQueue.Count <= _minCacheEntries.Value) return;
        // remove one untimed entry
        int steps = 0;
        while (steps++ < MaxEjectQueueDrainSteps && _untimedQueue.TryDequeue(out string? key))
        {
            // can we find this item in the cache?
            CacheEntry? entry;
            if (_cache.TryGetValue(key, out entry))
            {
                // is the expiration still the same (ie. untimed)?
                if (entry.Expiration == null)
                {
                    // remove it from the cache
                    await EjectEntry(entry, cancel);
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

    public async ValueTask<T?> Remove<T>(string itemKey, CancellationToken cancel = default) where T : class
    {
        CacheEntry? disposeEntry;
        if (_cache.TryRemove(itemKey, out disposeEntry))
        {
            // hand the item off to the caller if it's the requested type, transferring dispose responsibility
            if (disposeEntry.Entry is T) return (T?)disposeEntry.Entry;
            // otherwise the caller can't take ownership, so the cache disposes the discarded entry (no-op unless dispose-on-discard)
            await disposeEntry.Dispose();
        }
        return default;
    }

    private async ValueTask EjectEntry(CacheEntry entry, CancellationToken cancel = default)
    {
        CacheEntry? disposeEntry;
        // race to remove the item from the cache--did we win the race?
        if (_cache.TryRemove(entry.Key, out disposeEntry))
        {
            await disposeEntry!.Dispose();  // if it was successfully removed, it can't be null
        }
    }

    public async ValueTask Clear(CancellationToken cancel = default)
    {
        Interlocked.Exchange(ref _untimedQueue, new ConcurrentQueue<string>());
        Interlocked.Exchange(ref _timedQueue, new ConcurrentQueue<TimedQueueEntry>());

        for (int pass = 0; pass < MaxClearPasses; pass++)
        {
            KeyValuePair<string, CacheEntry>[] snapshot = _cache.ToArray();
            if (snapshot.Length == 0)
                break;

            foreach (KeyValuePair<string, CacheEntry> kv in snapshot)
            {
                cancel.ThrowIfCancellationRequested();
                await EjectEntry(kv.Value, cancel);
            }

            if (_cache.IsEmpty)
                break;
        }
    }
}
