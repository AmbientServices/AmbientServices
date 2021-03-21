using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices
{
    [DefaultAmbientService]
    internal class BasicAmbientCache : IAmbientCache
    {
        private static readonly AmbientService<IAmbientSettingsSet> _Settings = Ambient.GetService<IAmbientSettingsSet>();

        private readonly IAmbientSetting<int> _callFrequencyToEject;
        private readonly IAmbientSetting<int> _countToEject;
        private readonly IAmbientSetting<int> _minCacheEntries;
        private int _expireCount;
        private ConcurrentQueue<TimedQueueEntry> _timedQueue = new ConcurrentQueue<TimedQueueEntry>();
        private ConcurrentQueue<string> _untimedQueue = new ConcurrentQueue<string>();
        private ConcurrentDictionary<string, CacheEntry> _cache = new ConcurrentDictionary<string, CacheEntry>();

        public BasicAmbientCache()
            : this(_Settings.Local)
        {
        }

        public BasicAmbientCache(IAmbientSettingsSet? settings)
        {
            _callFrequencyToEject = AmbientSettings.GetSetting<int>(settings, nameof(BasicAmbientCache) + "-EjectFrequency", "The number of cache calls between cache ejections where at least one timed and one untimed entry is ejected from the cache.", s => Int32.Parse(s!, System.Globalization.CultureInfo.InvariantCulture), "100");
            _countToEject = AmbientSettings.GetSetting<int>(settings, nameof(BasicAmbientCache) + "-MaximumItemCount", "The maximum number of both timed and untimed items to allow in the cache before ejecting items.", s => Int32.Parse(s!, System.Globalization.CultureInfo.InvariantCulture), "1000");
            _minCacheEntries = AmbientSettings.GetSetting<int>(settings, nameof(BasicAmbientCache) + "-MinimumItemCount", "The minimum number of unexpired both timed and untimed items to keep in the cache at all times.", s => Int32.Parse(s!, System.Globalization.CultureInfo.InvariantCulture), "1");
        }

        struct TimedQueueEntry
        {
            public string Key;
            public DateTime Expiration;
        }
        class CacheEntry
        {
            public string Key;
            public DateTime? Expiration;
            public object Entry;

            public CacheEntry(string key, DateTime? expiration, object entry)
            {
                Key = key;
                Expiration = expiration;
                Entry = entry;
            }
        }
        public Task<T?> Retrieve<T>(string key, TimeSpan? refresh = null, CancellationToken cancel = default(CancellationToken)) where T : class
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
                EjectIfNeeded();
                // no expiration or NOT expired? return the item now
                if (!(entry.Expiration < now))
                {
                    return Task.FromResult<T?>(entry.Entry as T);
                }
                // else this item is expired so remove it from the cache
                _cache.TryRemove(entry.Key, out entry);
            }
            else
            {
                EjectIfNeeded();
            }
            return Task.FromResult<T?>(null);
        }

        public Task Store<T>(bool localOnly, string itemKey, T item, TimeSpan? maxCacheDuration = null, DateTime? expiration = null, CancellationToken cancel = default(CancellationToken)) where T : class
        {
            // does this entry *not* expire in the past?
            if (!(maxCacheDuration < TimeSpan.FromTicks(0)))
            {
                DateTime? actualExpiration = null;
                DateTime now = AmbientClock.UtcNow;
                if (maxCacheDuration != null) actualExpiration = now.Add(maxCacheDuration.Value);
                if (expiration != null && expiration.Value.Kind == DateTimeKind.Local) expiration = expiration.Value.ToUniversalTime();
                if (expiration < actualExpiration) actualExpiration = expiration;
                CacheEntry entry = new CacheEntry(itemKey, actualExpiration, item);
                _cache.AddOrUpdate(itemKey, entry, (k, v) => entry);
                if (actualExpiration == null)
                {
                    _untimedQueue.Enqueue(itemKey);
                }
                else
                {
                    _timedQueue.Enqueue(new TimedQueueEntry { Key = itemKey, Expiration = actualExpiration.Value });
                }
            }
            EjectIfNeeded();
            return Task.CompletedTask;
        }
        void EjectIfNeeded()
        {
            int callFrequencyToEject = _callFrequencyToEject.Value;
            int countToEject = _countToEject.Value;
            // time to eject?
            while ((System.Threading.Interlocked.Increment(ref _expireCount) % callFrequencyToEject) == 0 || (_untimedQueue.Count + _timedQueue.Count) > countToEject)
            {
                EjectOneTimed();
                EjectOneUntimed();
            }
        }

        private void EjectOneTimed()
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
                        _cache.TryRemove(qEntry.Key, out entry);
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

        private void EjectOneUntimed()
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
                        _cache.TryRemove(key, out entry);
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

        public Task Remove<T>(bool localOnly, string itemKey, CancellationToken cancel = default(CancellationToken))
        {
            CacheEntry? entry;
            _cache.TryRemove(itemKey, out entry);
            // we don't remove the entry from the queue, but that's okay because we'll just ignore that entry when we get to it
            return Task.CompletedTask;
        }

        public Task Clear(bool localOnly = true, CancellationToken cancel = default(CancellationToken))
        {
            _untimedQueue = new ConcurrentQueue<string>();
            _timedQueue = new ConcurrentQueue<TimedQueueEntry>();
            _cache.Clear();
            return Task.CompletedTask;
        }
    }
}
