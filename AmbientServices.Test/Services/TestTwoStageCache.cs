using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AmbientServices.Test;

/// <summary>
/// A class that holds tests for <see cref="AmbientTwoStageCache"/>.
/// </summary>
[TestClass]
public class TestTwoStageCache
{
    private static readonly Dictionary<string, string> TestCacheSettingsDictionary = new() { { nameof(BasicAmbientCache) + "-EjectFrequency", "10" }, { nameof(BasicAmbientCache) + "-MaximumItemCount", "20" }, { nameof(BasicAmbientCache) + "-MinimumItemCount", "1" }, { nameof(BasicAmbientLocalCache) + "-EjectFrequency", "10" }, { nameof(BasicAmbientLocalCache) + "-MaximumItemCount", "20" }, { nameof(BasicAmbientLocalCache) + "-MinimumItemCount", "1" } };
    private static readonly Dictionary<string, string> AllowEmptyCacheSettingsDictionary = new() { { nameof(BasicAmbientCache) + "-EjectFrequency", "40" }, { nameof(BasicAmbientCache) + "-MaximumItemCount", "20" }, { nameof(BasicAmbientCache) + "-MinimumItemCount", "-1" }, { nameof(BasicAmbientLocalCache) + "-EjectFrequency", "40" }, { nameof(BasicAmbientLocalCache) + "-MaximumItemCount", "20" }, { nameof(BasicAmbientLocalCache) + "-MinimumItemCount", "-1" } };
    /// <summary>
    /// Performs tests on <see cref="AmbientTwoStageCache"/>.
    /// </summary>
    [TestMethod]
    public async Task CacheAmbient()
    {
        AmbientSettingsOverride localSettingsSet = new(TestCacheSettingsDictionary, nameof(CacheAmbient));
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(localSettingsSet))
        {
            IAmbientSharedCache localSharedOverride = new BasicAmbientCache();
            using ScopedLocalServiceOverride<IAmbientSharedCache> scopeSharedCache = new(localSharedOverride);
            IAmbientLocalCache localLocalOverride = new BasicAmbientLocalCache();
            using ScopedLocalServiceOverride<IAmbientLocalCache> scopeLocalCache = new(localLocalOverride);
            TestTwoStageCache ret;
            AmbientTwoStageCache<TestTwoStageCache> cache = new();
            await cache.Store("Test1", this);
            await cache.Store("Test1", this);
            ret = await cache.Retrieve<TestTwoStageCache>("Test1", null);
            Assert.AreEqual(this, ret);
            await cache.Remove<TestTwoStageCache>("Test1");
            ret = await cache.Retrieve<TestTwoStageCache>("Test1", null);
            Assert.IsNull(ret);
            await cache.Store("Test2", this, null, DateTime.MinValue);
            ret = await cache.Retrieve<TestTwoStageCache>("Test2", null);
            Assert.AreEqual(this, ret);
            await Eject(cache, 2);
            ret = await cache.Retrieve<TestTwoStageCache>("Test2", null);
            Assert.IsNull(ret);
            await cache.Store("Test3", this, TimeSpan.FromMinutes(-1));
            ret = await cache.Retrieve<TestTwoStageCache>("Test3", null);
            Assert.IsNull(ret);
            await cache.Store("Test4", this, TimeSpan.FromMinutes(10), DateTime.UtcNow.AddMinutes(11));
            ret = await cache.Retrieve<TestTwoStageCache>("Test4", null);
            Assert.AreEqual(this, ret);
            await cache.Store("Test5", this, TimeSpan.FromMinutes(10), DateTime.Now.AddMinutes(11));
            ret = await cache.Retrieve<TestTwoStageCache>("Test5", null);
            Assert.AreEqual(this, ret);
            await cache.Store("Test6", this, TimeSpan.FromMinutes(60), DateTime.UtcNow.AddMinutes(10));
            ret = await cache.Retrieve<TestTwoStageCache>("Test6", null);
            Assert.AreEqual(this, ret);
            ret = await cache.Retrieve<TestTwoStageCache>("Test6", TimeSpan.FromMinutes(10));
            Assert.AreEqual(this, ret);
            await Eject(cache, 50);
            await cache.Clear();
            ret = await cache.Retrieve<TestTwoStageCache>("Test6", null);
            Assert.IsNull(ret);
        }
    }
    /// <summary>
    /// Performs tests on <see cref="AmbientTwoStageCache"/>.
    /// </summary>
    [TestMethod]
    public async Task CacheNone()
    {
        using ScopedLocalServiceOverride<IAmbientSharedCache> localSharedCache = new(null);
        using ScopedLocalServiceOverride<IAmbientLocalCache> localLocalCache = new(null);
        TestTwoStageCache ret;
        AmbientTwoStageCache<TestTwoStageCache> cache = new();
        await cache.Store("Test1", this);
        ret = await cache.Retrieve<TestTwoStageCache>("Test1");
        Assert.IsNull(ret);
        await cache.Remove<TestTwoStageCache>("Test1");
        ret = await cache.Retrieve<TestTwoStageCache>("Test1", null);
        Assert.IsNull(ret);
        await cache.Clear();
        ret = await cache.Retrieve<TestTwoStageCache>("Test1", null);
        Assert.IsNull(ret);
    }
    /// <summary>
    /// Performs tests on <see cref="AmbientTwoStageCache"/>.
    /// </summary>
    [TestMethod]
    public async Task CacheExpiration()
    {
        BasicAmbientCache localOverride = new();
        BasicAmbientLocalCache localLocalOverride = new();
        using (AmbientClock.Pause())
        using (ScopedLocalServiceOverride<IAmbientSharedCache> localSharedCache = new(localOverride))
        using (ScopedLocalServiceOverride<IAmbientLocalCache> localLocalCache = new(localLocalOverride))
        {
            string keyName1 = nameof(CacheExpiration) + "1";
            string keyName2 = nameof(CacheExpiration) + "2";
            string keyName3 = nameof(CacheExpiration) + "3";
            string keyName4 = nameof(CacheExpiration) + "4";
            string keyName5 = nameof(CacheExpiration) + "5";
            string keyName6 = nameof(CacheExpiration) + "6";
            string keyName7 = nameof(CacheExpiration) + "7";
            TestTwoStageCache ret;
            AmbientTwoStageCache<TestTwoStageCache> cache = new();
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
            ret = await cache.Retrieve<TestTwoStageCache>(keyName1);
            Assert.IsNotNull(ret);
            ret = await cache.Retrieve<TestTwoStageCache>(keyName2);
            Assert.IsNotNull(ret);
            ret = await cache.Retrieve<TestTwoStageCache>(keyName3);
            Assert.IsNull(ret);
            ret = await cache.Retrieve<TestTwoStageCache>(keyName4);
            Assert.IsNotNull(ret);
            ret = await cache.Retrieve<TestTwoStageCache>(keyName5);
            Assert.IsNotNull(ret);
            ret = await cache.Retrieve<TestTwoStageCache>(keyName6);
            Assert.IsNotNull(ret);
            ret = await cache.Retrieve<TestTwoStageCache>(keyName7);
            Assert.IsNotNull(ret);
            await Eject(cache, 1);  // this should eject 1 because it's the LRU timed and 2 because it's the LRU untimed
            ret = await cache.Retrieve<TestTwoStageCache>(keyName1);
            Assert.IsNull(ret);
            ret = await cache.Retrieve<TestTwoStageCache>(keyName2);
            Assert.IsNull(ret);
            ret = await cache.Retrieve<TestTwoStageCache>(keyName4);
            Assert.IsNotNull(ret);
            ret = await cache.Retrieve<TestTwoStageCache>(keyName5);
            Assert.IsNotNull(ret);
            ret = await cache.Retrieve<TestTwoStageCache>(keyName6);
            Assert.IsNotNull(ret);
            ret = await cache.Retrieve<TestTwoStageCache>(keyName7);
            Assert.IsNotNull(ret);
            await Eject(cache, 1);  // this should eject 5 because it's the LRU timed and 4 because it's the LRU untimed
            ret = await cache.Retrieve<TestTwoStageCache>(keyName4);
            Assert.IsNull(ret);
            ret = await cache.Retrieve<TestTwoStageCache>(keyName5);
            Assert.IsNull(ret);
            ret = await cache.Retrieve<TestTwoStageCache>(keyName6);
            Assert.IsNotNull(ret);
            ret = await cache.Retrieve<TestTwoStageCache>(keyName7);
            Assert.IsNotNull(ret);
            AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(100));
            await Eject(cache, 1);  // this should eject 6 because it's the LRU timed but not 7 because only the first entry is expired, and not untimed LRU
            ret = await cache.Retrieve<TestTwoStageCache>(keyName6);
            Assert.IsNull(ret);
            ret = await cache.Retrieve<TestTwoStageCache>(keyName7);
            Assert.IsNotNull(ret);
        }
    }
    /// <summary>
    /// Performs tests on <see cref="AmbientTwoStageCache"/>.
    /// </summary>
    [TestMethod]
    public async Task CacheSkipAndEmptyEject()
    {
        AmbientSettingsOverride localSettingsSet = new(AllowEmptyCacheSettingsDictionary, nameof(CacheSkipAndEmptyEject));
        using (AmbientClock.Pause())
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(localSettingsSet))
        {
            BasicAmbientCache localOverride = new();
            BasicAmbientLocalCache localLocalOverride = new();
            using ScopedLocalServiceOverride<IAmbientSharedCache> localSharedCache = new(localOverride);
            using ScopedLocalServiceOverride<IAmbientLocalCache> localLocalCache = new(localLocalOverride);
            string keyName1 = nameof(CacheExpiration) + "1";
            string keyName2 = nameof(CacheExpiration) + "2";
            string keyName3 = nameof(CacheExpiration) + "3";
            //string keyName4 = nameof(CacheExpiration) + "4";
            //string keyName5 = nameof(CacheExpiration) + "5";
            //string keyName6 = nameof(CacheExpiration) + "6";
            //string keyName7 = nameof(CacheExpiration) + "7";
            TestTwoStageCache ret;
            AmbientTwoStageCache<TestTwoStageCache> cache = new();
            await cache.Store(keyName1, this, TimeSpan.FromMilliseconds(100));
            await cache.Store(keyName2, this, TimeSpan.FromMilliseconds(50));
            await cache.Store(keyName3, this, TimeSpan.FromMilliseconds(100));
            ret = await cache.Retrieve<TestTwoStageCache>(keyName1);
            Assert.IsNotNull(ret);
            ret = await cache.Retrieve<TestTwoStageCache>(keyName2, TimeSpan.FromMilliseconds(100));
            Assert.IsNotNull(ret);
            ret = await cache.Retrieve<TestTwoStageCache>(keyName3);
            Assert.IsNotNull(ret);
            AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(50));
            await Eject(cache, 1);  // this should eject 1 because it's the LRU timed, and the first timed entry for 2 because that's expired, but 2 should remain with a refreshed entry
            ret = await cache.Retrieve<TestTwoStageCache>(keyName1);
            Assert.IsNull(ret);
            ret = await cache.Retrieve<TestTwoStageCache>(keyName2, TimeSpan.FromMilliseconds(100));
            Assert.IsNotNull(ret);
            ret = await cache.Retrieve<TestTwoStageCache>(keyName3);
            Assert.IsNotNull(ret);
            await Eject(cache, 1);  // this should skip 2 because it's bee refershed again and eject 3 because it's the LRU timed
            ret = await cache.Retrieve<TestTwoStageCache>(keyName1);
            Assert.IsNull(ret);
            ret = await cache.Retrieve<TestTwoStageCache>(keyName2);
            Assert.IsNotNull(ret);
            ret = await cache.Retrieve<TestTwoStageCache>(keyName3);
            Assert.IsNull(ret);

            // change key2 to be untimed
            await cache.Store(keyName2, this);
            await Eject(cache, 1);  // this should skip over the timed entry for 2 but then eject it because it is untimed
        }
    }
    /// <summary>
    /// Performs tests on <see cref="AmbientTwoStageCache"/>.
    /// </summary>
    [TestMethod]
    public async Task CacheDoubleExpiration()
    {
        BasicAmbientCache localOverride = new();
        BasicAmbientLocalCache localLocalOverride = new();
        using (AmbientClock.Pause())
        using (ScopedLocalServiceOverride<IAmbientSharedCache> localSharedCache = new(localOverride))
        using (ScopedLocalServiceOverride<IAmbientLocalCache> localLocalCache = new(localLocalOverride))
        {
            string keyName1 = nameof(CacheDoubleExpiration) + "1";
            string keyName2 = nameof(CacheDoubleExpiration) + "2";
            string keyName3 = nameof(CacheDoubleExpiration) + "3";
            string keyName4 = nameof(CacheDoubleExpiration) + "4";
            string keyName5 = nameof(CacheDoubleExpiration) + "5";
//                string keyName6 = nameof(CacheDoubleExpiration) + "6";
            TestTwoStageCache ret;
            AmbientTwoStageCache<TestTwoStageCache> cache = new();
            await cache.Store(keyName1, this, TimeSpan.FromMilliseconds(51));
            await cache.Store(keyName2, this, TimeSpan.FromMilliseconds(50));
            await cache.Store(keyName3, this, TimeSpan.FromSeconds(50));
            await cache.Store(keyName4, this, TimeSpan.FromSeconds(50));
            await cache.Store(keyName5, this, TimeSpan.FromSeconds(50));
//                await cache.Store(keyName6, this, TimeSpan.FromSeconds(50));
            ret = await cache.Retrieve<TestTwoStageCache>(keyName2);
            Assert.IsNotNull(ret);
            await Eject(cache, 1);  // this should eject 1 because it's the LRU item
            ret = await cache.Retrieve<TestTwoStageCache>(keyName2);
            Assert.IsNotNull(ret);
            AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(100));
            ret = await cache.Retrieve<TestTwoStageCache>(keyName1);    // this should return null even though we haven't ejected stuff because it's expired
            Assert.IsNull(ret);
            await Eject(cache, 2);  // this should eject 2 because it's both expired, and 3 because it's the LRU item
            ret = await cache.Retrieve<TestTwoStageCache>(keyName1);
            Assert.IsNull(ret);
            ret = await cache.Retrieve<TestTwoStageCache>(keyName2);
            Assert.IsNull(ret);
            ret = await cache.Retrieve<TestTwoStageCache>(keyName3);
            Assert.IsNull(ret);
            ret = await cache.Retrieve<TestTwoStageCache>(keyName4);
            Assert.IsNotNull(ret);
            ret = await cache.Retrieve<TestTwoStageCache>(keyName5);
            Assert.IsNotNull(ret);
            //ret = await cache.Retrieve<TestTwoStageCache>(keyName6);
            //Assert.IsNotNull(ret);
            await Eject(cache, 1);  // this should eject 4, but only because it's the LRU item
            ret = await cache.Retrieve<TestTwoStageCache>(keyName4);
            Assert.IsNull(ret);
            ret = await cache.Retrieve<TestTwoStageCache>(keyName5);
            Assert.IsNotNull(ret);
            //ret = await cache.Retrieve<TestTwoStageCache>(keyName6);
            //Assert.IsNotNull(ret);
        }
    }
    /// <summary>
    /// Performs tests on <see cref="AmbientTwoStageCache"/>.
    /// </summary>
    [TestMethod]
    public async Task CacheSpecifiedImplementation()
    {
        AmbientSettingsOverride localSettingsSet = new(TestCacheSettingsDictionary, nameof(CacheSpecifiedImplementation));
        using (AmbientClock.Pause())
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(localSettingsSet))
        {
            TestTwoStageCache ret;
            BasicAmbientCache localSharedCacheService = new(localSettingsSet);
            BasicAmbientLocalCache localLocalCacheService = new(localSettingsSet);
            AmbientTwoStageCache<TestTwoStageCache> cache = new(localLocalCacheService, localSharedCacheService, "prefix");
            await cache.Store<TestTwoStageCache>("Test1", this);
            ret = await cache.Retrieve<TestTwoStageCache>("Test1", null);
            Assert.AreEqual(this, ret);
            await cache.Remove<TestTwoStageCache>("Test1");
            ret = await cache.Retrieve<TestTwoStageCache>("Test1", null);
            Assert.IsNull(ret);
            await cache.Store<TestTwoStageCache>("Test2", this, null, DateTime.MinValue);
            ret = await cache.Retrieve<TestTwoStageCache>("Test2", null);
            Assert.AreEqual(this, ret);
            await Eject(cache, 1);
            ret = await cache.Retrieve<TestTwoStageCache>("Test2", null);
            Assert.IsNull(ret);
            await cache.Store<TestTwoStageCache>("Test3", this, TimeSpan.FromMinutes(-1));
            ret = await cache.Retrieve<TestTwoStageCache>("Test3", null);
            Assert.IsNull(ret);
            await cache.Store<TestTwoStageCache>("Test4", this, TimeSpan.FromMinutes(10), AmbientClock.UtcNow.AddMinutes(11));
            ret = await cache.Retrieve<TestTwoStageCache>("Test4", null);
            Assert.AreEqual(this, ret);
            await cache.Store<TestTwoStageCache>("Test5", this, TimeSpan.FromMinutes(10), AmbientClock.Now.AddMinutes(11));
            ret = await cache.Retrieve<TestTwoStageCache>("Test5", null);
            Assert.AreEqual(this, ret);
            await cache.Store<TestTwoStageCache>("Test6", this, TimeSpan.FromMinutes(60), AmbientClock.UtcNow.AddMinutes(10));
            ret = await cache.Retrieve<TestTwoStageCache>("Test6", null);
            Assert.AreEqual(this, ret);
            ret = await cache.Retrieve<TestTwoStageCache>("Test6", TimeSpan.FromMinutes(10));
            Assert.AreEqual(this, ret);
            await Eject(cache, 50);
            await cache.Clear();
            ret = await cache.Retrieve<TestTwoStageCache>("Test6", null);
            Assert.IsNull(ret);
        }
    }
    /// <summary>
    /// Performs tests on <see cref="AmbientTwoStageCache"/>.
    /// </summary>
    [TestMethod]
    public async Task CacheRefresh()
    {
        AmbientSettingsOverride localSettingsSet = new(TestCacheSettingsDictionary, nameof(CacheRefresh));
        using (AmbientClock.Pause())
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(localSettingsSet))
        {
            TestTwoStageCache ret;
            BasicAmbientCache localSharedCacheService = new(localSettingsSet);
            BasicAmbientLocalCache localLocalCacheService = new(localSettingsSet);
            AmbientTwoStageCache<TestTwoStageCache> cache = new(localLocalCacheService, localSharedCacheService, "prefix");
            await cache.Store<TestTwoStageCache>("CacheRefresh1", this, TimeSpan.FromSeconds(1));

            AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(1100));

            ret = await cache.Retrieve<TestTwoStageCache>("CacheRefresh1", null);
            Assert.IsNull(ret);
            await cache.Store<TestTwoStageCache>("CacheRefresh1", this, TimeSpan.FromMinutes(10));
            ret = await cache.Retrieve<TestTwoStageCache>("CacheRefresh1", null);
            Assert.AreEqual(this, ret);
            await Eject(cache, 1);

            await cache.Store<TestTwoStageCache>("CacheRefresh2", this);
            ret = await cache.Retrieve<TestTwoStageCache>("CacheRefresh2", null);
            Assert.AreEqual(this, ret);
            await cache.Store<TestTwoStageCache>("CacheRefresh3", this);
            ret = await cache.Retrieve<TestTwoStageCache>("CacheRefresh3", null);
            Assert.AreEqual(this, ret);
            await cache.Remove<TestTwoStageCache>("CacheRefresh3");
            ret = await cache.Retrieve<TestTwoStageCache>("CacheRefresh3", null);
            Assert.IsNull(ret);

            await Eject(cache, 1);
        }
    }
    private static readonly AmbientService<IAmbientSettingsSet> _Settings = Ambient.GetService<IAmbientSettingsSet>();
    private int CountsToEject => AmbientSettings.GetSetting<int>(_Settings.Local, nameof(BasicAmbientCache) + "-EjectFrequency", "The number of cache calls between cache ejections where at least one timed and one untimed entry is ejected from the cache.", s => Int32.Parse(s, System.Globalization.CultureInfo.InvariantCulture), "100").Value;
    private async Task Eject<T>(AmbientTwoStageCache<T> cache, int count)
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

