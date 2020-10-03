using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestAmbientServices
{
    /// <summary>
    /// A class that holds tests for <see cref="IAmbientCacheProvider"/>.
    /// </summary>
    [TestClass]
    public class TestCache
    {
        private static readonly Dictionary<string, string> TestCacheSettingsDictionary = new Dictionary<string, string>() { { nameof(BasicAmbientCache) + "-EjectFrequency", "10" }, { nameof(BasicAmbientCache) + "-ItemCount", "20" } };
        /// <summary>
        /// Performs tests on <see cref="IAmbientCacheProvider"/>.
        /// </summary>
        [TestMethod]
        public async Task CacheAmbient()
        {
            AmbientSettingsOverride localProvider = new AmbientSettingsOverride(TestCacheSettingsDictionary, nameof(CacheAmbient));
            using (new LocalServiceScopedOverride<IAmbientSettingsProvider>(localProvider))
            {
                IAmbientCacheProvider localOverride = new BasicAmbientCache();
                using (LocalServiceScopedOverride<IAmbientCacheProvider> localCache = new LocalServiceScopedOverride<IAmbientCacheProvider>(localOverride))
                {
                    TestCache ret;
                    AmbientCache<TestCache> cache = new AmbientCache<TestCache>();
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
        /// Performs tests on <see cref="IAmbientCacheProvider"/>.
        /// </summary>
        [TestMethod]
        public async Task CacheNone()
        {
            using (LocalServiceScopedOverride<IAmbientCacheProvider> localCache = new LocalServiceScopedOverride<IAmbientCacheProvider>(null))
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
        /// Performs tests on <see cref="IAmbientCacheProvider"/>.
        /// </summary>
        [TestMethod]
        public async Task CacheSpecifiedProvider()
        {
            AmbientSettingsOverride localProvider = new AmbientSettingsOverride(TestCacheSettingsDictionary, nameof(CacheSpecifiedProvider));
            using (new LocalServiceScopedOverride<IAmbientSettingsProvider>(localProvider))
            {
                TestCache ret;
                IAmbientCacheProvider cacheProvider = new BasicAmbientCache(localProvider);
                AmbientCache<TestCache> cache = new AmbientCache<TestCache>(cacheProvider, "prefix");
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
                await cache.Store<TestCache>(true, "Test4", this, TimeSpan.FromMinutes(10), DateTime.UtcNow.AddMinutes(11));
                ret = await cache.Retrieve<TestCache>("Test4", null);
                Assert.AreEqual(this, ret);
                await cache.Store<TestCache>(true, "Test5", this, TimeSpan.FromMinutes(10), DateTime.Now.AddMinutes(11));
                ret = await cache.Retrieve<TestCache>("Test5", null);
                Assert.AreEqual(this, ret);
                await cache.Store<TestCache>(true, "Test6", this, TimeSpan.FromMinutes(60), DateTime.UtcNow.AddMinutes(10));
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
        /// Performs tests on <see cref="IAmbientCacheProvider"/>.
        /// </summary>
        [TestMethod]
        public async Task CacheRefresh()
        {
            AmbientSettingsOverride localProvider = new AmbientSettingsOverride(TestCacheSettingsDictionary, nameof(CacheRefresh));
            using (new LocalServiceScopedOverride<IAmbientSettingsProvider>(localProvider))
            {
                using (AmbientClock.Pause())
                {
                    TestCache ret;
                    IAmbientCacheProvider cache = new BasicAmbientCache(localProvider);
                    await cache.Store<TestCache>(true, "CacheRefresh1", this, TimeSpan.FromSeconds(1));

                    AmbientClock.SkipAhead(TimeSpan.FromMilliseconds(1100));

                    ret = await cache.Retrieve<TestCache>("CacheRefresh1", null);
                    Assert.IsNull(ret);
                    await cache.Store<TestCache>(true, "CacheRefresh1", this, TimeSpan.FromMinutes(10));
                    ret = await cache.Retrieve<TestCache>("CacheRefresh1", null);
                    Assert.AreEqual(this, ret);
                    await Eject(cache, 50);

                    await cache.Store<TestCache>(true, "CacheRefresh2", this);
                    ret = await cache.Retrieve<TestCache>("CacheRefresh2", null);
                    Assert.AreEqual(this, ret);
                    await cache.Store<TestCache>(true, "CacheRefresh3", this);
                    ret = await cache.Retrieve<TestCache>("CacheRefresh3", null);
                    Assert.AreEqual(this, ret);
                    await cache.Remove<TestCache>(true, "CacheRefresh3");
                    ret = await cache.Retrieve<TestCache>("CacheRefresh3", null);
                    Assert.IsNull(ret);

                    await Eject(cache, 50);
                }
            }
        }
        const int CountsToEject = 20;
        private async Task Eject(IAmbientCacheProvider cache, int count)
        {
            for (int ejection = 0; ejection < count; ++ejection)
            {
                for (int i = 0; i < CountsToEject; ++i)
                {
                    string shouldNotBeFoundValue;
                    shouldNotBeFoundValue = await cache.Retrieve<string>("vhxcjklhdsufihs");
                }
            }
        }
        private async Task Eject<T>(AmbientCache<T> cache, int count)
        {
            for (int ejection = 0; ejection < count; ++ejection)
            {
                for (int i = 0; i < CountsToEject; ++i)
                {
                    string shouldNotBeFoundValue;
                    shouldNotBeFoundValue = await cache.Retrieve<string>("vhxcjklhdsufihs");
                }
            }
        }
    }

    sealed class AmbientSettingsOverride : IMutableAmbientSettingsProvider
    {
        private readonly ServiceAccessor<IAmbientSettingsProvider> _settings;
        private readonly Dictionary<string, string> _overrideSettings;
        private string _name;

        public AmbientSettingsOverride(Dictionary<string, string> overrideSettings, string name, ServiceAccessor<IAmbientSettingsProvider> settings = null)
        {
            _overrideSettings = overrideSettings;
            _name = name;
            _settings = settings ?? Service.GetAccessor<IAmbientSettingsProvider>();
        }

        public string ProviderName => _name;

        public event EventHandler<AmbientSettingsChangedEventArgs> SettingsChanged;

        public void ChangeSetting(string key, string value)
        {
            _overrideSettings[key] = value;
            SettingsChanged?.Invoke(this, new AmbientSettingsChangedEventArgs { Keys = new string[] { key } });
        }

        public string GetSetting(string key)
        {
            string value;
            if (_overrideSettings.TryGetValue(key, out value))
            {
                return value;
            }
            return _settings.LocalProvider.GetSetting(key);
        }
    }
}
