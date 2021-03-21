using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmbientServices.Test
{
    /// <summary>
    /// A class that holds tests for <see cref="IAmbientCache"/>.
    /// </summary>
    [TestClass]
    public class TestCache
    {
        private static readonly Dictionary<string, string> TestCacheSettingsDictionary = new Dictionary<string, string>() { { nameof(BasicAmbientCache) + "-EjectFrequency", "10" }, { nameof(BasicAmbientCache) + "-MaximumItemCount", "20" }, { nameof(BasicAmbientCache) + "-MinimumItemCount", "1" } };
        private static readonly Dictionary<string, string> AllowEmptyCacheSettingsDictionary = new Dictionary<string, string>() { { nameof(BasicAmbientCache) + "-EjectFrequency", "40" }, { nameof(BasicAmbientCache) + "-MaximumItemCount", "20" }, { nameof(BasicAmbientCache) + "-MinimumItemCount", "-1" } };
        /// <summary>
        /// Performs tests on <see cref="IAmbientCache"/>.
        /// </summary>
        [TestMethod]
        public async Task CacheAmbient()
        {
            AmbientSettingsOverride localSettingsSet = new AmbientSettingsOverride(TestCacheSettingsDictionary, nameof(CacheAmbient));
            using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(localSettingsSet))
            {
                IAmbientCache localOverride = new BasicAmbientCache();
                using (ScopedLocalServiceOverride<IAmbientCache> localCache = new ScopedLocalServiceOverride<IAmbientCache>(localOverride))
                {
                    TestCache ret;
                    AmbientCache<TestCache> cache = new AmbientCache<TestCache>();
                    await cache.Store(true, "Test1", this);
                    await cache.Store(true, "Test1", this);
                    ret = await cache.Retrieve<TestCache>("Test1", null);
                    Assert.AreEqual(this, ret);
                    await cache.Remove<TestCache>(true, "Test1");
                    ret = await cache.Retrieve<TestCache>("Test1", null);
                    Assert.IsNull(ret);
                    await cache.Store(true, "Test2", this, null, DateTime.MinValue);
                    ret = await cache.Retrieve<TestCache>("Test2", null);
                    Assert.AreEqual(this, ret);
                    await Eject(cache, 2);
                    ret = await cache.Retrieve<TestCache>("Test2", null);
                    Assert.IsNull(ret);
                    await cache.Store(true, "Test3", this, TimeSpan.FromMinutes(-1));
                    ret = await cache.Retrieve<TestCache>("Test3", null);
                    Assert.IsNull(ret);
                    await cache.Store(true, "Test4", this, TimeSpan.FromMinutes(10), DateTime.UtcNow.AddMinutes(11));
                    ret = await cache.Retrieve<TestCache>("Test4", null);
                    Assert.AreEqual(this, ret);
                    await cache.Store(true, "Test5", this, TimeSpan.FromMinutes(10), DateTime.Now.AddMinutes(11));
                    ret = await cache.Retrieve<TestCache>("Test5", null);
                    Assert.AreEqual(this, ret);
                    await cache.Store(true, "Test6", this, TimeSpan.FromMinutes(60), DateTime.UtcNow.AddMinutes(10));
                    ret = await cache.Retrieve<TestCache>("Test6", null);
                    Assert.AreEqual(this, ret);
                    ret = await cache.Retrieve<TestCache>("Test6", TimeSpan.FromMinutes(10));
                    Assert.AreEqual(this, ret);
                    await Eject(cache, 50);
                    await cache.Clear();
                    ret = await cache.Retrieve<TestCache>("Test6", null);
                    Assert.IsNull(ret);
                }
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientCache"/>.
        /// </summary>
        [TestMethod]
        public async Task CacheNone()
        {
            using (ScopedLocalServiceOverride<IAmbientCache> localCache = new ScopedLocalServiceOverride<IAmbientCache>(null))
            {
                TestCache ret;
                AmbientCache<TestCache> cache = new AmbientCache<TestCache>();
                await cache.Store(true, "Test1", this);
                ret = await cache.Retrieve<TestCache>("Test1");
                Assert.IsNull(ret);
                await cache.Remove<TestCache>(true, "Test1");
                ret = await cache.Retrieve<TestCache>("Test1", null);
                Assert.IsNull(ret);
                await cache.Clear();
                ret = await cache.Retrieve<TestCache>("Test1", null);
                Assert.IsNull(ret);
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientCache"/>.
        /// </summary>
        [TestMethod]
        public async Task CacheExpiration()
        {
            IAmbientCache localOverride = new BasicAmbientCache();
            using (AmbientClock.Pause())
            using (ScopedLocalServiceOverride<IAmbientCache> localCache = new ScopedLocalServiceOverride<IAmbientCache>(localOverride))
            {
                string keyName1 = nameof(CacheExpiration) + "1";
                string keyName2 = nameof(CacheExpiration) + "2";
                string keyName3 = nameof(CacheExpiration) + "3";
                string keyName4 = nameof(CacheExpiration) + "4";
                string keyName5 = nameof(CacheExpiration) + "5";
                string keyName6 = nameof(CacheExpiration) + "6";
                string keyName7 = nameof(CacheExpiration) + "7";
                TestCache ret;
                AmbientCache<TestCache> cache = new AmbientCache<TestCache>();
                await cache.Store(true, keyName1, this, TimeSpan.FromMilliseconds(50));
                await cache.Store(true, keyName1, this, TimeSpan.FromMilliseconds(51));
                await cache.Store(true, keyName2, this);
                await cache.Store(true, keyName2, this);
                await cache.Store(true, keyName3, this, TimeSpan.FromMilliseconds(-51));    // this should never get cached because the time span is negative
                await cache.Store(true, keyName3, this, TimeSpan.FromMilliseconds(-50));    // this should never get cached because the time span is negative
                await cache.Store(true, keyName4, this);
                await cache.Store(true, keyName4, this);
                await cache.Store(true, keyName5, this, TimeSpan.FromMilliseconds(50));
                await cache.Store(true, keyName5, this, TimeSpan.FromMilliseconds(50));
                await cache.Store(true, keyName6, this, TimeSpan.FromMilliseconds(1000));
                await cache.Store(true, keyName6, this, TimeSpan.FromMilliseconds(1000));
                await cache.Store(true, keyName7, this, TimeSpan.FromMilliseconds(75));
                await cache.Store(true, keyName7, this, TimeSpan.FromMilliseconds(1000));
                ret = await cache.Retrieve<TestCache>(keyName1);
                Assert.IsNotNull(ret);
                ret = await cache.Retrieve<TestCache>(keyName2);
                Assert.IsNotNull(ret);
                ret = await cache.Retrieve<TestCache>(keyName3);
                Assert.IsNull(ret);
                ret = await cache.Retrieve<TestCache>(keyName4);
                Assert.IsNotNull(ret);
                ret = await cache.Retrieve<TestCache>(keyName5);
                Assert.IsNotNull(ret);
                ret = await cache.Retrieve<TestCache>(keyName6);
                Assert.IsNotNull(ret);
                ret = await cache.Retrieve<TestCache>(keyName7);
                Assert.IsNotNull(ret);
                await Eject(cache, 1);  // this should eject 1 because it's the LRU timed and 2 because it's the LRU untimed
                ret = await cache.Retrieve<TestCache>(keyName1);
                Assert.IsNull(ret);
                ret = await cache.Retrieve<TestCache>(keyName2);
                Assert.IsNull(ret);
                ret = await cache.Retrieve<TestCache>(keyName4);
                Assert.IsNotNull(ret);
                ret = await cache.Retrieve<TestCache>(keyName5);
                Assert.IsNotNull(ret);
                ret = await cache.Retrieve<TestCache>(keyName6);
                Assert.IsNotNull(ret);
                ret = await cache.Retrieve<TestCache>(keyName7);
                Assert.IsNotNull(ret);
                await Eject(cache, 1);  // this should eject 5 because it's the LRU timed and 4 because it's the LRU untimed
                ret = await cache.Retrieve<TestCache>(keyName4);
                Assert.IsNull(ret);
                ret = await cache.Retrieve<TestCache>(keyName5);
                Assert.IsNull(ret);
                ret = await cache.Retrieve<TestCache>(keyName6);
                Assert.IsNotNull(ret);
                ret = await cache.Retrieve<TestCache>(keyName7);
                Assert.IsNotNull(ret);
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(100));
                await Eject(cache, 1);  // this should eject 6 because it's the LRU timed but not 7 because only the first entry is expired, and not untimed LRU
                ret = await cache.Retrieve<TestCache>(keyName6);
                Assert.IsNull(ret);
                ret = await cache.Retrieve<TestCache>(keyName7);
                Assert.IsNotNull(ret);
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientCache"/>.
        /// </summary>
        [TestMethod]
        public async Task CacheSkipAndEmptyEject()
        {
            AmbientSettingsOverride localSettingsSet = new AmbientSettingsOverride(AllowEmptyCacheSettingsDictionary, nameof(CacheAmbient));
            using (AmbientClock.Pause())
            using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(localSettingsSet))
            {
                IAmbientCache localOverride = new BasicAmbientCache();
                using (ScopedLocalServiceOverride<IAmbientCache> localCache = new ScopedLocalServiceOverride<IAmbientCache>(localOverride))
                {
                    string keyName1 = nameof(CacheExpiration) + "1";
                    string keyName2 = nameof(CacheExpiration) + "2";
                    string keyName3 = nameof(CacheExpiration) + "3";
                    //string keyName4 = nameof(CacheExpiration) + "4";
                    //string keyName5 = nameof(CacheExpiration) + "5";
                    //string keyName6 = nameof(CacheExpiration) + "6";
                    //string keyName7 = nameof(CacheExpiration) + "7";
                    TestCache ret;
                    AmbientCache<TestCache> cache = new AmbientCache<TestCache>();
                    await cache.Store(true, keyName1, this, TimeSpan.FromMilliseconds(100));
                    await cache.Store(true, keyName2, this, TimeSpan.FromMilliseconds(50));
                    await cache.Store(true, keyName3, this, TimeSpan.FromMilliseconds(100));
                    ret = await cache.Retrieve<TestCache>(keyName1);
                    Assert.IsNotNull(ret);
                    ret = await cache.Retrieve<TestCache>(keyName2, TimeSpan.FromMilliseconds(100));
                    Assert.IsNotNull(ret);
                    ret = await cache.Retrieve<TestCache>(keyName3);
                    Assert.IsNotNull(ret);
                    AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(50));
                    await Eject(cache, 1);  // this should eject 1 because it's the LRU timed, and the first timed entry for 2 because that's expired, but 2 should remain with a refreshed entry
                    ret = await cache.Retrieve<TestCache>(keyName1);
                    Assert.IsNull(ret);
                    ret = await cache.Retrieve<TestCache>(keyName2, TimeSpan.FromMilliseconds(100));
                    Assert.IsNotNull(ret);
                    ret = await cache.Retrieve<TestCache>(keyName3);
                    Assert.IsNotNull(ret);
                    await Eject(cache, 1);  // this should skip 2 because it's bee refershed again and eject 3 because it's the LRU timed
                    ret = await cache.Retrieve<TestCache>(keyName1);
                    Assert.IsNull(ret);
                    ret = await cache.Retrieve<TestCache>(keyName2);
                    Assert.IsNotNull(ret);
                    ret = await cache.Retrieve<TestCache>(keyName3);
                    Assert.IsNull(ret);

                    // change key2 to be untimed
                    await cache.Store(true, keyName2, this);
                    await Eject(cache, 1);  // this should skip over the timed entry for 2 but then eject it because it is untimed
                }
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientCache"/>.
        /// </summary>
        [TestMethod]
        public async Task CacheDoubleExpiration()
        {
            IAmbientCache localOverride = new BasicAmbientCache();
            using (AmbientClock.Pause())
            using (ScopedLocalServiceOverride<IAmbientCache> localCache = new ScopedLocalServiceOverride<IAmbientCache>(localOverride))
            {
                string keyName1 = nameof(CacheDoubleExpiration) + "1";
                string keyName2 = nameof(CacheDoubleExpiration) + "2";
                string keyName3 = nameof(CacheDoubleExpiration) + "3";
                string keyName4 = nameof(CacheDoubleExpiration) + "4";
                string keyName5 = nameof(CacheDoubleExpiration) + "5";
//                string keyName6 = nameof(CacheDoubleExpiration) + "6";
                TestCache ret;
                AmbientCache<TestCache> cache = new AmbientCache<TestCache>();
                await cache.Store(true, keyName1, this, TimeSpan.FromMilliseconds(51));
                await cache.Store(true, keyName2, this, TimeSpan.FromMilliseconds(50));
                await cache.Store(true, keyName3, this, TimeSpan.FromSeconds(50));
                await cache.Store(true, keyName4, this, TimeSpan.FromSeconds(50));
                await cache.Store(true, keyName5, this, TimeSpan.FromSeconds(50));
//                await cache.Store(true, keyName6, this, TimeSpan.FromSeconds(50));
                ret = await cache.Retrieve<TestCache>(keyName2);
                Assert.IsNotNull(ret);
                await Eject(cache, 1);  // this should eject 1 because it's the LRU item
                ret = await cache.Retrieve<TestCache>(keyName2);
                Assert.IsNotNull(ret);
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(100));
                ret = await cache.Retrieve<TestCache>(keyName1);    // this should return null even though we haven't ejected stuff because it's expired
                Assert.IsNull(ret);
                await Eject(cache, 2);  // this should eject 2 because it's both expired, and 3 because it's the LRU item
                ret = await cache.Retrieve<TestCache>(keyName1);
                Assert.IsNull(ret);
                ret = await cache.Retrieve<TestCache>(keyName2);
                Assert.IsNull(ret);
                ret = await cache.Retrieve<TestCache>(keyName3);
                Assert.IsNull(ret);
                ret = await cache.Retrieve<TestCache>(keyName4);
                Assert.IsNotNull(ret);
                ret = await cache.Retrieve<TestCache>(keyName5);
                Assert.IsNotNull(ret);
                //ret = await cache.Retrieve<TestCache>(keyName6);
                //Assert.IsNotNull(ret);
                await Eject(cache, 1);  // this should eject 4, but only because it's the LRU item
                ret = await cache.Retrieve<TestCache>(keyName4);
                Assert.IsNull(ret);
                ret = await cache.Retrieve<TestCache>(keyName5);
                Assert.IsNotNull(ret);
                //ret = await cache.Retrieve<TestCache>(keyName6);
                //Assert.IsNotNull(ret);
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientCache"/>.
        /// </summary>
        [TestMethod]
        public async Task CacheSpecifiedImplementation()
        {
            AmbientSettingsOverride localSettingsSet = new AmbientSettingsOverride(TestCacheSettingsDictionary, nameof(CacheSpecifiedImplementation));
            using (AmbientClock.Pause())
            using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(localSettingsSet))
            {
                TestCache ret;
                IAmbientCache cacheService = new BasicAmbientCache(localSettingsSet);
                AmbientCache<TestCache> cache = new AmbientCache<TestCache>(cacheService, "prefix");
                await cache.Store<TestCache>(true, "Test1", this);
                ret = await cache.Retrieve<TestCache>("Test1", null);
                Assert.AreEqual(this, ret);
                await cache.Remove<TestCache>(true, "Test1");
                ret = await cache.Retrieve<TestCache>("Test1", null);
                Assert.IsNull(ret);
                await cache.Store<TestCache>(true, "Test2", this, null, DateTime.MinValue);
                ret = await cache.Retrieve<TestCache>("Test2", null);
                Assert.AreEqual(this, ret);
                await Eject(cache, 1);
                ret = await cache.Retrieve<TestCache>("Test2", null);
                Assert.IsNull(ret);
                await cache.Store<TestCache>(true, "Test3", this, TimeSpan.FromMinutes(-1));
                ret = await cache.Retrieve<TestCache>("Test3", null);
                Assert.IsNull(ret);
                await cache.Store<TestCache>(true, "Test4", this, TimeSpan.FromMinutes(10), AmbientClock.UtcNow.AddMinutes(11));
                ret = await cache.Retrieve<TestCache>("Test4", null);
                Assert.AreEqual(this, ret);
                await cache.Store<TestCache>(true, "Test5", this, TimeSpan.FromMinutes(10), AmbientClock.Now.AddMinutes(11));
                ret = await cache.Retrieve<TestCache>("Test5", null);
                Assert.AreEqual(this, ret);
                await cache.Store<TestCache>(true, "Test6", this, TimeSpan.FromMinutes(60), AmbientClock.UtcNow.AddMinutes(10));
                ret = await cache.Retrieve<TestCache>("Test6", null);
                Assert.AreEqual(this, ret);
                ret = await cache.Retrieve<TestCache>("Test6", TimeSpan.FromMinutes(10));
                Assert.AreEqual(this, ret);
                await Eject(cache, 50);
                await cache.Clear();
                ret = await cache.Retrieve<TestCache>("Test6", null);
                Assert.IsNull(ret);
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientCache"/>.
        /// </summary>
        [TestMethod]
        public async Task CacheRefresh()
        {
            AmbientSettingsOverride localSettingsSet = new AmbientSettingsOverride(TestCacheSettingsDictionary, nameof(CacheRefresh));
            using (AmbientClock.Pause())
            using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(localSettingsSet))
            {
                TestCache ret;
                IAmbientCache cache = new BasicAmbientCache(localSettingsSet);
                await cache.Store<TestCache>(true, "CacheRefresh1", this, TimeSpan.FromSeconds(1));

                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(1100));

                ret = await cache.Retrieve<TestCache>("CacheRefresh1", null);
                Assert.IsNull(ret);
                await cache.Store<TestCache>(true, "CacheRefresh1", this, TimeSpan.FromMinutes(10));
                ret = await cache.Retrieve<TestCache>("CacheRefresh1", null);
                Assert.AreEqual(this, ret);
                await Eject(cache, 1);

                await cache.Store<TestCache>(true, "CacheRefresh2", this);
                ret = await cache.Retrieve<TestCache>("CacheRefresh2", null);
                Assert.AreEqual(this, ret);
                await cache.Store<TestCache>(true, "CacheRefresh3", this);
                ret = await cache.Retrieve<TestCache>("CacheRefresh3", null);
                Assert.AreEqual(this, ret);
                await cache.Remove<TestCache>(true, "CacheRefresh3");
                ret = await cache.Retrieve<TestCache>("CacheRefresh3", null);
                Assert.IsNull(ret);

                await Eject(cache, 1);
            }
        }
        private static readonly AmbientService<IAmbientSettingsSet> _Settings = Ambient.GetService<IAmbientSettingsSet>();
        private int CountsToEject 
        {
            get 
            {
                return AmbientSettings.GetSetting<int>(_Settings.Local, nameof(BasicAmbientCache) + "-EjectFrequency", "The number of cache calls between cache ejections where at least one timed and one untimed entry is ejected from the cache.", s => Int32.Parse(s, System.Globalization.CultureInfo.InvariantCulture), "100").Value;
            }
        }
        private async Task Eject(IAmbientCache cache, int count)
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
        private async Task Eject<T>(AmbientCache<T> cache, int count)
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

    sealed class AmbientSettingsOverride : IMutableAmbientSettingsSet
    {
        private readonly LazyUnsubscribeWeakEventListenerProxy<AmbientSettingsOverride, object, IAmbientSettingInfo> _weakSettingRegistered;
        private readonly IAmbientSettingsSet _fallbackSettings;
        private readonly ConcurrentDictionary<string, string> _overrideRawSettings;
        private readonly ConcurrentDictionary<string, object> _overrideTypedSettings;
        private string _name;

        public AmbientSettingsOverride(Dictionary<string, string> overrideSettings, string name, IAmbientSettingsSet fallback = null, AmbientService<IAmbientSettingsSet> settings = null)
        {
            _overrideRawSettings = new ConcurrentDictionary<string, string>(overrideSettings);
            _overrideTypedSettings = new ConcurrentDictionary<string, object>();
            foreach (string key in overrideSettings.Keys)
            {
                IAmbientSettingInfo ps = SettingsRegistry.DefaultRegistry.TryGetSetting(key);
                if (ps != null) _overrideTypedSettings[key] = ps.Convert(this, overrideSettings[key]);
            }
            _name = name;
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

        public string SetName => _name;

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
}
