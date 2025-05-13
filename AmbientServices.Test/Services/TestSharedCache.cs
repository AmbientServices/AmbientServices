using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices.Test;

/// <summary>
/// A class that holds tests for <see cref="IAmbientSharedCache"/>.
/// </summary>
[TestClass]
public class TestSharedCache
{
    private static readonly Dictionary<string, string> TestCacheSettingsDictionary = new() { { nameof(BasicAmbientCache) + "-EjectFrequency", "10" }, { nameof(BasicAmbientCache) + "-MaximumItemCount", "20" }, { nameof(BasicAmbientCache) + "-MinimumItemCount", "1" } };
    private static readonly Dictionary<string, string> AllowEmptyCacheSettingsDictionary = new() { { nameof(BasicAmbientCache) + "-EjectFrequency", "40" }, { nameof(BasicAmbientCache) + "-MaximumItemCount", "20" }, { nameof(BasicAmbientCache) + "-MinimumItemCount", "-1" } };
    /// <summary>
    /// Performs tests on <see cref="IAmbientSharedCache"/>.
    /// </summary>
    [TestMethod]
    public async Task CacheAmbient()
    {
        AmbientSettingsOverride localSettingsSet = new(TestCacheSettingsDictionary, nameof(CacheAmbient));
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(localSettingsSet))
        {
            IAmbientSharedCache localOverride = new BasicAmbientCache();
            using ScopedLocalServiceOverride<IAmbientSharedCache> localCache = new(localOverride);
            TestSharedCache ret;
            AmbientSharedCache<TestSharedCache> cache = new();
            await cache.Store("Test1", this);
            await cache.Store("Test1", this);
            ret = await cache.Retrieve<TestSharedCache>("Test1", null);
            Assert.AreEqual(this, ret);
            await cache.Remove<TestSharedCache>("Test1");
            ret = await cache.Retrieve<TestSharedCache>("Test1", null);
            Assert.IsNull(ret);
            await cache.Store("Test2", this, null, DateTime.MinValue);
            ret = await cache.Retrieve<TestSharedCache>("Test2", null);
            Assert.AreEqual(this, ret);
            await Eject(cache, 2);
            ret = await cache.Retrieve<TestSharedCache>("Test2", null);
            Assert.IsNull(ret);
            await cache.Store("Test3", this, TimeSpan.FromMinutes(-1));
            ret = await cache.Retrieve<TestSharedCache>("Test3", null);
            Assert.IsNull(ret);
            await cache.Store("Test4", this, TimeSpan.FromMinutes(10), DateTime.UtcNow.AddMinutes(11));
            ret = await cache.Retrieve<TestSharedCache>("Test4", null);
            Assert.AreEqual(this, ret);
            await cache.Store("Test5", this, TimeSpan.FromMinutes(10), DateTime.Now.AddMinutes(11));
            ret = await cache.Retrieve<TestSharedCache>("Test5", null);
            Assert.AreEqual(this, ret);
            await cache.Store("Test6", this, TimeSpan.FromMinutes(60), DateTime.UtcNow.AddMinutes(10));
            ret = await cache.Retrieve<TestSharedCache>("Test6", null);
            Assert.AreEqual(this, ret);
            ret = await cache.Retrieve<TestSharedCache>("Test6", TimeSpan.FromMinutes(10));
            Assert.AreEqual(this, ret);
            await Eject(cache, 50);
            await cache.Clear();
            ret = await cache.Retrieve<TestSharedCache>("Test6", null);
            Assert.IsNull(ret);
        }
    }
    /// <summary>
    /// Performs tests on <see cref="IAmbientSharedCache"/>.
    /// </summary>
    [TestMethod]
    public async Task CacheNone()
    {
        using ScopedLocalServiceOverride<IAmbientSharedCache> localCache = new(null);
        TestSharedCache ret;
        AmbientSharedCache<TestSharedCache> cache = new();
        await cache.Store("Test1", this);
        ret = await cache.Retrieve<TestSharedCache>("Test1");
        Assert.IsNull(ret);
        await cache.Remove<TestSharedCache>("Test1");
        ret = await cache.Retrieve<TestSharedCache>("Test1", null);
        Assert.IsNull(ret);
        await cache.Clear();
        ret = await cache.Retrieve<TestSharedCache>("Test1", null);
        Assert.IsNull(ret);
    }
    /// <summary>
    /// Performs tests on <see cref="IAmbientSharedCache"/>.
    /// </summary>
    [TestMethod]
    public async Task CacheExpiration()
    {
        IAmbientSharedCache localOverride = new BasicAmbientCache();
        using (AmbientClock.Pause())
        using (ScopedLocalServiceOverride<IAmbientSharedCache> localCache = new(localOverride))
        {
            string keyName1 = nameof(CacheExpiration) + "1";
            string keyName2 = nameof(CacheExpiration) + "2";
            string keyName3 = nameof(CacheExpiration) + "3";
            string keyName4 = nameof(CacheExpiration) + "4";
            string keyName5 = nameof(CacheExpiration) + "5";
            string keyName6 = nameof(CacheExpiration) + "6";
            string keyName7 = nameof(CacheExpiration) + "7";
            TestSharedCache ret;
            AmbientSharedCache<TestSharedCache> cache = new();
            await cache.Store(keyName1, this, TimeSpan.FromMilliseconds(50));
            await cache.Store(keyName1, this, TimeSpan.FromMilliseconds(51));
            await cache.Store(keyName2, this);
            await cache.Store(keyName2, this);
            await cache.Store(keyName3, this, TimeSpan.FromMilliseconds(-51));    // this should never get cached because the time span is negative
            await cache.Store(keyName3, this, TimeSpan.FromMilliseconds(-50));    // this should never get cached because the time span is negative
            await cache.Store(keyName4, this);
            await cache.Store(keyName4, this);
            await cache.Store(keyName5, this, TimeSpan.FromMilliseconds(50));
            await cache.Store(keyName5, this, TimeSpan.FromMilliseconds(50));
            await cache.Store(keyName6, this, TimeSpan.FromMilliseconds(1000));
            await cache.Store(keyName6, this, TimeSpan.FromMilliseconds(1000));
            await cache.Store(keyName7, this, TimeSpan.FromMilliseconds(75));
            await cache.Store(keyName7, this, TimeSpan.FromMilliseconds(1000));
            ret = await cache.Retrieve<TestSharedCache>(keyName1);
            Assert.IsNotNull(ret);
            ret = await cache.Retrieve<TestSharedCache>(keyName2);
            Assert.IsNotNull(ret);
            ret = await cache.Retrieve<TestSharedCache>(keyName3);
            Assert.IsNull(ret);
            ret = await cache.Retrieve<TestSharedCache>(keyName4);
            Assert.IsNotNull(ret);
            ret = await cache.Retrieve<TestSharedCache>(keyName5);
            Assert.IsNotNull(ret);
            ret = await cache.Retrieve<TestSharedCache>(keyName6);
            Assert.IsNotNull(ret);
            ret = await cache.Retrieve<TestSharedCache>(keyName7);
            Assert.IsNotNull(ret);
            await Eject(cache, 1);  // this should eject 1 because it's the LRU timed and 2 because it's the LRU untimed
            ret = await cache.Retrieve<TestSharedCache>(keyName1);
            Assert.IsNull(ret);
            ret = await cache.Retrieve<TestSharedCache>(keyName2);
            Assert.IsNull(ret);
            ret = await cache.Retrieve<TestSharedCache>(keyName4);
            Assert.IsNotNull(ret);
            ret = await cache.Retrieve<TestSharedCache>(keyName5);
            Assert.IsNotNull(ret);
            ret = await cache.Retrieve<TestSharedCache>(keyName6);
            Assert.IsNotNull(ret);
            ret = await cache.Retrieve<TestSharedCache>(keyName7);
            Assert.IsNotNull(ret);
            await Eject(cache, 1);  // this should eject 5 because it's the LRU timed and 4 because it's the LRU untimed
            ret = await cache.Retrieve<TestSharedCache>(keyName4);
            Assert.IsNull(ret);
            ret = await cache.Retrieve<TestSharedCache>(keyName5);
            Assert.IsNull(ret);
            ret = await cache.Retrieve<TestSharedCache>(keyName6);
            Assert.IsNotNull(ret);
            ret = await cache.Retrieve<TestSharedCache>(keyName7);
            Assert.IsNotNull(ret);
            AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(100));
            await Eject(cache, 1);  // this should eject 6 because it's the LRU timed but not 7 because only the first entry is expired, and not untimed LRU
            ret = await cache.Retrieve<TestSharedCache>(keyName6);
            Assert.IsNull(ret);
            ret = await cache.Retrieve<TestSharedCache>(keyName7);
            Assert.IsNotNull(ret);
        }
    }
    /// <summary>
    /// Performs tests on <see cref="IAmbientSharedCache"/>.
    /// </summary>
    [TestMethod]
    public async Task CacheSkipAndEmptyEject()
    {
        AmbientSettingsOverride localSettingsSet = new(AllowEmptyCacheSettingsDictionary, nameof(CacheSkipAndEmptyEject));
        using (AmbientClock.Pause())
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(localSettingsSet))
        {
            IAmbientSharedCache localOverride = new BasicAmbientCache();
            using ScopedLocalServiceOverride<IAmbientSharedCache> localCache = new(localOverride);
            string keyName1 = nameof(CacheExpiration) + "1";
            string keyName2 = nameof(CacheExpiration) + "2";
            string keyName3 = nameof(CacheExpiration) + "3";
            //string keyName4 = nameof(CacheExpiration) + "4";
            //string keyName5 = nameof(CacheExpiration) + "5";
            //string keyName6 = nameof(CacheExpiration) + "6";
            //string keyName7 = nameof(CacheExpiration) + "7";
            TestSharedCache ret;
            AmbientSharedCache<TestSharedCache> cache = new();
            await cache.Store(keyName1, this, TimeSpan.FromMilliseconds(100));
            await cache.Store(keyName2, this, TimeSpan.FromMilliseconds(50));
            await cache.Store(keyName3, this, TimeSpan.FromMilliseconds(100));
            ret = await cache.Retrieve<TestSharedCache>(keyName1);
            Assert.IsNotNull(ret);
            ret = await cache.Retrieve<TestSharedCache>(keyName2, TimeSpan.FromMilliseconds(100));
            Assert.IsNotNull(ret);
            ret = await cache.Retrieve<TestSharedCache>(keyName3);
            Assert.IsNotNull(ret);
            AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(50));
            await Eject(cache, 1);  // this should eject 1 because it's the LRU timed, and the first timed entry for 2 because that's expired, but 2 should remain with a refreshed entry
            ret = await cache.Retrieve<TestSharedCache>(keyName1);
            Assert.IsNull(ret);
            ret = await cache.Retrieve<TestSharedCache>(keyName2, TimeSpan.FromMilliseconds(100));
            Assert.IsNotNull(ret);
            ret = await cache.Retrieve<TestSharedCache>(keyName3);
            Assert.IsNotNull(ret);
            await Eject(cache, 1);  // this should skip 2 because it's bee refershed again and eject 3 because it's the LRU timed
            ret = await cache.Retrieve<TestSharedCache>(keyName1);
            Assert.IsNull(ret);
            ret = await cache.Retrieve<TestSharedCache>(keyName2);
            Assert.IsNotNull(ret);
            ret = await cache.Retrieve<TestSharedCache>(keyName3);
            Assert.IsNull(ret);

            // change key2 to be untimed
            await cache.Store(keyName2, this);
            await Eject(cache, 1);  // this should skip over the timed entry for 2 but then eject it because it is untimed
        }
    }
    /// <summary>
    /// Performs tests on <see cref="IAmbientSharedCache"/>.
    /// </summary>
    [TestMethod]
    public async Task CacheDoubleExpiration()
    {
        IAmbientSharedCache localOverride = new BasicAmbientCache();
        using (AmbientClock.Pause())
        using (ScopedLocalServiceOverride<IAmbientSharedCache> localCache = new(localOverride))
        {
            string keyName1 = nameof(CacheDoubleExpiration) + "1";
            string keyName2 = nameof(CacheDoubleExpiration) + "2";
            string keyName3 = nameof(CacheDoubleExpiration) + "3";
            string keyName4 = nameof(CacheDoubleExpiration) + "4";
            string keyName5 = nameof(CacheDoubleExpiration) + "5";
//                string keyName6 = nameof(CacheDoubleExpiration) + "6";
            TestSharedCache ret;
            AmbientSharedCache<TestSharedCache> cache = new();
            await cache.Store(keyName1, this, TimeSpan.FromMilliseconds(51));
            await cache.Store(keyName2, this, TimeSpan.FromMilliseconds(50));
            await cache.Store(keyName3, this, TimeSpan.FromSeconds(50));
            await cache.Store(keyName4, this, TimeSpan.FromSeconds(50));
            await cache.Store(keyName5, this, TimeSpan.FromSeconds(50));
//                await cache.Store(keyName6, this, TimeSpan.FromSeconds(50));
            ret = await cache.Retrieve<TestSharedCache>(keyName2);
            Assert.IsNotNull(ret);
            await Eject(cache, 1);  // this should eject 1 because it's the LRU item
            ret = await cache.Retrieve<TestSharedCache>(keyName2);
            Assert.IsNotNull(ret);
            AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(100));
            ret = await cache.Retrieve<TestSharedCache>(keyName1);    // this should return null even though we haven't ejected stuff because it's expired
            Assert.IsNull(ret);
            await Eject(cache, 2);  // this should eject 2 because it's both expired, and 3 because it's the LRU item
            ret = await cache.Retrieve<TestSharedCache>(keyName1);
            Assert.IsNull(ret);
            ret = await cache.Retrieve<TestSharedCache>(keyName2);
            Assert.IsNull(ret);
            ret = await cache.Retrieve<TestSharedCache>(keyName3);
            Assert.IsNull(ret);
            ret = await cache.Retrieve<TestSharedCache>(keyName4);
            Assert.IsNotNull(ret);
            ret = await cache.Retrieve<TestSharedCache>(keyName5);
            Assert.IsNotNull(ret);
            //ret = await cache.Retrieve<TestCache>(keyName6);
            //Assert.IsNotNull(ret);
            await Eject(cache, 1);  // this should eject 4, but only because it's the LRU item
            ret = await cache.Retrieve<TestSharedCache>(keyName4);
            Assert.IsNull(ret);
            ret = await cache.Retrieve<TestSharedCache>(keyName5);
            Assert.IsNotNull(ret);
            //ret = await cache.Retrieve<TestCache>(keyName6);
            //Assert.IsNotNull(ret);
        }
    }
    /// <summary>
    /// Performs tests on <see cref="IAmbientSharedCache"/>.
    /// </summary>
    [TestMethod]
    public async Task CacheSpecifiedImplementation()
    {
        AmbientSettingsOverride localSettingsSet = new(TestCacheSettingsDictionary, nameof(CacheSpecifiedImplementation));
        using (AmbientClock.Pause())
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(localSettingsSet))
        {
            TestSharedCache ret;
            IAmbientSharedCache cacheService = new BasicAmbientCache(localSettingsSet);
            AmbientSharedCache<TestSharedCache> cache = new(cacheService, "prefix");
            await cache.Store<TestSharedCache>("Test1", this);
            ret = await cache.Retrieve<TestSharedCache>("Test1", null);
            Assert.AreEqual(this, ret);
            await cache.Remove<TestSharedCache>("Test1");
            ret = await cache.Retrieve<TestSharedCache>("Test1", null);
            Assert.IsNull(ret);
            await cache.Store<TestSharedCache>("Test2", this, null, DateTime.MinValue);
            ret = await cache.Retrieve<TestSharedCache>("Test2", null);
            Assert.AreEqual(this, ret);
            await Eject(cache, 1);
            ret = await cache.Retrieve<TestSharedCache>("Test2", null);
            Assert.IsNull(ret);
            await cache.Store<TestSharedCache>("Test3", this, TimeSpan.FromMinutes(-1));
            ret = await cache.Retrieve<TestSharedCache>("Test3", null);
            Assert.IsNull(ret);
            await cache.Store<TestSharedCache>("Test4", this, TimeSpan.FromMinutes(10), AmbientClock.UtcNow.AddMinutes(11));
            ret = await cache.Retrieve<TestSharedCache>("Test4", null);
            Assert.AreEqual(this, ret);
            await cache.Store<TestSharedCache>("Test5", this, TimeSpan.FromMinutes(10), AmbientClock.Now.AddMinutes(11));
            ret = await cache.Retrieve<TestSharedCache>("Test5", null);
            Assert.AreEqual(this, ret);
            await cache.Store<TestSharedCache>("Test6", this, TimeSpan.FromMinutes(60), AmbientClock.UtcNow.AddMinutes(10));
            ret = await cache.Retrieve<TestSharedCache>("Test6", null);
            Assert.AreEqual(this, ret);
            ret = await cache.Retrieve<TestSharedCache>("Test6", TimeSpan.FromMinutes(10));
            Assert.AreEqual(this, ret);
            await Eject(cache, 50);
            await cache.Clear();
            ret = await cache.Retrieve<TestSharedCache>("Test6", null);
            Assert.IsNull(ret);
        }
    }
    /// <summary>
    /// Performs tests on <see cref="IAmbientSharedCache"/>.
    /// </summary>
    [TestMethod]
    public async Task CacheRefresh()
    {
        AmbientSettingsOverride localSettingsSet = new(TestCacheSettingsDictionary, nameof(CacheRefresh));
        using (AmbientClock.Pause())
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(localSettingsSet))
        {
            TestSharedCache ret;
            IAmbientSharedCache cache = new BasicAmbientCache(localSettingsSet);
            await cache.Store<TestSharedCache>("CacheRefresh1", this, TimeSpan.FromSeconds(1));

            AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(1100));

            ret = await cache.Retrieve<TestSharedCache>("CacheRefresh1", null);
            Assert.IsNull(ret);
            await cache.Store<TestSharedCache>("CacheRefresh1", this, TimeSpan.FromMinutes(10));
            ret = await cache.Retrieve<TestSharedCache>("CacheRefresh1", null);
            Assert.AreEqual(this, ret);
            await Eject(cache, 1);

            await cache.Store<TestSharedCache>("CacheRefresh2", this);
            ret = await cache.Retrieve<TestSharedCache>("CacheRefresh2", null);
            Assert.AreEqual(this, ret);
            await cache.Store<TestSharedCache>("CacheRefresh3", this);
            ret = await cache.Retrieve<TestSharedCache>("CacheRefresh3", null);
            Assert.AreEqual(this, ret);
            await cache.Remove<TestSharedCache>("CacheRefresh3");
            ret = await cache.Retrieve<TestSharedCache>("CacheRefresh3", null);
            Assert.IsNull(ret);

            await Eject(cache, 1);
        }
    }
    private static readonly AmbientService<IAmbientSettingsSet> _Settings = Ambient.GetService<IAmbientSettingsSet>();
    private int CountsToEject => AmbientSettings.GetSetting<int>(_Settings.Local, nameof(BasicAmbientCache) + "-EjectFrequency", "The number of cache calls between cache ejections where at least one timed and one untimed entry is ejected from the cache.", s => Int32.Parse(s, System.Globalization.CultureInfo.InvariantCulture), "100").Value;
    private async Task Eject(IAmbientSharedCache cache, int count)
    {
        int countsToEject = CountsToEject;
        for (int ejection = 0; ejection < count; ++ejection)
        {
            for (int i = 0; i < countsToEject; ++i)
            {
                string shouldNotBeFoundValue;
                shouldNotBeFoundValue = await cache.Retrieve<string>("vhxcjklhdsufihs");
            }
        }
    }
    private async Task Eject<T>(AmbientSharedCache<T> cache, int count)
    {
        int countsToEject = CountsToEject;
        for (int ejection = 0; ejection < count; ++ejection)
        {
            for (int i = 0; i < countsToEject; ++i)
            {
                string shouldNotBeFoundValue;
                shouldNotBeFoundValue = await cache.Retrieve<string>("vhxcjklhdsufihs");
            }
        }
    }
}

sealed class AmbientSettingsOverride : IAmbientSettingsSet
{
    private readonly LazyUnsubscribeWeakEventListenerProxy<AmbientSettingsOverride, object, IAmbientSettingInfo> _weakSettingRegistered;
    private readonly IAmbientSettingsSet _fallbackSettings;
    private readonly ConcurrentDictionary<string, string> _overrideRawSettings;
    private readonly ConcurrentDictionary<string, object> _overrideTypedSettings;

    public AmbientSettingsOverride(Dictionary<string, string> overrideSettings, string name, IAmbientSettingsSet fallback = null, AmbientService<IAmbientSettingsSet> settings = null)
    {
        _overrideRawSettings = new ConcurrentDictionary<string, string>(overrideSettings);
        _overrideTypedSettings = new ConcurrentDictionary<string, object>();
        foreach (string key in overrideSettings.Keys)
        {
            IAmbientSettingInfo ps = SettingsRegistry.DefaultRegistry.TryGetSetting(key);
            if (ps != null) _overrideTypedSettings[key] = ps.Convert(this, overrideSettings[key]);
        }
        SetName = name;
        _fallbackSettings = fallback ?? settings?.Local;
        _weakSettingRegistered = new LazyUnsubscribeWeakEventListenerProxy<AmbientSettingsOverride, object, IAmbientSettingInfo>(
                this, NewSettingRegistered, wvc => SettingsRegistry.DefaultRegistry.SettingRegistered -= wvc.WeakEventHandler);
        SettingsRegistry.DefaultRegistry.SettingRegistered += _weakSettingRegistered.WeakEventHandler;
    }
    static void NewSettingRegistered(AmbientSettingsOverride settingsSet, object sender, IAmbientSettingInfo setting)
    {
        // is there a value for this setting?
        string value;
        if (settingsSet._overrideRawSettings.TryGetValue(setting.Key, out value))
        {
            // get the typed value
            settingsSet._overrideTypedSettings[setting.Key] = setting.Convert(settingsSet, value ?? "");
        }
    }

    public string SetName { get; }
    /// <summary>
    /// Gets whether or not the settings set is mutable.
    /// </summary>
    public bool SettingsAreMutable => true;

    public bool ChangeSetting(string key, string value)
    {
        string oldValue = null;
        _overrideRawSettings.AddOrUpdate(key, value, (k, v) => { oldValue = v; return value; } );
        // no change?
        if (String.Equals(oldValue, value, StringComparison.Ordinal)) return false;
        IAmbientSettingInfo ps = SettingsRegistry.DefaultRegistry.TryGetSetting(key);
        _overrideTypedSettings[key] = ps?.Convert(this, value);
        return true;
    }
    /// <summary>
    /// Gets the current raw value for the setting with the specified key, or null if the setting is not set.
    /// </summary>
    /// <param name="key">A key identifying the setting whose value is to be retrieved.</param>
    /// <returns>The setting value, or null if the setting is not set.</returns>
    public string GetRawValue(string key)
    {
        string value;
        if (_overrideRawSettings.TryGetValue(key, out value))
        {
            return value;
        }
        return _fallbackSettings?.GetRawValue(key) ?? SettingsRegistry.DefaultRegistry.TryGetSetting(key)?.DefaultValueString;
    }
    /// <summary>
    /// Gets the current typed value for the setting with the specified key, or null if the setting is not set.
    /// </summary>
    /// <param name="key">A key identifying the setting whose value is to be retrieved.</param>
    /// <returns>The setting value, or null if the setting is not set.</returns>
    public object GetTypedValue(string key)
    {
        object value;
        if (_overrideTypedSettings.TryGetValue(key, out value))
        {
            return value;
        }
        return _fallbackSettings?.GetTypedValue(key) ?? SettingsRegistry.DefaultRegistry.TryGetSetting(key)?.DefaultValue;
    }
}


#nullable enable
[DefaultAmbientService]
internal class BasicAmbientCache : IAmbientSharedCache
{
    private static readonly AmbientService<IAmbientSettingsSet> _Settings = Ambient.GetService<IAmbientSettingsSet>();

    private readonly IAmbientSetting<int> _callFrequencyToEject;
    private readonly IAmbientSetting<int> _countToEject;
    private readonly IAmbientSetting<int> _minCacheEntries;
    private int _expireCount;
    private ConcurrentQueue<TimedQueueEntry> _timedQueue = new();
    private ConcurrentQueue<string> _untimedQueue = new();
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();

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
#if NET5_0_OR_GREATER
    public async ValueTask Dispose()
    {
        // if the entry is disposable, dispose it after removing it
        if (Entry is IAsyncDisposable asyncDisposable) await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        if (Entry is IDisposable disposable) disposable.Dispose();
    }
#else
        public async ValueTask Dispose()
        {
            // if the entry is disposable, dispose it after removing it
            if (Entry is IDisposable disposable) disposable.Dispose();
            await Task.CompletedTask.ConfigureAwait(false);
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

    public async ValueTask Store<T>(string itemKey, T item, TimeSpan? maxCacheDuration = null, DateTime? expiration = null, CancellationToken cancel = default) where T : class
    {
        // does this entry *not* expire in the past?
        if (!(maxCacheDuration < TimeSpan.FromTicks(0)))
        {
            DateTime? actualExpiration = null;
            DateTime now = AmbientClock.UtcNow;
            if (maxCacheDuration != null) actualExpiration = now.Add(maxCacheDuration.Value);
            if (expiration != null && expiration.Value.Kind == DateTimeKind.Local) expiration = expiration.Value.ToUniversalTime();
            if (expiration < actualExpiration) actualExpiration = expiration;
            CacheEntry entry = new(itemKey, actualExpiration, item);
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
        await EjectIfNeeded().ConfigureAwait(false);
    }
    async ValueTask EjectIfNeeded()
    {
        int callFrequencyToEject = _callFrequencyToEject.Value;
        int countToEject = _countToEject.Value;
        // time to eject?
        while ((Interlocked.Increment(ref _expireCount) % callFrequencyToEject) == 0 || (_untimedQueue.Count + _timedQueue.Count) > countToEject)
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

    public async ValueTask Remove<T>(string itemKey, CancellationToken cancel = default)
    {
        CacheEntry? disposeEntry;
        if (_cache.TryRemove(itemKey, out disposeEntry))
        {
            // we don't remove the entry from the queue, but that's okay because we'll just ignore that entry when we get to it
            await disposeEntry!.Dispose().ConfigureAwait(false);  // if it was successfully removed, it can't be null
        }
    }

    private async ValueTask EjectEntry(CacheEntry entry, CancellationToken cancel = default)
    {
        CacheEntry? disposeEntry;
        // race to remove the item from the cache--did we win the race?
        if (_cache.TryRemove(entry.Key, out disposeEntry))
        {
            await disposeEntry!.Dispose().ConfigureAwait(false);  // if it was successfully removed, it can't be null
        }
    }

    public async ValueTask Clear(CancellationToken cancel = default)
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
#nullable disable


