using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AmbientServices.Test;

/// <summary>
/// Tests for obsolete <see cref="AmbientCache"/> / <see cref="AmbientCache{T}"/> wrappers (same behavior as <see cref="AmbientSharedCache"/>).
/// </summary>
[TestClass]
public class TestAmbientCache
{
    private static readonly Dictionary<string, string> BasicCacheSettings = new()
    {
        { nameof(BasicAmbientCache) + "-EjectFrequency", "10" },
        { nameof(BasicAmbientCache) + "-MaximumItemCount", "20" },
        { nameof(BasicAmbientCache) + "-MinimumItemCount", "1" }
    };

#pragma warning disable CS0618 // AmbientCache is obsolete but still supported until removal
    [TestMethod]
    public async Task AmbientCache_DelegatesToSharedCacheWithExplicitImplementation()
    {
        AmbientSettingsOverride settings = new(BasicCacheSettings, nameof(AmbientCache_DelegatesToSharedCacheWithExplicitImplementation));
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settings))
        {
            IAmbientSharedCache backing = new BasicAmbientCache();
            AmbientCache cache = new(typeof(TestAmbientCache), backing, "Pfx-");
            await cache.Store("k", this);
            TestAmbientCache? got = await cache.Retrieve<TestAmbientCache>("k");
            Assert.AreSame(this, got);
            await cache.Remove<TestAmbientCache>("k");
            Assert.IsNull(await cache.Retrieve<TestAmbientCache>("k"));
        }
    }

    [TestMethod]
    public async Task AmbientCacheGeneric_UsesOwnerTypeInKeyPrefix()
    {
        AmbientSettingsOverride settings = new(BasicCacheSettings, nameof(AmbientCacheGeneric_UsesOwnerTypeInKeyPrefix));
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settings))
        {
            IAmbientSharedCache backing = new BasicAmbientCache();
            AmbientCache<TestAmbientCache> cache = new(backing);
            await cache.Store("x", this);
            TestAmbientCache? got = await backing.Retrieve<TestAmbientCache>("TestAmbientCache-x");
            Assert.AreSame(this, got);
        }
    }

    [TestMethod]
    public async Task AmbientCache_NonGeneric_UsesAmbientSharedCache_DefaultPrefix()
    {
        AmbientSettingsOverride settings = new(BasicCacheSettings, nameof(AmbientCache_NonGeneric_UsesAmbientSharedCache_DefaultPrefix));
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settings))
        {
            IAmbientSharedCache backing = new BasicAmbientCache();
            using (new ScopedLocalServiceOverride<IAmbientSharedCache>(backing))
            {
                AmbientCache cache = new(typeof(TestAmbientCache));
                await cache.Store("k", this);
                TestAmbientCache? got = await backing.Retrieve<TestAmbientCache>("TestAmbientCache-k");
                Assert.AreSame(this, got);
            }
        }
    }

    [TestMethod]
    public async Task AmbientCache_NonGeneric_UsesAmbientSharedCache_ExplicitPrefix()
    {
        AmbientSettingsOverride settings = new(BasicCacheSettings, nameof(AmbientCache_NonGeneric_UsesAmbientSharedCache_ExplicitPrefix));
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settings))
        {
            IAmbientSharedCache backing = new BasicAmbientCache();
            using (new ScopedLocalServiceOverride<IAmbientSharedCache>(backing))
            {
                AmbientCache cache = new(typeof(TestAmbientCache), "Alt-");
                await cache.Store("k", this);
                TestAmbientCache? got = await backing.Retrieve<TestAmbientCache>("Alt-k");
                Assert.AreSame(this, got);
            }
        }
    }

    [TestMethod]
    public async Task AmbientCacheGeneric_UsesAmbientSharedCache_DefaultPrefix()
    {
        AmbientSettingsOverride settings = new(BasicCacheSettings, nameof(AmbientCacheGeneric_UsesAmbientSharedCache_DefaultPrefix));
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settings))
        {
            IAmbientSharedCache backing = new BasicAmbientCache();
            using (new ScopedLocalServiceOverride<IAmbientSharedCache>(backing))
            {
                AmbientCache<TestAmbientCache> cache = new();
                await cache.Store("x", this);
                TestAmbientCache? got = await backing.Retrieve<TestAmbientCache>("TestAmbientCache-x");
                Assert.AreSame(this, got);
            }
        }
    }

    [TestMethod]
    public async Task AmbientCacheGeneric_UsesAmbientSharedCache_ExplicitPrefix()
    {
        AmbientSettingsOverride settings = new(BasicCacheSettings, nameof(AmbientCacheGeneric_UsesAmbientSharedCache_ExplicitPrefix));
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settings))
        {
            IAmbientSharedCache backing = new BasicAmbientCache();
            using (new ScopedLocalServiceOverride<IAmbientSharedCache>(backing))
            {
                AmbientCache<TestAmbientCache> cache = new("Alt-");
                await cache.Store("x", this);
                TestAmbientCache? got = await backing.Retrieve<TestAmbientCache>("Alt-x");
                Assert.AreSame(this, got);
            }
        }
    }
#pragma warning restore CS0618
}
