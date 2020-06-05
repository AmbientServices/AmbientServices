using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices
{
    /// <summary>
    /// An interface that abstracts a caching service.
    /// </summary>
    public interface ICache
    {
        /// <summary>
        /// Checks to see if the item with the specified key is cached and returns it if it is.
        /// </summary>
        /// <typeparam name="T">The type of the cached object.</typeparam>
        /// <param name="key">The unique key sent when the object was cached.</param>
        /// <param name="refresh">An optoinal <see cref="TimeSpan"/> indicating the length of time to extend the lifespan of the cached item.  Defaults to null, meaning not to update the expiration time.</param>
        /// <returns>The cached object, or null if it was not found in the cache.</returns>
        Task<T> TryGet<T>(string key, TimeSpan? refresh = null, CancellationToken cancel = default(CancellationToken)) where T : class;
        /// <summary>
        /// Sets the specified item into the cache.
        /// </summary>
        /// <typeparam name="T">The type of the item to be cached.</typeparam>
        /// <param name="localOnly">Whether or not this item should only be stored in a local cache (as opposed to a shared cache).</param>
        /// <param name="itemKey">A string that uniquely identifies the item being cached.</param>
        /// <param name="item">The item to be cached.</param>
        /// <param name="expiration">An optional <see cref="DateTime"/> indicating a fixed time for when the item should expire from the cache.</param>
        /// <param name="maxCacheDuration">An optional <see cref="TimeSpan"/> indicating the maximum amount of time to keep the item in the cache.</param>
        /// <remarks>
        /// If both <paramref name="expiration"/> and <paramref name="maxCacheDuration"/> are both set, the earlier expiration will be used.
        /// </remarks>
        Task Set<T>(bool localOnly, string itemKey, T item, DateTime? expiration = null, TimeSpan? maxCacheDuration = null, CancellationToken cancel = default(CancellationToken)) where T : class;
        /// <summary>
        /// Removes the specified item from the cache.
        /// </summary>
        /// <typeparam name="T">The type of the item to be cached.</typeparam>
        /// <param name="localOnly">Whether or not this item should only be stored in a local cache (as opposed to a shared cache).</param>
        /// <param name="itemKey">A string that uniquely identifies the item being cached.</param>
        Task Remove<T>(bool localOnly, string itemKey, CancellationToken cancel = default(CancellationToken));
        /// <summary>
        /// Flushes the cache.
        /// </summary>
        /// <param name="localOnly">Whether or not to clear only the local cache.</param>
        Task Clear(bool localOnly = true, CancellationToken cancel = default(CancellationToken));
    }
    [DefaultImplementation]
    class DefaultCache : ICache
    {
        const int CallFrequencyToEject = 100;
        const int CountToEject = 10000;
        private int _expireCount = 0;
        private ConcurrentQueue<TimedQueueEntry> _timedQueue = new ConcurrentQueue<TimedQueueEntry>();
        private ConcurrentQueue<string> _untimedQueue = new ConcurrentQueue<string>();
        private ConcurrentDictionary<string, CacheEntry> _cache = new ConcurrentDictionary<string, CacheEntry>();

        public DefaultCache()
        {
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
        }
        public Task<T> TryGet<T>(string key, TimeSpan? refresh = null, CancellationToken cancel = default(CancellationToken)) where T : class
        {
            CacheEntry entry;
            if (_cache.TryGetValue(key, out entry))
            {
                // refresh expiration?
                if (refresh != null)
                {
                    // update the expiration time in the cache entry and add a NEW timed queue entry (we'll ignore the other one when we dequeue it)
                    DateTime newExpiration = DateTime.UtcNow.Add(refresh.Value);
                    entry.Expiration = newExpiration;
                    _timedQueue.Enqueue(new TimedQueueEntry { Key = key, Expiration = newExpiration });
                }
                EjectIfNeeded();
                // no expiration or NOT expired?
                if (entry.Expiration == null || entry.Expiration >= DateTime.UtcNow) return Task.FromResult<T>(entry.Entry as T);
            }
            else
            {
                EjectIfNeeded();
            }
            return Task.FromResult<T>(null);
        }

        public Task Set<T>(bool localOnly, string itemKey, T item, DateTime? expiration = null, TimeSpan? maxCacheDuration = null, CancellationToken cancel = default(CancellationToken)) where T : class
        {
            DateTime? actualExpiration = null;
            if (maxCacheDuration != null) actualExpiration = DateTime.UtcNow.Add(maxCacheDuration.Value);
            if (expiration != null && expiration.Value.Kind == DateTimeKind.Local) expiration = expiration.Value.ToUniversalTime();
            if (expiration < actualExpiration) actualExpiration = expiration;
            CacheEntry entry = new CacheEntry { Key = itemKey, Expiration = actualExpiration, Entry = item };
            _cache.AddOrUpdate(itemKey, entry, (k, v) => entry);
            if (actualExpiration == null)
            {
                _untimedQueue.Enqueue(itemKey);
            }
            else
            {
                _timedQueue.Enqueue(new TimedQueueEntry { Key = itemKey, Expiration = actualExpiration.Value });
            }
            EjectIfNeeded();
            return Task.CompletedTask;
        }
        void EjectIfNeeded()
        { 
            // time to eject?
            while ((System.Threading.Interlocked.Increment(ref _expireCount) % CallFrequencyToEject) == 0 || (_untimedQueue.Count + _timedQueue.Count) > CountToEject)
            {
                // removing at least one timed item (and all expired items)
                TimedQueueEntry qEntry;
                while (_timedQueue.TryDequeue(out qEntry))
                {
                    // can we find this item in the cache?
                    CacheEntry entry;
                    if (_cache.TryGetValue(qEntry.Key, out entry))
                    {
                        // is the expiration still the same?
                        if (qEntry.Expiration == entry.Expiration)
                        {
                            // remove it from the cache
                            _cache.TryRemove(qEntry.Key, out entry);
                        }
                        else // else the item was refreshed, so we should ignore this entry and go around again
                        {
                            continue;
                        }
                    }
                    // else we couldn't find the entry in the cache, so just move to the next entry
                    else
                    {
                        continue;
                    }
                    // peek at the next entry
                    if (_timedQueue.TryPeek(out qEntry))
                    {
                        // has this entry expired?
                        if (entry.Expiration < DateTime.UtcNow) continue;
                        // else the entry hasn't expired and we either removed an entry above or skipped this code, so we can just fall through and exit the loop
                    }
                    // if we get here, there is no reason to look at another timed entry
                    break;
                }
                // remove one untimed entry
                string key;
                while (_untimedQueue.TryDequeue(out key))
                {
                    // can we find this item in the cache?
                    CacheEntry entry;
                    if (_cache.TryGetValue(key, out entry))
                    {
                        // is the expiration still the same (ie. untimed)?
                        if (entry.Expiration == null)
                        {
                            // remove it from the cache
                            _cache.TryRemove(key, out entry);
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
        }

        public Task Remove<T>(bool localOnly, string itemKey, CancellationToken cancel = default(CancellationToken))
        {
            CacheEntry entry;
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
