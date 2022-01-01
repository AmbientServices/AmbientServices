using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices.Test
{
    /// <summary>
    /// A class that holds tests for <see cref="IAmbientLocalCache"/>.
    /// </summary>
    [TestClass]
    public class TestLocalCache
    {
        private static readonly Dictionary<string, string> TestLocalCacheSettingsDictionary = new Dictionary<string, string>() { { nameof(BasicAmbientLocalCache) + "-EjectFrequency", "10" }, { nameof(BasicAmbientLocalCache) + "-MaximumItemCount", "20" }, { nameof(BasicAmbientLocalCache) + "-MinimumItemCount", "1" } };
        private static readonly Dictionary<string, string> AllowEmptyLocalCacheSettingsDictionary = new Dictionary<string, string>() { { nameof(BasicAmbientLocalCache) + "-EjectFrequency", "40" }, { nameof(BasicAmbientLocalCache) + "-MaximumItemCount", "20" }, { nameof(BasicAmbientLocalCache) + "-MinimumItemCount", "-1" } };
        /// <summary>
        /// Performs tests on <see cref="IAmbientLocalCache"/>.
        /// </summary>
        [TestMethod]
        public async Task LocalCacheAmbient()
        {
            AmbientSettingsOverride localSettingsSet = new AmbientSettingsOverride(TestLocalCacheSettingsDictionary, nameof(LocalCacheAmbient));
            using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(localSettingsSet))
            {
                IAmbientLocalCache localOverride = new BasicAmbientLocalCache();
                using (ScopedLocalServiceOverride<IAmbientLocalCache> localLocalCache = new ScopedLocalServiceOverride<IAmbientLocalCache>(localOverride))
                {
                    TestLocalCache ret;
                    AmbientLocalCache<TestLocalCache> cache = new AmbientLocalCache<TestLocalCache>();
                    await cache.Store("Test1", this);
                    await cache.Store("Test1", this);
                    ret = await cache.Retrieve<TestLocalCache>("Test1", null);
                    Assert.AreEqual(this, ret);
                    await cache.Remove<TestLocalCache>("Test1");
                    ret = await cache.Retrieve<TestLocalCache>("Test1", null);
                    Assert.IsNull(ret);
                    await cache.Store("Test2", this, null, DateTime.MinValue);
                    ret = await cache.Retrieve<TestLocalCache>("Test2", null);
                    Assert.AreEqual(this, ret);
                    await Eject(cache, 2);
                    ret = await cache.Retrieve<TestLocalCache>("Test2", null);
                    Assert.IsNull(ret);
                    await cache.Store("Test3", this, TimeSpan.FromMinutes(-1));
                    ret = await cache.Retrieve<TestLocalCache>("Test3", null);
                    Assert.IsNull(ret);
                    await cache.Store("Test4", this, TimeSpan.FromMinutes(10), DateTime.UtcNow.AddMinutes(11));
                    ret = await cache.Retrieve<TestLocalCache>("Test4", null);
                    Assert.AreEqual(this, ret);
                    await cache.Store("Test5", this, TimeSpan.FromMinutes(10), DateTime.Now.AddMinutes(11));
                    ret = await cache.Retrieve<TestLocalCache>("Test5", null);
                    Assert.AreEqual(this, ret);
                    await cache.Store("Test6", this, TimeSpan.FromMinutes(60), DateTime.UtcNow.AddMinutes(10));
                    ret = await cache.Retrieve<TestLocalCache>("Test6", null);
                    Assert.AreEqual(this, ret);
                    ret = await cache.Retrieve<TestLocalCache>("Test6", TimeSpan.FromMinutes(10));
                    Assert.AreEqual(this, ret);
                    await Eject(cache, 50);
                    await cache.Clear();
                    ret = await cache.Retrieve<TestLocalCache>("Test6", null);
                    Assert.IsNull(ret);
                }
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientLocalCache"/>.
        /// </summary>
        [TestMethod]
        public async Task LocalCacheNone()
        {
            using (ScopedLocalServiceOverride<IAmbientLocalCache> localLocalCache = new ScopedLocalServiceOverride<IAmbientLocalCache>(null))
            {
                TestLocalCache ret;
                AmbientLocalCache<TestLocalCache> cache = new AmbientLocalCache<TestLocalCache>();
                await cache.Store("Test1", this);
                ret = await cache.Retrieve<TestLocalCache>("Test1");
                Assert.IsNull(ret);
                await cache.Remove<TestLocalCache>("Test1");
                ret = await cache.Retrieve<TestLocalCache>("Test1", null);
                Assert.IsNull(ret);
                await cache.Clear();
                ret = await cache.Retrieve<TestLocalCache>("Test1", null);
                Assert.IsNull(ret);
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientLocalCache"/>.
        /// </summary>
        [TestMethod]
        public async Task LocalCacheExpiration()
        {
            IAmbientLocalCache localOverride = new BasicAmbientLocalCache();
            using (AmbientClock.Pause())
            using (ScopedLocalServiceOverride<IAmbientLocalCache> localLocalCache = new ScopedLocalServiceOverride<IAmbientLocalCache>(localOverride))
            {
                string keyName1 = nameof(LocalCacheExpiration) + "1";
                string keyName2 = nameof(LocalCacheExpiration) + "2";
                string keyName3 = nameof(LocalCacheExpiration) + "3";
                string keyName4 = nameof(LocalCacheExpiration) + "4";
                string keyName5 = nameof(LocalCacheExpiration) + "5";
                string keyName6 = nameof(LocalCacheExpiration) + "6";
                string keyName7 = nameof(LocalCacheExpiration) + "7";
                TestLocalCache ret;
                AmbientLocalCache<TestLocalCache> cache = new AmbientLocalCache<TestLocalCache>();
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
                ret = await cache.Retrieve<TestLocalCache>(keyName1);
                Assert.IsNotNull(ret);
                ret = await cache.Retrieve<TestLocalCache>(keyName2);
                Assert.IsNotNull(ret);
                ret = await cache.Retrieve<TestLocalCache>(keyName3);
                Assert.IsNull(ret);
                ret = await cache.Retrieve<TestLocalCache>(keyName4);
                Assert.IsNotNull(ret);
                ret = await cache.Retrieve<TestLocalCache>(keyName5);
                Assert.IsNotNull(ret);
                ret = await cache.Retrieve<TestLocalCache>(keyName6);
                Assert.IsNotNull(ret);
                ret = await cache.Retrieve<TestLocalCache>(keyName7);
                Assert.IsNotNull(ret);
                await Eject(cache, 1);  // this should eject 1 because it's the LRU timed and 2 because it's the LRU untimed
                ret = await cache.Retrieve<TestLocalCache>(keyName1);
                Assert.IsNull(ret);
                ret = await cache.Retrieve<TestLocalCache>(keyName2);
                Assert.IsNull(ret);
                ret = await cache.Retrieve<TestLocalCache>(keyName4);
                Assert.IsNotNull(ret);
                ret = await cache.Retrieve<TestLocalCache>(keyName5);
                Assert.IsNotNull(ret);
                ret = await cache.Retrieve<TestLocalCache>(keyName6);
                Assert.IsNotNull(ret);
                ret = await cache.Retrieve<TestLocalCache>(keyName7);
                Assert.IsNotNull(ret);
                await Eject(cache, 1);  // this should eject 5 because it's the LRU timed and 4 because it's the LRU untimed
                ret = await cache.Retrieve<TestLocalCache>(keyName4);
                Assert.IsNull(ret);
                ret = await cache.Retrieve<TestLocalCache>(keyName5);
                Assert.IsNull(ret);
                ret = await cache.Retrieve<TestLocalCache>(keyName6);
                Assert.IsNotNull(ret);
                ret = await cache.Retrieve<TestLocalCache>(keyName7);
                Assert.IsNotNull(ret);
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(100));
                await Eject(cache, 1);  // this should eject 6 because it's the LRU timed but not 7 because only the first entry is expired, and not untimed LRU
                ret = await cache.Retrieve<TestLocalCache>(keyName6);
                Assert.IsNull(ret);
                ret = await cache.Retrieve<TestLocalCache>(keyName7);
                Assert.IsNotNull(ret);
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientLocalCache"/>.
        /// </summary>
        [TestMethod]
        public async Task LocalCacheSkipAndEmptyEject()
        {
            AmbientSettingsOverride localSettingsSet = new AmbientSettingsOverride(AllowEmptyLocalCacheSettingsDictionary, nameof(LocalCacheAmbient));
            using (AmbientClock.Pause())
            using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(localSettingsSet))
            {
                IAmbientLocalCache localOverride = new BasicAmbientLocalCache();
                using (ScopedLocalServiceOverride<IAmbientLocalCache> localLocalCache = new ScopedLocalServiceOverride<IAmbientLocalCache>(localOverride))
                {
                    string keyName1 = nameof(LocalCacheExpiration) + "1";
                    string keyName2 = nameof(LocalCacheExpiration) + "2";
                    string keyName3 = nameof(LocalCacheExpiration) + "3";
                    //string keyName4 = nameof(LocalCacheExpiration) + "4";
                    //string keyName5 = nameof(LocalCacheExpiration) + "5";
                    //string keyName6 = nameof(LocalCacheExpiration) + "6";
                    //string keyName7 = nameof(LocalCacheExpiration) + "7";
                    TestLocalCache ret;
                    AmbientLocalCache<TestLocalCache> cache = new AmbientLocalCache<TestLocalCache>();
                    await cache.Store(keyName1, this, TimeSpan.FromMilliseconds(100));
                    await cache.Store(keyName2, this, TimeSpan.FromMilliseconds(50));
                    await cache.Store(keyName3, this, TimeSpan.FromMilliseconds(100));
                    ret = await cache.Retrieve<TestLocalCache>(keyName1);
                    Assert.IsNotNull(ret);
                    ret = await cache.Retrieve<TestLocalCache>(keyName2, TimeSpan.FromMilliseconds(100));
                    Assert.IsNotNull(ret);
                    ret = await cache.Retrieve<TestLocalCache>(keyName3);
                    Assert.IsNotNull(ret);
                    AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(50));
                    await Eject(cache, 1);  // this should eject 1 because it's the LRU timed, and the first timed entry for 2 because that's expired, but 2 should remain with a refreshed entry
                    ret = await cache.Retrieve<TestLocalCache>(keyName1);
                    Assert.IsNull(ret);
                    ret = await cache.Retrieve<TestLocalCache>(keyName2, TimeSpan.FromMilliseconds(100));
                    Assert.IsNotNull(ret);
                    ret = await cache.Retrieve<TestLocalCache>(keyName3);
                    Assert.IsNotNull(ret);
                    await Eject(cache, 1);  // this should skip 2 because it's bee refershed again and eject 3 because it's the LRU timed
                    ret = await cache.Retrieve<TestLocalCache>(keyName1);
                    Assert.IsNull(ret);
                    ret = await cache.Retrieve<TestLocalCache>(keyName2);
                    Assert.IsNotNull(ret);
                    ret = await cache.Retrieve<TestLocalCache>(keyName3);
                    Assert.IsNull(ret);

                    // change key2 to be untimed
                    await cache.Store(keyName2, this);
                    await Eject(cache, 1);  // this should skip over the timed entry for 2 but then eject it because it is untimed
                }
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientLocalCache"/>.
        /// </summary>
        [TestMethod]
        public async Task LocalCacheDoubleExpiration()
        {
            IAmbientLocalCache localOverride = new BasicAmbientLocalCache();
            using (AmbientClock.Pause())
            using (ScopedLocalServiceOverride<IAmbientLocalCache> localLocalCache = new ScopedLocalServiceOverride<IAmbientLocalCache>(localOverride))
            {
                string keyName1 = nameof(LocalCacheDoubleExpiration) + "1";
                string keyName2 = nameof(LocalCacheDoubleExpiration) + "2";
                string keyName3 = nameof(LocalCacheDoubleExpiration) + "3";
                string keyName4 = nameof(LocalCacheDoubleExpiration) + "4";
                string keyName5 = nameof(LocalCacheDoubleExpiration) + "5";
//                string keyName6 = nameof(LocalCacheDoubleExpiration) + "6";
                TestLocalCache ret;
                AmbientLocalCache<TestLocalCache> cache = new AmbientLocalCache<TestLocalCache>();
                await cache.Store(keyName1, this, TimeSpan.FromMilliseconds(51));
                await cache.Store(keyName2, this, TimeSpan.FromMilliseconds(50));
                await cache.Store(keyName3, this, TimeSpan.FromSeconds(50));
                await cache.Store(keyName4, this, TimeSpan.FromSeconds(50));
                await cache.Store(keyName5, this, TimeSpan.FromSeconds(50));
//                await cache.Store(keyName6, this, TimeSpan.FromSeconds(50));
                ret = await cache.Retrieve<TestLocalCache>(keyName2);
                Assert.IsNotNull(ret);
                await Eject(cache, 1);  // this should eject 1 because it's the LRU item
                ret = await cache.Retrieve<TestLocalCache>(keyName2);
                Assert.IsNotNull(ret);
                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(100));
                ret = await cache.Retrieve<TestLocalCache>(keyName1);    // this should return null even though we haven't ejected stuff because it's expired
                Assert.IsNull(ret);
                await Eject(cache, 2);  // this should eject 2 because it's both expired, and 3 because it's the LRU item
                ret = await cache.Retrieve<TestLocalCache>(keyName1);
                Assert.IsNull(ret);
                ret = await cache.Retrieve<TestLocalCache>(keyName2);
                Assert.IsNull(ret);
                ret = await cache.Retrieve<TestLocalCache>(keyName3);
                Assert.IsNull(ret);
                ret = await cache.Retrieve<TestLocalCache>(keyName4);
                Assert.IsNotNull(ret);
                ret = await cache.Retrieve<TestLocalCache>(keyName5);
                Assert.IsNotNull(ret);
                //ret = await cache.Retrieve<TestLocalCache>(keyName6);
                //Assert.IsNotNull(ret);
                await Eject(cache, 1);  // this should eject 4, but only because it's the LRU item
                ret = await cache.Retrieve<TestLocalCache>(keyName4);
                Assert.IsNull(ret);
                ret = await cache.Retrieve<TestLocalCache>(keyName5);
                Assert.IsNotNull(ret);
                //ret = await cache.Retrieve<TestLocalCache>(keyName6);
                //Assert.IsNotNull(ret);
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientLocalCache"/>.
        /// </summary>
        [TestMethod]
        public async Task LocalCacheSpecifiedImplementation()
        {
            AmbientSettingsOverride localSettingsSet = new AmbientSettingsOverride(TestLocalCacheSettingsDictionary, nameof(LocalCacheSpecifiedImplementation));
            using (AmbientClock.Pause())
            using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(localSettingsSet))
            {
                TestLocalCache ret;
                IAmbientLocalCache cacheService = new BasicAmbientLocalCache(localSettingsSet);
                AmbientLocalCache<TestLocalCache> cache = new AmbientLocalCache<TestLocalCache>(cacheService, "prefix");
                await cache.Store<TestLocalCache>("Test1", this);
                ret = await cache.Retrieve<TestLocalCache>("Test1", null);
                Assert.AreEqual(this, ret);
                await cache.Remove<TestLocalCache>("Test1");
                ret = await cache.Retrieve<TestLocalCache>("Test1", null);
                Assert.IsNull(ret);
                await cache.Store<TestLocalCache>("Test2", this, null, DateTime.MinValue);
                ret = await cache.Retrieve<TestLocalCache>("Test2", null);
                Assert.AreEqual(this, ret);
                await Eject(cache, 1);
                ret = await cache.Retrieve<TestLocalCache>("Test2", null);
                Assert.IsNull(ret);
                await cache.Store<TestLocalCache>("Test3", this, TimeSpan.FromMinutes(-1));
                ret = await cache.Retrieve<TestLocalCache>("Test3", null);
                Assert.IsNull(ret);
                await cache.Store<TestLocalCache>("Test4", this, TimeSpan.FromMinutes(10), AmbientClock.UtcNow.AddMinutes(11));
                ret = await cache.Retrieve<TestLocalCache>("Test4", null);
                Assert.AreEqual(this, ret);
                await cache.Store<TestLocalCache>("Test5", this, TimeSpan.FromMinutes(10), AmbientClock.Now.AddMinutes(11));
                ret = await cache.Retrieve<TestLocalCache>("Test5", null);
                Assert.AreEqual(this, ret);
                await cache.Store<TestLocalCache>("Test6", this, TimeSpan.FromMinutes(60), AmbientClock.UtcNow.AddMinutes(10));
                ret = await cache.Retrieve<TestLocalCache>("Test6", null);
                Assert.AreEqual(this, ret);
                ret = await cache.Retrieve<TestLocalCache>("Test6", TimeSpan.FromMinutes(10));
                Assert.AreEqual(this, ret);
                await Eject(cache, 50);
                await cache.Clear();
                ret = await cache.Retrieve<TestLocalCache>("Test6", null);
                Assert.IsNull(ret);
            }
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientLocalCache"/>.
        /// </summary>
        [TestMethod]
        public async Task LocalCacheRefresh()
        {
            AmbientSettingsOverride localSettingsSet = new AmbientSettingsOverride(TestLocalCacheSettingsDictionary, nameof(LocalCacheRefresh));
            using (AmbientClock.Pause())
            using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(localSettingsSet))
            {
                TestLocalCache ret;
                IAmbientLocalCache cache = new BasicAmbientLocalCache(localSettingsSet);
                await cache.Store<TestLocalCache>("LocalCacheRefresh1", this, TimeSpan.FromSeconds(1));

                AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(1100));

                ret = await cache.Retrieve<TestLocalCache>("LocalCacheRefresh1", null);
                Assert.IsNull(ret);
                await cache.Store<TestLocalCache>("LocalCacheRefresh1", this, TimeSpan.FromMinutes(10));
                ret = await cache.Retrieve<TestLocalCache>("LocalCacheRefresh1", null);
                Assert.AreEqual(this, ret);
                await Eject(cache, 1);

                await cache.Store<TestLocalCache>("LocalCacheRefresh2", this);
                ret = await cache.Retrieve<TestLocalCache>("LocalCacheRefresh2", null);
                Assert.AreEqual(this, ret);
                await cache.Store<TestLocalCache>("LocalCacheRefresh3", this);
                ret = await cache.Retrieve<TestLocalCache>("LocalCacheRefresh3", null);
                Assert.AreEqual(this, ret);
                await cache.Remove<TestLocalCache>("LocalCacheRefresh3");
                ret = await cache.Retrieve<TestLocalCache>("LocalCacheRefresh3", null);
                Assert.IsNull(ret);

                await Eject(cache, 1);
            }
        }
        private static readonly AmbientService<IAmbientSettingsSet> _Settings = Ambient.GetService<IAmbientSettingsSet>();
        private int CountsToEject 
        {
            get 
            {
                return AmbientSettings.GetSetting<int>(_Settings.Local, nameof(BasicAmbientLocalCache) + "-EjectFrequency", "The number of cache calls between cache ejections where at least one timed and one untimed entry is ejected from the cache.", s => Int32.Parse(s, System.Globalization.CultureInfo.InvariantCulture), "100").Value;
            }
        }
        private async Task Eject(IAmbientLocalCache cache, int count)
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
        private async Task Eject<T>(AmbientLocalCache<T> cache, int count)
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
}

