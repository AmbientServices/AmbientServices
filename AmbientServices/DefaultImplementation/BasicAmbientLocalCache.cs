using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices
{
    [DefaultAmbientService]
    internal class BasicAmbientLocalCache : IAmbientLocalCache
    {
        private static readonly AmbientService<IAmbientSettingsSet> _Settings = Ambient.GetService<IAmbientSettingsSet>();

        private readonly IAmbientSetting<int> _callFrequencyToEject;
        private readonly IAmbientSetting<int> _countToEject;
        private readonly IAmbientSetting<int> _minCacheEntries;
        private int _expireCount;
        private ConcurrentQueue<TimedQueueEntry> _timedQueue = new ConcurrentQueue<TimedQueueEntry>();
        private ConcurrentQueue<string> _untimedQueue = new ConcurrentQueue<string>();
        private ConcurrentDictionary<string, CacheEntry> _cache = new ConcurrentDictionary<string, CacheEntry>();

        public BasicAmbientLocalCache()
            : this(_Settings.Local)
        {
        }

        public BasicAmbientLocalCache(IAmbientSettingsSet? settings)
        {
            _callFrequencyToEject = AmbientSettings.GetSetting<int>(settings, nameof(BasicAmbientLocalCache) + "-EjectFrequency", "The number of cache calls between cache ejections where at least one timed and one untimed entry is ejected from the cache.", s => Int32.Parse(s!, System.Globalization.CultureInfo.InvariantCulture), "100");
            _countToEject = AmbientSettings.GetSetting<int>(settings, nameof(BasicAmbientLocalCache) + "-MaximumItemCount", "The maximum number of both timed and untimed items to allow in the cache before ejecting items.", s => Int32.Parse(s!, System.Globalization.CultureInfo.InvariantCulture), "1000");
            _minCacheEntries = AmbientSettings.GetSetting<int>(settings, nameof(BasicAmbientLocalCache) + "-MinimumItemCount", "The minimum number of unexpired both timed and untimed items to keep in the cache at all times.", s => Int32.Parse(s!, System.Globalization.CultureInfo.InvariantCulture), "1");
        }

        struct TimedQueueEntry
        {
            public string Key;
            public DateTime Expiration;
        }
        class CacheEntry
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
                    if (Entry is IAsyncDisposable asyncDisposable) await asyncDisposable.DisposeAsync().ConfigureAwait(false);
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
                    await Task.CompletedTask.ConfigureAwait(false);
                }
            }
#endif
        }
        public async ValueTask<T?> Retrieve<T>(string key, TimeSpan? refresh = null, CancellationToken cancel = default(CancellationToken)) where T : class
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
                await EjectIfNeeded().ConfigureAwait(false);
                // no expiration or NOT expired? return the item now
                if (!(entry.Expiration < now))
                {
                    return entry.Entry as T;
                }
                // else this item is expired so remove it from the cache
                await EjectEntry(entry, cancel).ConfigureAwait(false);
            }
            else
            {
                await EjectIfNeeded().ConfigureAwait(false);
            }
            return null;
        }

        public async ValueTask Store<T>(string itemKey, T item, bool disposeWhenDiscarding, TimeSpan? maxCacheDuration = null, DateTime? expiration = null, CancellationToken cancel = default(CancellationToken)) where T : class
        {
            // does this entry *not* expire in the past?
            if (!(maxCacheDuration < TimeSpan.FromTicks(0)))
            {
                DateTime? actualExpiration = null;
                DateTime now = AmbientClock.UtcNow;
                if (maxCacheDuration != null) actualExpiration = now.Add(maxCacheDuration.Value);
                if (expiration != null && expiration.Value.Kind == DateTimeKind.Local) expiration = expiration.Value.ToUniversalTime();
                if (expiration < actualExpiration) actualExpiration = expiration;
                CacheEntry entry = new CacheEntry(itemKey, actualExpiration, item, disposeWhenDiscarding);
                _cache.AddOrUpdate(itemKey, entry, (k, v) => { Async.SynchronizeValue(async () => { CacheEntry c = v; if (c != null) await c.Dispose().ConfigureAwait(true); }); return entry; });
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
            await EjectIfNeeded().ConfigureAwait(false);
        }
        async ValueTask EjectIfNeeded()
        {
            int callFrequencyToEject = _callFrequencyToEject.Value;
            int countToEject = _countToEject.Value;
            // time to eject?
            while ((System.Threading.Interlocked.Increment(ref _expireCount) % callFrequencyToEject) == 0 || (_untimedQueue.Count + _timedQueue.Count) > countToEject)
            {
                await EjectOneTimed().ConfigureAwait(false);
                await EjectOneUntimed().ConfigureAwait(false);
            }
        }

        private async ValueTask EjectOneTimed(CancellationToken cancel = default)
        {
            // have we hit the minimum number of items?
            if (_timedQueue.Count <= _minCacheEntries.Value) return;
            // removing at least one timed item (as well as any expired items we come across)
            bool unexpiredItemEjected = false;
            TimedQueueEntry qEntry;
            while (_timedQueue.TryDequeue(out qEntry))
            {
                // can we find this item in the cache?
                CacheEntry? entry;
                if (_cache.TryGetValue(qEntry.Key, out entry))
                {
                    // is the expiration still the same?
                    if (qEntry.Expiration == entry.Expiration)
                    {
                        // remove it from the cache, even though it may not have expired yet because it's time to eject something
                        await EjectEntry(entry, cancel).ConfigureAwait(false);
                        // fall through and check to wee if the next item is already expired
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
            string? key;
            while (_untimedQueue.TryDequeue(out key))
            {
                // can we find this item in the cache?
                CacheEntry? entry;
                if (_cache.TryGetValue(key, out entry))
                {
                    // is the expiration still the same (ie. untimed)?
                    if (entry.Expiration == null)
                    {
                        // remove it from the cache
                        await EjectEntry(entry, cancel).ConfigureAwait(false);
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

        public ValueTask<T?> Remove<T>(string itemKey, CancellationToken cancel = default(CancellationToken))
        {
            CacheEntry? disposeEntry;
            if (_cache.TryRemove(itemKey, out disposeEntry))
            {
                if (disposeEntry.Entry is T) return TaskExtensions.ValueTaskFromResult((T?)disposeEntry.Entry);
            }
            return TaskExtensions.ValueTaskFromResult((T?)default);
        }

        private async ValueTask EjectEntry(CacheEntry entry, CancellationToken cancel = default(CancellationToken))
        {
            CacheEntry? disposeEntry;
            // race to remove the item from the cache--did we win the race?
            if (_cache.TryRemove(entry.Key, out disposeEntry))
            {
                await disposeEntry!.Dispose().ConfigureAwait(false);  // if it was successfully removed, it can't be null
            }
        }

        public async ValueTask Clear(CancellationToken cancel = default(CancellationToken))
        {
            _untimedQueue = new ConcurrentQueue<string>();
            _timedQueue = new ConcurrentQueue<TimedQueueEntry>();
            while (!_cache.IsEmpty)
            {
                foreach (CacheEntry entry in _cache.Values)
                {
                    await EjectEntry(entry, cancel).ConfigureAwait(false);
                }
            }
        }
    }
}
