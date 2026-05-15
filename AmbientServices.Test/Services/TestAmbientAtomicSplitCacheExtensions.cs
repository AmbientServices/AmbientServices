using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AmbientServices.Test;

[TestClass]
public sealed class TestAmbientAtomicSplitCacheExtensions
{
    private static readonly Dictionary<string, string> SettingsDictionary = new()
    {
        { nameof(BasicAmbientAtomicCache) + "-EjectFrequency", "10" },
        { nameof(BasicAmbientAtomicCache) + "-MaximumItemCount", "20" },
        { nameof(BasicAmbientAtomicCache) + "-MinimumItemCount", "1" },
    };

    [TestMethod]
    public async Task MonotonicSplitCache_GetHeadAndPayload_UsesHeadRevisionAsPayloadSlot()
    {
        AmbientSettingsOverride settings = new(SettingsDictionary, nameof(MonotonicSplitCache_GetHeadAndPayload_UsesHeadRevisionAsPayloadSlot));
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settings))
        {
            IAmbientAtomicCache cache = new BasicAmbientAtomicCache(settings);
            string baseKey = nameof(MonotonicSplitCache_GetHeadAndPayload_UsesHeadRevisionAsPayloadSlot);
            long v1 = await cache.MonotonicSplitCachePutHeadAsync(baseKey, new SplitHead(1), TimeSpan.FromHours(1));
            Assert.IsTrue(v1 >= 1);
            _ = await cache.MonotonicSplitCacheGetOrAddPayloadAsync<SplitPayload>(baseKey, v1, async () => (new SplitPayload(100), null));
            (SplitHead? h, SplitPayload? p, long v) = await cache.MonotonicSplitCacheGetHeadAndPayloadAsync<SplitHead, SplitPayload>(
                baseKey,
                minHeadVersion: -1,
                async () => throw new InvalidOperationException("Payload factory should not run when head hits."));
            Assert.IsNotNull(h);
            Assert.IsNotNull(p);
            Assert.AreEqual(v1, v);
            Assert.AreEqual(1, h!.Marker);
            Assert.AreEqual(100, p!.Value);
        }
    }

    [TestMethod]
    public async Task MonotonicSplitCache_GetHeadAndPayload_MinVersionTooNew_SkipsPayload()
    {
        AmbientSettingsOverride settings = new(SettingsDictionary, nameof(MonotonicSplitCache_GetHeadAndPayload_MinVersionTooNew_SkipsPayload));
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settings))
        {
            IAmbientAtomicCache cache = new BasicAmbientAtomicCache(settings);
            string baseKey = nameof(MonotonicSplitCache_GetHeadAndPayload_MinVersionTooNew_SkipsPayload);
            long v1 = await cache.MonotonicSplitCachePutHeadAsync(baseKey, new SplitHead(1), TimeSpan.FromHours(1));
            _ = await cache.MonotonicSplitCacheGetOrAddPayloadAsync<SplitPayload>(baseKey, v1, async () => (new SplitPayload(200), null));
            int factoryCalls = 0;
            (SplitHead? h, SplitPayload? p, long v) = await cache.MonotonicSplitCacheGetHeadAndPayloadAsync<SplitHead, SplitPayload>(
                baseKey,
                minHeadVersion: v1 + 1,
                async () =>
                {
                    System.Threading.Interlocked.Increment(ref factoryCalls);
                    return (new SplitPayload(999), null);
                });
            Assert.IsNull(h);
            Assert.IsNull(p);
            Assert.AreEqual(v1, v);
            Assert.AreEqual(0, factoryCalls);
        }
    }

    [TestMethod]
    public async Task MonotonicSplitCache_OlderPayloadSlot_RemainsAddressable()
    {
        AmbientSettingsOverride settings = new(SettingsDictionary, nameof(MonotonicSplitCache_OlderPayloadSlot_RemainsAddressable));
        using (new ScopedLocalServiceOverride<IAmbientSettingsSet>(settings))
        {
            IAmbientAtomicCache cache = new BasicAmbientAtomicCache(settings);
            string baseKey = nameof(MonotonicSplitCache_OlderPayloadSlot_RemainsAddressable);
            long v1 = await cache.MonotonicSplitCachePutHeadAsync(baseKey, new SplitHead(1), TimeSpan.FromHours(1));
            SplitPayload at1 = await cache.MonotonicSplitCacheGetOrAddPayloadAsync<SplitPayload>(baseKey, v1, async () => (new SplitPayload(10), null));
            long v2 = await cache.MonotonicSplitCachePutHeadAsync(baseKey, new SplitHead(2), TimeSpan.FromHours(1));
            Assert.IsTrue(v2 > v1);
            SplitPayload at2 = await cache.MonotonicSplitCacheGetOrAddPayloadAsync<SplitPayload>(baseKey, v2, async () => (new SplitPayload(20), null));
            SplitPayload still1 = await cache.GetOrAdd<SplitPayload>(
                AmbientAtomicSplitCacheExtensions.GetMonotonicSplitCachePayloadKey(baseKey, v1),
                async () => throw new InvalidOperationException());
            SplitPayload still2 = await cache.GetOrAdd<SplitPayload>(
                AmbientAtomicSplitCacheExtensions.GetMonotonicSplitCachePayloadKey(baseKey, v2),
                async () => throw new InvalidOperationException());
            Assert.AreEqual(10, still1.Value);
            Assert.AreEqual(20, still2.Value);
            Assert.AreSame(at1, still1);
            Assert.AreSame(at2, still2);
        }
    }

    private sealed class SplitHead
    {
        public int Marker { get; }
        public SplitHead(int marker) => Marker = marker;
    }

    private sealed class SplitPayload
    {
        public int Value { get; }
        public SplitPayload(int value) => Value = value;
    }
}
