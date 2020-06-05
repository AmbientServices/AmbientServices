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
    /// A class that holds tests for <see cref="ICache"/>.
    /// </summary>
    [TestClass]
    public class TestCache
    {
        /// <summary>
        /// Performs tests on <see cref="ICache"/>.
        /// </summary>
        [TestMethod]
        public async Task Cache()
        {
            TestCache ret;
            ICache cache = Registry<ICache>.Implementation;
            await cache.Set<TestCache>(true, "Test", this);
            ret = await cache.TryGet<TestCache>("Test", null);
            Assert.AreEqual(this, ret);
            await cache.Remove<TestCache>(true, "Test");
            ret = await cache.TryGet<TestCache>("Test", null);
            Assert.IsNull(ret);
            await cache.Set<TestCache>(true, "Test", this, DateTime.MinValue);
            ret = await cache.TryGet<TestCache>("Test", null);
            Assert.AreEqual(this, ret);
            await TriggerEjection(cache, 1);
            ret = await cache.TryGet<TestCache>("Test", null);
            Assert.IsNull(ret);
            await cache.Set<TestCache>(true, "Test", this, null, TimeSpan.FromMinutes(-1));
            ret = await cache.TryGet<TestCache>("Test", null);
            Assert.IsNull(ret);
            await cache.Set<TestCache>(true, "Test", this, DateTime.UtcNow.AddMinutes(11), TimeSpan.FromMinutes(10));
            ret = await cache.TryGet<TestCache>("Test", null);
            Assert.AreEqual(this, ret);
            ret = await cache.TryGet<TestCache>("Test", TimeSpan.FromMinutes(10));
            Assert.AreEqual(this, ret);
            await cache.Clear();
            ret = await cache.TryGet<TestCache>("Test", null);
            Assert.IsNull(ret);
        }
        const int CountsToEject = 100;
        private async Task TriggerEjection(ICache cache, int count)
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
