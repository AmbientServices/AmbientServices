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
    /// A class that holds tests for <see cref="IAmbientCache"/>.
    /// </summary>
    [TestClass]
    public class TestCache
    {
        /// <summary>
        /// Performs tests on <see cref="IAmbientCache"/>.
        /// </summary>
        [TestMethod]
        public async Task Cache()
        {
            TestCache ret;
            IAmbientCache cache = new BasicAmbientCache();
            await cache.Set<TestCache>(true, "Test", this);
            ret = await cache.TryGet<TestCache>("Test", null);
            Assert.AreEqual(this, ret);
            await cache.Remove<TestCache>(true, "Test");
            ret = await cache.TryGet<TestCache>("Test", null);
            Assert.IsNull(ret);
            await cache.Set<TestCache>(true, "Test", this, null, DateTime.MinValue);
            ret = await cache.TryGet<TestCache>("Test", null);
            Assert.AreEqual(this, ret);
            await TriggerEjection(cache, 1);
            ret = await cache.TryGet<TestCache>("Test", null);
            Assert.IsNull(ret);
            await cache.Set<TestCache>(true, "Test", this, TimeSpan.FromMinutes(-1));
            ret = await cache.TryGet<TestCache>("Test", null);
            Assert.IsNull(ret);
            await cache.Set<TestCache>(true, "Test", this, TimeSpan.FromMinutes(10), DateTime.UtcNow.AddMinutes(11));
            ret = await cache.TryGet<TestCache>("Test", null);
            Assert.AreEqual(this, ret);
            await cache.Set<TestCache>(true, "Test", this, TimeSpan.FromMinutes(10), DateTime.Now.AddMinutes(11));
            ret = await cache.TryGet<TestCache>("Test", null);
            Assert.AreEqual(this, ret);
            await cache.Set<TestCache>(true, "Test", this, TimeSpan.FromMinutes(60), DateTime.UtcNow.AddMinutes(10));
            ret = await cache.TryGet<TestCache>("Test", null);
            Assert.AreEqual(this, ret);
            ret = await cache.TryGet<TestCache>("Test", TimeSpan.FromMinutes(10));
            Assert.AreEqual(this, ret);
            await TriggerEjection(cache, 50);
            await cache.Clear();
            ret = await cache.TryGet<TestCache>("Test", null);
            Assert.IsNull(ret);
        }
        /// <summary>
        /// Performs tests on <see cref="IAmbientCache"/>.
        /// </summary>
        [TestMethod]
        public async Task CacheRefresh()
        {
            TestCache ret;
            IAmbientCache cache = new BasicAmbientCache();
            await cache.Set<TestCache>(true, "CacheRefresh1", this, TimeSpan.FromSeconds(1));
            System.Threading.Thread.Sleep(1100);
            ret = await cache.TryGet<TestCache>("CacheRefresh1", null);
            Assert.IsNull(ret);
            await cache.Set<TestCache>(true, "CacheRefresh1", this, TimeSpan.FromMinutes(10));
            ret = await cache.TryGet<TestCache>("CacheRefresh1", null);
            Assert.AreEqual(this, ret);
            await TriggerEjection(cache, 50);

            await cache.Set<TestCache>(true, "CacheRefresh2", this);
            ret = await cache.TryGet<TestCache>("CacheRefresh2", null);
            Assert.AreEqual(this, ret);
            await cache.Set<TestCache>(true, "CacheRefresh2", this);
            ret = await cache.TryGet<TestCache>("CacheRefresh2", null);
            Assert.AreEqual(this, ret);
            await cache.Remove<TestCache>(true, "CacheRefresh2");
            ret = await cache.TryGet<TestCache>("CacheRefresh2", null);
            Assert.IsNull(ret);

            await TriggerEjection(cache, 50);
        }
        const int CountsToEject = 100;
        private async Task TriggerEjection(IAmbientCache cache, int count)
        {
            for (int ejection = 0; ejection < count; ++ejection)
            {
                for (int i = 0; i < CountsToEject; ++i)
                {
                    string shouldNotBeFoundValue;
                    shouldNotBeFoundValue = await cache.TryGet<string>("vhxcjklhdsufihs");
                }
            }
        }
    }
}
